using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{

    public sealed class LuaBridge : IDisposable
    {
        public const int Port = 4444;
        private const int MaxPayload = 1048576;

        private TcpListener? _listener;
        private TcpClient? _game;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public bool IsGameConnected
        {
            get { lock (_lock) return _game is { Connected: true }; }
        }

        public event Action<string>? Log;
        public event Action<string, string>? GameReady;
        public event Action<JsonElement>? VehicleReceived;
        public event Action<JsonElement>? VehCfgReceived;
        public event Action<JsonElement>? ChatReceived;
        public event Action? GameDisconnected;

        public bool IsStarted
        {
            get { lock (_lock) return _listener != null; }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_listener != null) return;
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
            }
            Log?.Invoke($"本地桥已监听 127.0.0.1:{Port}，等待游戏内模组连接");
            _ = Task.Run(() => AcceptLoopAsync(_cts!.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener!.AcceptTcpClientAsync(token); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Log?.Invoke("本地桥监听异常：" + ex.Message); return; }

                NetworkStream stream;
                lock (_lock)
                {
                    CloseGameLocked();
                    _game = client;
                    _stream = stream = client.GetStream();
                }
                client.NoDelay = true;
                Log?.Invoke("游戏内模组已连接到启动器");
                _ = Task.Run(() => ReceiveLoopAsync(client, stream, token));
            }
        }

        private async Task ReceiveLoopAsync(TcpClient client, NetworkStream stream, CancellationToken token)
        {
            var buffer = new byte[65536];
            var pending = new MemoryStream();

            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    int n = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (n == 0) break;
                    pending.Write(buffer, 0, n);

                    while (true)
                    {
                        var data = pending.ToArray();
                        if (data.Length < 4) break;

                        int len = data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
                        if (len <= 0 || len > MaxPayload)
                        {
                            Log?.Invoke($"模组数据长度非法（{len}），丢弃缓冲");
                            pending.SetLength(0);
                            break;
                        }
                        if (data.Length < 4 + len) break;

                        string json = Encoding.UTF8.GetString(data, 4, len);
                        var rest = new byte[data.Length - 4 - len];
                        Array.Copy(data, 4 + len, rest, 0, rest.Length);
                        pending.SetLength(0);
                        pending.Write(rest, 0, rest.Length);

                        Dispatch(json);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log?.Invoke("模组连接出错：" + ex.Message);
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_game, client)) CloseGameLocked();
                }
                try { client.Close(); } catch { }
                Log?.Invoke("模组与启动器断开");
                GameDisconnected?.Invoke();
            }
        }

        private void Dispatch(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                if (!root.TryGetProperty("type", out var t)) return;

                switch (t.GetString())
                {
                    case "ready":
                        string name = root.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                        string ver = root.TryGetProperty("version", out var vp) ? vp.GetString() ?? "" : "";
                        Log?.Invoke($"模组就绪 name={name} version={ver}");
                        GameReady?.Invoke(name, ver);
                        break;

                    case "vehicle":
                        if (root.TryGetProperty("id", out _)) VehicleReceived?.Invoke(root.Clone());
                        break;

                    case "vehcfg":
                        VehCfgReceived?.Invoke(root.Clone());
                        break;

                    case "chat":
                        ChatReceived?.Invoke(root.Clone());
                        break;

                    case "ping":
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("解析模组消息失败：" + ex.Message);
            }
        }

        public void SendJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            SendRaw(Encoding.UTF8.GetBytes(json));
        }

        public async Task SendAsync(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            await SendRawAsync(Encoding.UTF8.GetBytes(json));
        }

        public void Send(object payload)
        {
            try { SendJson(JsonSerializer.Serialize(payload)); }
            catch (Exception ex) { Log?.Invoke("发送到游戏失败：" + ex.Message); }
        }

        private void SendRaw(byte[] body)
        {
            _ = SendRawAsync(body);
        }

        private async Task SendRawAsync(byte[] body)
        {
            await _sendLock.WaitAsync();
            try
            {
                NetworkStream? s;
                lock (_lock) s = _stream;
                if (s == null || body.Length > MaxPayload) return;

                var frame = new byte[4 + body.Length];
                int len = body.Length;
                frame[0] = (byte)(len & 0xFF);
                frame[1] = (byte)((len >> 8) & 0xFF);
                frame[2] = (byte)((len >> 16) & 0xFF);
                frame[3] = (byte)((len >> 24) & 0xFF);
                Array.Copy(body, 0, frame, 4, body.Length);

                await s.WriteAsync(frame, 0, frame.Length);
                await s.FlushAsync();
            }
            catch (Exception ex)
            {
                Log?.Invoke("发送到游戏失败：" + ex.Message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void CloseGameLocked()
        {
            try { _stream?.Close(); } catch { }
            try { _game?.Close(); } catch { }
            _stream = null;
            _game = null;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            lock (_lock)
            {
                CloseGameLocked();
                try { _listener?.Stop(); } catch { }
                _listener = null;
            }
        }

        public void Dispose() => Stop();
    }
}
