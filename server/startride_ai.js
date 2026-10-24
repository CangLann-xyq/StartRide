/**
 * StartRide Launcher · AI 助手代理（挂在 /api/startride/ai）
 * ---------------------------------------------------------------------------
 * 为什么必须有这一层（客户端不直接调 DeepSeek）：
 *   1) 密钥不能出现在安装包里。客户端是 .NET 程序集，任何写进代码/配置的 key
 *      都能用 ildasm/strings 几分钟扒出来；一旦泄露就是按量计费的损失。
 *      所以 key 只存在于服务器 .env（SR_AI_DEEPSEEK_KEY），客户端拿到的是
 *      「本服务的地址」，拿不到 key，也换不了别人的 key 来刷。
 *   2) 可以限流。DeepSeek 是按 token 计费的，客户端直连等于把计费权交出去；
 *      这里按 IP 限流 + 并发上限，账号被刷爆的风险落在服务端可控范围内。
 *   3) 可以换模型/换供应商而不发版。客户端只认 deepseek-chat / deepseek-reasoner
 *      两个逻辑名，真正的上游随时能在服务端改。
 *
 * 流式：
 *   直接透传上游 SSE（text/event-stream），一个字节都不攒 —— 客户端要实时显示
 *   「思考过程（reasoning_content）」和正文，攒完再发就全失去意义了。
 *   nginx 那条 location 必须配 proxy_buffering off（见站点配置），否则会在
 *   反代层被缓冲成一整块。
 *
 * 安全边界（诚实说明）：
 *   x-sr-client 头只是「不是随手 curl 就能用」的门槛，不是强认证 —— 客户端
 *   二进制里能翻到这个值。真正的护城河是「key 不在客户端」，以及限流。
 */
const express = require("express");
const fs = require("fs");
const path = require("path");

const router = express.Router();

// ---------------------------------------------------------------------------
// 配置：本进程（startride_server.js）不加载 .env，所以这里自己读一次。
// process.env 优先（systemd 若配了 EnvironmentFile 就以那里为准），文件兜底。
// ---------------------------------------------------------------------------
function readEnvFile() {
  const out = {};
  try {
    const txt = fs.readFileSync(path.join(__dirname, "..", ".env"), "utf8");
    for (const line of txt.split(/\r?\n/)) {
      const m = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/);
      if (!m) continue;
      let v = m[2].trim();
      if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
        v = v.slice(1, -1);
      }
      out[m[1]] = v;
    }
  } catch (e) {
    // 读不到就当作没配，下面会返回 503，不会抛栈到客户端
  }
  return out;
}

const ENV = readEnvFile();
function cfg(key, dflt) {
  const v = process.env[key] || ENV[key];
  return v && String(v).trim() ? String(v).trim() : dflt;
}

const API_KEY = cfg("SR_AI_DEEPSEEK_KEY", "") || cfg("DEEPSEEK_API_KEY", "");
const API_URL = cfg("SR_AI_DEEPSEEK_URL", "https://api.deepseek.com/v1/chat/completions");
const CLIENT_TAG = cfg("SR_AI_CLIENT_TAG", "startride-launcher");

// 客户端只能点这两个逻辑名；真实上游模型由服务端决定
const MODELS = new Set(["deepseek-chat", "deepseek-reasoner"]);
const MAX_MESSAGES = 40;        // 单次请求最多带多少条历史（含 tool 结果）
const MAX_TOKENS_CAP = 8192;
const WINDOW_MS = 10 * 60 * 1000;
const PER_IP_LIMIT = 60;        // 10 分钟 60 次：一次 agent 任务约 3~8 次往返
const MAX_CONCURRENCY = 8;

// ---------------------------------------------------------------------------
// 限流（进程内；单实例够用。多实例部署要换成 Redis 计数）
// ---------------------------------------------------------------------------
const hits = new Map();         // ip -> { n, resetAt }
let active = 0;

function hitOk(ip) {
  const now = Date.now();
  const rec = hits.get(ip);
  if (!rec || rec.resetAt <= now) {
    hits.set(ip, { n: 1, resetAt: now + WINDOW_MS });
    if (hits.size > 5000) {          // 兜底防内存膨胀
      for (const [k, v] of hits) if (v.resetAt <= now) hits.delete(k);
    }
    return true;
  }
  if (rec.n >= PER_IP_LIMIT) return false;
  rec.n += 1;
  return true;
}

function clientIp(req) {
  const fwd = req.headers["x-forwarded-for"];
  if (typeof fwd === "string" && fwd.length) return fwd.split(",")[0].trim();
  return req.socket && req.socket.remoteAddress ? req.socket.remoteAddress : "unknown";
}

// ---------------------------------------------------------------------------
// 入参清洗：宁可多丢掉一截历史，也不要把可疑字段原样转给上游
// ---------------------------------------------------------------------------
function sanitizeMessages(input) {
  if (!Array.isArray(input)) return null;
  const out = [];
  for (const m of input.slice(-MAX_MESSAGES)) {
    if (!m || typeof m !== "object") continue;
    const role = String(m.role || "");
    if (!["system", "user", "assistant", "tool"].includes(role)) continue;
    const rec = { role };
    if (typeof m.content === "string") rec.content = m.content.slice(0, 60000);
    else rec.content = "";
    if (role === "assistant" && Array.isArray(m.tool_calls)) {
      rec.tool_calls = m.tool_calls.map((c) => ({
        id: String((c && c.id) || ""),
        type: "function",
        function: {
          name: String((c && c.function && c.function.name) || ""),
          arguments: String((c && c.function && c.function.arguments) || "{}"),
        },
      }));
    }
    if (role === "tool") {
      rec.tool_call_id = String(m.tool_call_id || "");
      if (!rec.content) rec.content = "(空)";
    }
    out.push(rec);
  }
  return out.length ? out : null;
}

function sanitizeTools(input) {
  if (!Array.isArray(input)) return undefined;
  const out = [];
  for (const t of input.slice(0, 24)) {
    if (!t || typeof t !== "object") continue;
    const fn = t.function || {};
    if (!fn.name) continue;
    out.push({
      type: "function",
      function: {
        name: String(fn.name),
        description: String(fn.description || "").slice(0, 2000),
        parameters: (fn.parameters && typeof fn.parameters === "object") ? fn.parameters : { type: "object", properties: {} },
      },
    });
  }
  return out.length ? out : undefined;
}

// ---------------------------------------------------------------------------
// GET /api/startride/ai/health —— 客户端连不上时的自检入口（不暴露 key）
// ---------------------------------------------------------------------------
router.get("/health", (req, res) => {
  res.json({
    code: 200,
    ok: true,
    keyConfigured: Boolean(API_KEY),
    models: Array.from(MODELS),
    upstream: API_URL.replace(/\/\/[^/]+/, "//…"),   // 只给主机名之外的形状，不泄露完整 URL
  });
});

// ---------------------------------------------------------------------------
// POST /api/startride/ai/chat —— 流式透传
// ---------------------------------------------------------------------------
router.post("/chat", async (req, res) => {
  if (!API_KEY) {
    return res.status(503).json({ code: 503, msg: "服务端尚未配置 AI 密钥" });
  }
  const tag = String(req.headers["x-sr-client"] || "");
  if (tag !== CLIENT_TAG) {
    return res.status(403).json({ code: 403, msg: "仅限 StartRide 启动器调用" });
  }
  const ip = clientIp(req);
  if (!hitOk(ip)) {
    return res.status(429).json({ code: 429, msg: "请求过于频繁，请稍后再试" });
  }
  if (active >= MAX_CONCURRENCY) {
    return res.status(429).json({ code: 429, msg: "服务繁忙，请稍后再试" });
  }

  const body = req.body || {};
  const messages = sanitizeMessages(body.messages);
  if (!messages) {
    return res.status(400).json({ code: 400, msg: "messages 不合法" });
  }
  const model = MODELS.has(String(body.model || "")) ? String(body.model) : "deepseek-chat";
  const payload = { model, messages, stream: true };
  const tools = sanitizeTools(body.tools);
  if (tools) {
    payload.tools = tools;
    payload.tool_choice = "auto";
  }
  const maxTokens = parseInt(body.max_tokens, 10);
  if (Number.isFinite(maxTokens) && maxTokens > 0) {
    payload.max_tokens = Math.min(maxTokens, MAX_TOKENS_CAP);
  }
  const temp = Number(body.temperature);
  if (Number.isFinite(temp) && temp >= 0 && temp <= 2) {
    payload.temperature = temp;
  }

  // ⚠️ 必须挂 res 的 close，不能挂 req 的：Node 16+ 在**请求体读完**时就会在 req 上
  // 发 close，挂上去会把每个正常请求当场 abort（现象是 curl 全部 HTTP=000，
  // 服务端日志却是「客户端断开」，看起来像网络问题，实际是监听器挂错对象）。
  const ctrl = new AbortController();
  let closed = false;
  res.on("close", () => {
    if (!res.writableEnded) { closed = true; ctrl.abort(); }
  });

  active += 1;
  const started = Date.now();
  try {
    const upstream = await fetch(API_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + API_KEY,
        Accept: "text/event-stream",
      },
      body: JSON.stringify(payload),
      signal: ctrl.signal,
    });

    if (!upstream.ok || !upstream.body) {
      const detail = await upstream.text().catch(() => "");
      console.log("[SR-AI] 上游失败", upstream.status, detail.slice(0, 200));
      return res.status(502).json({
        code: 502,
        msg: "上游返回 " + upstream.status,
        detail: detail.slice(0, 300),
      });
    }

    res.writeHead(200, {
      "Content-Type": "text/event-stream; charset=utf-8",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive",
      "X-Accel-Buffering": "no",     // 双保险：nginx 不缓冲
    });

    const reader = upstream.body.getReader();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      if (closed) break;
      res.write(Buffer.from(value));
    }
    res.end();
    console.log("[SR-AI] 完成", model, "ms=" + (Date.now() - started));
  } catch (e) {
    if (closed) {
      console.log("[SR-AI] 客户端断开", model);
    } else {
      console.log("[SR-AI] 异常", (e && e.name) || "", String(e && e.message || e).slice(0, 200));
      if (!res.headersSent) {
        res.status(502).json({ code: 502, msg: "AI 服务暂时不可用" });
      } else {
        try { res.end(); } catch (_) { /* 已经断了 */ }
      }
    }
  } finally {
    active -= 1;
  }
});

module.exports = router;
