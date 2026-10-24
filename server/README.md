# server · AI 助手代理

启动器内置的 AI 助手**不直连 DeepSeek**，中间隔着这一层（`:3002` 的 `/api/startride/ai`）。

## 为什么要这一层

1. **密钥不能进安装包。** 客户端是 .NET 程序集，写进代码或配置的 key 用 `strings` /
   `ildasm` 几分钟就能扒出来，一旦泄露就是按量计费的损失，换 key 还得重新发版。
   现在 key 只在服务器 `.env` 的 `SR_AI_DEEPSEEK_KEY`，客户端只知道自己的服务地址。
2. **能限流。** 按 IP 10 分钟 60 次 + 进程内并发 8，账号被刷爆的风险留在服务端。
3. **能换模型/换供应商而不发版。** 客户端只认 `deepseek-chat` / `deepseek-reasoner`
   两个逻辑名。

## 文件

| 文件 | 作用 |
|---|---|
| `startride_ai.js` | 路由本体，挂到 `/api/startride/ai`（`POST /chat` 流式透传、`GET /health`） |
| `deploy_ai_proxy.py` | 幂等部署：上传路由 → 挂载 → 补 `.env` → 插 nginx location → 重启 `:3002` → 验证 |

## 部署

```bash
python server/deploy_ai_proxy.py --dry-run     # 先看要改什么
python server/deploy_ai_proxy.py               # 真部署
```

脚本做的事（全部幂等，重跑不叠加）：

- 备份 `.env` / `startride_server.js` / 两个 nginx 站点（`.bak-ai-<时间戳>`）
- 上传 `router/startride_ai.js` 并 `node --check`
- 在 `startride_server.js` 里**插在** `app.use("/api/startride", ...)` 之前挂载
- `.env` 补 `SR_AI_DEEPSEEK_KEY`（已存在且值不同只报出来，不擅自覆盖）
- nginx 插 `location ^~ /api/startride/ai/`，**必须** `proxy_buffering off`
  + `proxy_read_timeout 600s`：老位置 30s 超时、默认 buffering 会把 SSE 攒成一整块，
  客户端就看不到「思考过程」逐字冒出来
- `systemctl restart startride-server`（**绝不碰 `:3001` 风寻**）
- 验证本机/公网 health、公网流式首帧时间

## 两个已经踩过的坑

- ⚠️ **`req.on("close")` 会 abort 掉每个正常请求**：Node 16+ 在请求体读完时就在 `req`
  上发 close，挂上去的现象是 curl 全部 `HTTP=000`、服务端日志全是「客户端断开」，
  看着像网络问题，其实是监听器挂错了对象。要挂 `res.on("close")` 并判断
  `!res.writableEnded`。
- ⚠️ **同一个 IP、同一个 443，换 SNI 就握手失败**（本机实测：SNI=`cloudfurhub.cn`
  被链路重置，SNI=`windseek.cloud` 正常；服务器侧同 SNI 自测 200、证书到 2027）。
  所以客户端 `AiChatClient` 带主/备域名兜底，一个不通自动切另一个。

## 安全边界（诚实说明）

`x-sr-client: startride-launcher` 只是「不是随手 curl 就能用」的门槛，不是强认证 ——
客户端二进制里能翻到它。真正的护城河是**key 不在客户端**，加上限流。
如果以后要防得更死，得给每个玩家发短期票据（登录态 HMAC），那是另一件事。
