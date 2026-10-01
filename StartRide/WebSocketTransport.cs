using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{

    public sealed class WebSocketTransport : IDisposable
    {
        private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const int PingIntervalMs = 20000;

        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        private readonly object _sendLock = new();
        private readonly MemoryStream _recvBuf = new();

        public bool IsOpen { get; private set; }

        public event Action<string>? MessageReceived;
        public event Action<string>? Log;
        public event Action<string>? Closed;

        public string RemoteDescription { get; private set; } = "";

        public async Task ConnectAsync(string host, int port, string path, TimeSpan timeout)
        {
            _cts = new CancellationTokenSource();

            _tcp = new TcpClient { NoDelay = true };
            var connectTask = _tcp.ConnectAsync(host, port);
            var done = await Task.WhenAny(connectTask, Task.Delay(timeout, _cts.Token));
            if (done != connectTask || !_tcp.Connected)
                throw new TimeoutException($"连接 {host}:{port} 超时");

            _stream = _tcp.GetStream();
            RemoteDescription = $"ws://{host}:{port}{path}";

            var keyBytes = new byte[16];
            RandomNumberGenerator.Fill(keyBytes);
            string key = Convert.ToBase64String(keyBytes);

            var req = new StringBuilder();
            req.Append($"GET {path} HTTP/1.1\r\n");
            req.Append($"Host: {host}\r\n");
            req.Append("Upgrade: websocket\r\n");
            req.Append("Connection: Upgrade\r\n");
            req.Append($"Sec-WebSocket-Key: {key}\r\n");
            req.Append("Sec-WebSocket-Version: 13\r\n");
            req.Append("Origin: http://" + host + "\r\n");
            req.Append("\r\n");

            var reqBytes = Encoding.ASCII.GetBytes(req.ToString());
            await _stream.WriteAsync(reqBytes, 0, reqBytes.Length, _cts.Token);
            await _stream.FlushAsync(_cts.Token);

            string header = await ReadHttpHeaderAsync(timeout);
            if (!header.StartsWith("HTTP/1.1 101", StringComparison.OrdinalIgnoreCase) &&
                !header.StartsWith("HTTP/1.0 101", StringComparison.OrdinalIgnoreCase))
            {
                string firstLine = header.Split('\n')[0].Trim();
                throw new IOException("WebSocket 升级被拒绝：" + firstLine);
            }

            string expect = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(key + WsGuid)));
            if (header.IndexOf("sec-websocket-accept: " + expect, StringComparison.OrdinalIgnoreCase) < 0)
                throw new IOException("WebSocket 握手校验失败：Sec-WebSocket-Accept 不匹配");

            IsOpen = true;
            Log?.Invoke($"WebSocket 隧道已建立 {RemoteDescription}");

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            _ = Task.Run(() => PingLoopAsync(_cts.Token));
        }

        private async Task<string> ReadHttpHeaderAsync(TimeSpan timeout)
        {
            var buf = new byte[1];
            var sb = new StringBuilder();
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                int n;
                try { n = await _stream!.ReadAsync(buf, 0, 1, _cts!.Token); }
                catch (OperationCanceledException) { throw new TimeoutException("握手被取消"); }
                if (n == 0) throw new IOException("握手期间连接被对端关闭");

                sb.Append((char)buf[0]);
                if (sb.Length >= 4 &&
                    sb[sb.Length - 4] == '\r' && sb[sb.Length - 3] == '\n' &&
                    sb[sb.Length - 2] == '\r' && sb[sb.Length - 1] == '\n')
                {
                    return sb.ToString();
                }
                if (sb.Length > 16384) throw new IOException("握手响应头过长");
            }
            throw new TimeoutException("等待 WebSocket 握手响应超时");
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var chunk = new byte[8192];
            int fragOpcode = 0;
            var fragments = new MemoryStream();

            try
            {
                while (!token.IsCancellationRequested && IsOpen)
                {
                    int n = await _stream!.ReadAsync(chunk, 0, chunk.Length, token);
                    if (n == 0) { CloseInternal("对端关闭连接"); return; }
                    lock (_recvBuf) _recvBuf.Write(chunk, 0, n);

                    while (true)
                    {
                        byte[]? payload = null;
                        byte opcode = 0;
                        bool fin = false;

                        lock (_recvBuf)
                        {

                            int avail = (int)_recvBuf.Length;
                            var data = _recvBuf.GetBuffer();
                            if (!TryParseFrame(data, avail, out fin, out opcode, out payload, out int consumed))
                                break;
                            int restLen = avail - consumed;
                            if (restLen > 0) Buffer.BlockCopy(data, consumed, data, 0, restLen);

                            _recvBuf.SetLength(restLen);
                        }

                        switch (opcode)
                        {
                            case 0x1:
                            case 0x2:
                            case 0x0:
                                if (opcode != 0x0) fragOpcode = opcode;
                                if (payload is { Length: > 0 }) fragments.Write(payload, 0, payload.Length);
                                if (fin)
                                {
                                    var text = Encoding.UTF8.GetString(fragments.ToArray());
                                    fragments.SetLength(0);
                                    fragOpcode = 0;
                                    if (!string.IsNullOrWhiteSpace(text)) MessageReceived?.Invoke(text);
                                }
                                break;

                            case 0x8:
                                CloseInternal("对端发起关闭");
                                return;

                            case 0x9:
                                SendFrame(0xA, payload ?? Array.Empty<byte>());
                                break;

                            case 0xA:
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CloseInternal("接收异常：" + ex.Message);
            }
        }

        private static bool TryParseFrame(byte[] d, int dataLen, out bool fin, out byte opcode,
                                          out byte[]? payload, out int consumed)
        {
            fin = false; opcode = 0; payload = null; consumed = 0;
            if (dataLen < 2) return false;

            fin = (d[0] & 0x80) != 0;
            opcode = (byte)(d[0] & 0x0F);
            bool masked = (d[1] & 0x80) != 0;
            long len = d[1] & 0x7F;
            int offset = 2;

            if (len == 126)
            {
                if (dataLen < 4) return false;
                len = (d[2] << 8) | d[3];
                offset = 4;
            }
            else if (len == 127)
            {
                if (dataLen < 10) return false;
                len = 0;
                for (int i = 0; i < 8; i++) len = (len << 8) | d[2 + i];
                offset = 10;
            }

            byte[]? mask = null;
            if (masked)
            {
                if (dataLen < offset + 4) return false;
                mask = new byte[4];
                Array.Copy(d, offset, mask, 0, 4);
                offset += 4;
            }

            if (len > int.MaxValue || dataLen < offset + len) return false;

            var body = new byte[len];
            Array.Copy(d, offset, body, 0, (int)len);
            if (masked && mask != null)
                for (int i = 0; i < body.Length; i++) body[i] ^= mask[i & 3];

            payload = body;
            consumed = offset + (int)len;
            return true;
        }

        public void SendText(string text)
        {
            if (!IsOpen) return;
            SendFrame(0x1, Encoding.UTF8.GetBytes(text));
        }

        private void SendFrame(byte opcode, byte[] payload)
        {
            try
            {
                lock (_sendLock)
                {
                    if (_stream == null) return;
                    var head = BuildHeader(opcode, payload.Length, masked: true);
                    _stream.Write(head, 0, head.Length);

                    if (payload.Length > 0)
                    {
                        var mask = new byte[4];
                        RandomNumberGenerator.Fill(mask);
                        Buffer.BlockCopy(head, head.Length - 4, mask, 0, 4);
                        var masked = new byte[payload.Length];
                        for (int i = 0; i < payload.Length; i++) masked[i] = (byte)(payload[i] ^ mask[i & 3]);
                        _stream.Write(masked, 0, masked.Length);
                    }
                    _stream.Flush();
                }
            }
            catch (Exception ex)
            {
                CloseInternal("发送失败：" + ex.Message);
            }
        }

        private static byte[] BuildHeader(byte opcode, int len, bool masked)
        {
            using var ms = new MemoryStream();
            ms.WriteByte((byte)(0x80 | opcode));

            int maskBit = masked ? 0x80 : 0;
            if (len < 126)
            {
                ms.WriteByte((byte)(maskBit | len));
            }
            else if (len < 65536)
            {
                ms.WriteByte((byte)(maskBit | 126));
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(len & 0xFF));
            }
            else
            {
                ms.WriteByte((byte)(maskBit | 127));
                for (int i = 7; i >= 0; i--) ms.WriteByte((byte)(((long)len >> (8 * i)) & 0xFF));
            }

            if (masked)
            {
                var mask = new byte[4];
                RandomNumberGenerator.Fill(mask);
                ms.Write(mask, 0, 4);
            }
            return ms.ToArray();
        }

        private async Task PingLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsOpen)
                {
                    await Task.Delay(PingIntervalMs, token);
                    if (!IsOpen) return;
                    SendFrame(0x9, Array.Empty<byte>());
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CloseInternal(string reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            try { _cts?.Cancel(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            Log?.Invoke("WebSocket 隧道关闭：" + reason);
            Closed?.Invoke(reason);
        }

        public void Dispose() => CloseInternal("本地主动关闭");
    }
}
