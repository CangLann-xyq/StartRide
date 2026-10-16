-- ============================================================================
-- StartRide 玩法积分
-- ============================================================================
-- 警匪追逐 / 德比 / 捉迷藏三个玩法的跨局积分账本。
--
-- 为什么不做网络同步：结算要用到的输入（winner / roles / roster / 名次 /
-- 找到人数）本来就是各客户端**同步过的一致数据**，同一套权重算出来的分
-- 天然一致。多开一条积分通道就多一处「名字对不上就静默失能」的机会 ——
-- 这个项目已经在同类问题上栽过四次，能不加通道就不加。
--
-- 记账口径：
--   roundKey = mode .. ':' .. round   —— 同一局只结算一次
--     （结算画面会停留好几秒，主模组每帧都会来问，不去重就会重复加分）
--   会话结束（room-closed）→ reset()；换玩法**不清账**（整场房间累计）
--
-- 权重可由 startride/multiplayer.json 的 modeRules 直接覆盖（全部可选，
-- 逐键容错）：sCop / sCapturer / sRobberRun / sRobberHeld /
-- sDerbyChamp / sDerbyRank2 / sDerbyRank3 / sDerbyRest /
-- sSeekerWin / sHiderLast / sEscaper / sRoundMax
-- ============================================================================

local M = {}

local TUNE = {
  scoreEnabled = 1,     -- 0 = 完全关闭积分（不记账也不显示）
  sRoundMax    = 200,   -- 单局单人得分上限（兜底，防配置写错炸掉榜单）
  sCop         = 3,     -- 警匪：抓到强盗，每名警察
  sCapturer    = 2,     -- 警匪：实际贴住强盗的那名警察额外
  sRobberRun   = 5,     -- 警匪：强盗撑满全场（未被抓）
  sRobberHeld  = 1,     -- 警匪：强盗被抓的参与分
  sDerbyChamp  = 5,     -- 德比：最后存活
  sDerbyRank2  = 3,     -- 德比：第 2 名
  sDerbyRank3  = 2,     -- 德比：第 3 名
  sDerbyRest   = 1,     -- 德比：其余名次（参与分）
  sSeekerWin   = 3,     -- 捉迷藏：找齐所有人，每名搜索者
  sHiderLast   = 4,     -- 捉迷藏：躲藏方胜，每个撑到最后的躲藏者
  sEscaper     = 3,     -- 捉迷藏：成功逃出逃脱区的人额外
}

local S = {
  mode     = '',
  round    = -1,
  roundKey = '',
  loaded   = false,
  totals   = {},        -- [id] = 累计分
  names    = {},        -- [id] = 昵称
  gains    = {},        -- [id] = 本局得分
  myId     = 'self',
  myTotal  = 0,
  myGain   = 0,
  board    = {},        -- 排序后的前若干名
  lastRound = 0,
}

local BOARD_SIZE = 8

-- ---------------------------------------------------------------------------
-- 工具
-- ---------------------------------------------------------------------------
local function logScore(...)
  local parts = { '[StartRide] 积分：' }
  for _, v in ipairs({ ... }) do parts[#parts + 1] = tostring(v) end
  pcall(function() log('I', 'startride', table.concat(parts, ' ')) end)
end

local function num(v, lo, hi)
  local n = tonumber(v)
  if not n or n < lo or n > hi then return nil end
  return n
end

-- ---------------------------------------------------------------------------
-- 配置读取（与各玩法模块同一份文件、同一套容错）
-- ---------------------------------------------------------------------------
local function readConfig()
  local cfg = nil
  pcall(function() cfg = jsonReadFile('startride/multiplayer.json') end)
  if type(cfg) ~= 'table' then
    pcall(function() cfg = jsonReadFile('current/startride/multiplayer.json') end)
  end
  S.loaded = true
  if type(cfg) ~= 'table' then return false end

  local rules = cfg.modeRules
  if type(rules) ~= 'table' then rules = cfg end
  local bucket = rules.score
  if type(bucket) ~= 'table' then bucket = rules end

  for k, cur in pairs(TUNE) do
    local v = num(bucket[k], 0, 1000)
    if v then TUNE[k] = v end
  end
  TUNE.scoreEnabled = (num(bucket.scoreEnabled, 0, 1) or TUNE.scoreEnabled) > 0 and 1 or 0
  return true
end

-- ---------------------------------------------------------------------------
-- 记账
-- ---------------------------------------------------------------------------
local function ensure(pid, name)
  pid = tostring(pid or '')
  if pid == '' then return nil end
  if S.totals[pid] == nil then S.totals[pid] = 0 end
  if name ~= nil and tostring(name) ~= '' then S.names[pid] = tostring(name) end
  return pid
end

local function gain(pid, n)
  pid = ensure(pid)
  if not pid then return end
  local v = tonumber(n) or 0
  if v <= 0 then return end
  S.gains[pid] = (S.gains[pid] or 0) + v
end

local function clampGain(v)
  local max = tonumber(TUNE.sRoundMax) or 200
  if max <= 0 then return v end
  if v > max then return max end
  return v
end

-- ---------------------------------------------------------------------------
-- 各玩法结算
-- ---------------------------------------------------------------------------

-- 警匪：抓到 → 全体警察 + 抓人者额外 + 强盗参与分；逃脱 → 强盗撑满全场。
local function settleCopsRobber(ctx)
  local winner = tostring(ctx.winner or '')
  local roles = ctx.roles or {}
  local captureBy = tostring(ctx.stats and ctx.stats.captureBy or '')
  for _, p in ipairs(ctx.roster or {}) do
    local pid = ensure(p.id, p.name)
    if pid then
      local role = tostring(roles[pid] or roles[p.id] or '')
      if winner == 'cop' then
        if role == 'cop' then gain(pid, TUNE.sCop) end
        if captureBy ~= '' and pid == captureBy then gain(pid, TUNE.sCapturer) end
        if role == 'robber' then gain(pid, TUNE.sRobberHeld) end
      elseif winner == 'robber' then
        if role == 'robber' then gain(pid, TUNE.sRobberRun) end
      end
    end
  end
end

-- 德比：名次分（未淘汰优先、车损小优先，与 derbyResults 同一把尺）。
-- 名次表拿不到时退化成「按 roster 顺序」—— 宁可给个粗糙名次，也不要全零。
local function settleDerby(ctx)
  local order = ctx.stats and ctx.stats.derby
  if type(order) ~= 'table' or #order == 0 then
    order = {}
    for _, p in ipairs(ctx.roster or {}) do
      order[#order + 1] = { id = tostring(p.id), name = tostring(p.name or p.id) }
    end
  end
  for i, e in ipairs(order) do
    local pid = ensure(e.id, e.name)
    if pid then
      if i == 1 then gain(pid, TUNE.sDerbyChamp)
      elseif i == 2 then gain(pid, TUNE.sDerbyRank2)
      elseif i == 3 then gain(pid, TUNE.sDerbyRank3)
      else gain(pid, TUNE.sDerbyRest) end
    end
  end
end

-- 捉迷藏：
--   搜索方胜 —— 每名搜索者得分；被找到的人此时已经被改判成搜索者，
--              所以「roles 里仍是 hider 的」正好是没被找到的（不存在）。
--   躲藏方胜 —— 每个「撑到最后仍是 hider」的人得分，逃脱者额外。
local function settleHideSeek(ctx)
  local winner = tostring(ctx.winner or '')
  local roles = ctx.roles or {}
  local escapedBy = tostring(ctx.stats and ctx.stats.escapedBy or '')
  for _, p in ipairs(ctx.roster or {}) do
    local pid = ensure(p.id, p.name)
    if pid then
      local role = tostring(roles[pid] or roles[p.id] or '')
      if winner == 'seeker' then
        if role == 'seeker' then gain(pid, TUNE.sSeekerWin) end
      elseif winner == 'hider' then
        if role == 'hider' then gain(pid, TUNE.sHiderLast) end
        if escapedBy ~= '' and pid == escapedBy then gain(pid, TUNE.sEscaper) end
      end
    end
  end
end

-- ---------------------------------------------------------------------------
-- 排序榜（前 BOARD_SIZE 名，含本机标记）
-- ---------------------------------------------------------------------------
local function rebuildBoard()
  local me = S.myId
  local list = {}
  for pid, total in pairs(S.totals) do
    list[#list + 1] = {
      id = pid,
      name = S.names[pid] or pid,
      score = total,
      delta = S.gains[pid] or 0,
      me = (pid == me),
    }
  end
  table.sort(list, function(a, b)
    if a.score ~= b.score then return a.score > b.score end
    if a.id ~= b.id then return a.id < b.id end
    return false
  end)
  local out = {}
  for i = 1, math.min(#list, BOARD_SIZE) do out[i] = list[i] end
  S.board = out
  S.myTotal = S.totals[me] or 0
  S.myGain = S.gains[me] or 0
end

-- ---------------------------------------------------------------------------
-- 对外：结算一局（幂等）
--
-- ctx = {
--   mode = 'cops_robber'|'derby'|'hide_seek',
--   round = 回合号, selfId = 本机 id, winner = 胜负, reason/finishReason,
--   roster = { {id, name}, ... }, roles = { [id] = role },
--   stats = { captureBy, derby = { {id,name,...}, ... }, escapedBy, ... },
-- }
-- ---------------------------------------------------------------------------
function M.settle(ctx)
  if type(ctx) ~= 'table' then return false end
  if not S.loaded then pcall(readConfig) end
  if TUNE.scoreEnabled <= 0 then return false end

  local mode = tostring(ctx.mode or '')
  if mode ~= 'cops_robber' and mode ~= 'derby' and mode ~= 'hide_seek' then return false end

  local round = tonumber(ctx.round) or 0
  -- ⚠️ 幂等键必须带上「本局的身份」，只靠 mode:round 会漏计：
  --    玩法模块在 gameMode 变化时会把 S.round 归零，所以「玩 A → 换 B → 换回 A」
  --    之后 A 的第 1 局又会算出同样的 mode:round，命中上一局的 key 直接跳过，
  --    整局白打。phaseAt 是本机进入 finished 的时刻（同局每帧都一样、跨局必然不同），
  --    拿它当身份最稳。拿不到时（旧桩件）退回旧键，行为不变。
  local key = mode .. ':' .. tostring(round)
  local pat = tonumber(ctx.phaseAt)
  if pat then key = key .. '@' .. string.format('%.3f', pat) end
  if key == S.roundKey then return false end

  local roster = ctx.roster
  if type(roster) ~= 'table' or #roster == 0 then return false end

  S.mode = mode
  S.round = round
  S.roundKey = key
  S.lastRound = round
  S.myId = tostring(ctx.selfId or S.myId or 'self')
  -- ⚠️ 必须先清本局得分表：gain() 是累加写的，不清就会把上一局的
  --    「本局 +N」带进这一局（榜单 delta 和结算卡都会显示错的分）。
  S.gains = {}

  if mode == 'derby' then
    settleDerby(ctx)
  elseif mode == 'hide_seek' then
    settleHideSeek(ctx)
  else
    settleCopsRobber(ctx)
  end

  -- 逐人封顶 + 落账
  for pid, g in pairs(S.gains) do
    local v = clampGain(g)
    S.gains[pid] = v
    S.totals[pid] = (S.totals[pid] or 0) + v
  end

  rebuildBoard()
  logScore(mode, '第 ' .. tostring(round) .. ' 局结算完成，本机 +' .. tostring(S.myGain)
    .. '，累计 ' .. tostring(S.myTotal))
  return true
end

-- ---------------------------------------------------------------------------
-- 对外：清账（会话结束 / 换房间 / 测试复位）
-- ---------------------------------------------------------------------------
function M.reset()
  S.mode = ''
  S.round = -1
  S.roundKey = ''
  S.totals = {}
  S.names = {}
  S.gains = {}
  S.board = {}
  S.myTotal = 0
  S.myGain = 0
  S.lastRound = 0
end

-- ---------------------------------------------------------------------------
-- 对外：快照（HUD 直接吃这一份）
-- ---------------------------------------------------------------------------
function M.snapshot()
  return {
    enabled   = TUNE.scoreEnabled > 0,
    total     = S.myTotal,
    gain      = S.myGain,
    round     = S.lastRound,
    board     = S.board,
    roundKey  = S.roundKey,
  }
end

function M.totalOf(pid)
  return S.totals[tostring(pid or '')] or 0
end

function M.gainOf(pid)
  return S.gains[tostring(pid or '')] or 0
end

function M.tune() return TUNE end

return M
