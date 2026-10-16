-- ============================================================================
-- StartRide 玩法阶段机
-- ============================================================================
-- 负责「警匪追逐 / 德比」这两个正式玩法的**规则主体**：
--   阶段推进、角色分配、抓捕判定、淘汰判定、结算排序。
--
-- 六个阶段（照 BeamLink 的划分，但由我们自己的桥驱动）：
--
--   lobby      房间刚建 / 玩法没开始，自由活动
--   staging    全员就位中（车已经被摆到出生位，但还没发车）
--   countdown  321 倒数
--   running    正式比赛
--   finished   结算
--
-- ⚠️ 坑（关键）：staging 与 countdown **都算 roundActive**。
--    如果只在 running 才锁输入，玩家可以在倒数期间按复位/传送直接跳出发车位。
--    所以 inputPolicy() 里 `roundActive = phase ~= 'lobby' and phase ~= 'finished'`。
--
-- 权威性：**房主（host）说了算**。房主本地推阶段并把 `mode-state` 广播出去；
-- 加入者只接受广播、不自己推阶段（否则两边倒计时不同步）。
-- 房主判定 = roomInfo.host 与自己的昵称/id 相等；判不出来时退化为「谁都别推」，
-- 宁可玩法不启动，也不要两端各自跑一套。
--
-- 上值控制：所有可调参数收进 TUNE，角色/阶段常量收进 PHASE / ROLE。
-- ============================================================================

local M = {}

-- ---------------------------------------------------------------------------
-- 可调参数（由启动器写入 startride/multiplayer.json，readConfig 读取）
-- ---------------------------------------------------------------------------
local TUNE = {
  countdownSeconds   = 5,      -- 倒数秒数
  stagingSeconds     = 8,      -- 就位等待上限（超时就开）
  captureHoldMs      = 5000,   -- 抓捕需要贴住多久（毫秒）
  captureStillSpeed  = 8.0,    -- 强盗「近乎静止」阈值（米/秒，约 29 km/h）
  derbyDamageLimit   = 8000,   -- 德比淘汰伤害阈值
  resetLimit         = 3,      -- 复位次数预算
  resetCooldownMs    = 10000,  -- 复位冷却
  finishHoldSeconds  = 6,      -- 结算画面停留（然后回 lobby）
  placementRetrySeconds = 5,   -- 就位超时退回 lobby 后的重试间隔（防死循环）
  minPlayers         = 2,      -- 少于此人数不启动玩法
  minCops            = 1,      -- 至少几个警察
  copRatio           = 0.4,    -- 警察占比（向上取整，但不超过 半数）
  copVehicleHint     = '',     -- 警察推荐车型（空 = 不限）
  robberVehicleHint  = '',     -- 强盗推荐车型
  roundSeconds       = 300,
  impactRadius       = 3.4,
  impactMinSpeed     = 4.0,
  ramDamageFactor    = 0.9,
  ramSelfShare       = 0.5,
  ramBonus           = 120,
  impactCooldownMs   = 700,
  copNavEnabled      = 1,
  copNavIntervalMs   = 1000,
  copNavMinMoveM     = 15,
  reportIntervalMs   = 1000,
}

local PHASE = {
  lobby     = 'lobby',
  staging   = 'staging',
  countdown = 'countdown',
  running   = 'running',
  finished  = 'finished',
}

local ROLE = {
  cop    = 'cop',
  robber = 'robber',
  none   = 'none',
}

-- 局内阶段（这几个阶段要锁按键）
local ROUND_PHASES = {
  [PHASE.staging]   = true,
  [PHASE.countdown] = true,
  [PHASE.running]   = true,
}

-- ---------------------------------------------------------------------------
-- 运行状态
-- ---------------------------------------------------------------------------
local S = {
  mode     = 'free_drive',   -- 当前玩法 id
  phase    = PHASE.lobby,
  round    = 0,              -- 回合序号（每开一局 +1）
  selfRole = ROLE.none,
  host     = false,          -- 本机是不是房主（权威端）
  authority = false,         -- 本机有没有推阶段的权力

  -- 计时
  phaseAt     = 0,           -- 进入当前阶段的时间（timeAccum 口径）
  clock       = 0,           -- 本地累计时间（tick 自增）
  countdownFrom = 0,         -- 倒数起始秒数

  -- 警匪
  captureSince = nil,        -- 当前玩家（强盗）被贴住的起点
  captureBy    = '',         -- 贴住他的警察 id
  captureDone  = false,      -- 本局是否已经抓到
  winner       = '',         -- 'cop' / 'robber' / ''

  -- 德比
  eliminated   = {},         -- [playerId] = true
  damage       = {},         -- [playerId] = 数值

  -- 复位预算
  resetUsed    = 0,
  resetFreeAt  = 0,

  -- 广播去重
  lastSentKey  = '',

  -- 位置缓存 [playerId] = {x, y, at}
  positions    = {},

  -- 远端出生方案 / 远端阶段
  pendingSpawnPlan = nil,
  lastPlanRound    = -1,

  -- 就位汇报：[playerId] = true（本机 + 远端广播回来的）
  placementReady   = {},
  placementOrder   = {},     -- 参与本回合的名单（算 totalCount 用）
  retryBlockedUntil = 0,     -- 就位超时后的重试冷却截止时刻

  -- 公告去重
  lastAnnounce = '',

  -- 配置是否已加载
  cfgLoaded    = false,

  finishReason     = '',
  ramDamage        = {},
  nativeDamage     = 0,
  impacts          = {},
  lastImpactAt     = {},
  hitFlash         = 0,
  hitNotice        = nil,
  pendingImpact    = nil,
  reportAt         = 0,
  lastReport       = -1,
  lastSyncKey      = '',
  forceSync        = false,
  roleSig          = '',
  remoteCaptureMs  = 0,
  remoteCaptureBy  = '',
  remoteRoundLeft  = nil,
  remoteRoundLeftAt = 0,
  remoteSurvivors  = nil,
  navAt            = 0,
  navPos           = nil,
  navOn            = false,
  goneAt           = {},
  leftIds          = {},
  leftElim         = {},
}

-- ---------------------------------------------------------------------------
-- 工具
-- ---------------------------------------------------------------------------
local function logMode(...)
  local parts = { '[StartRide] 玩法：' }
  for _, v in ipairs({ ... }) do parts[#parts + 1] = tostring(v) end
  pcall(function() log('I', 'startride', table.concat(parts, ' ')) end)
end

local function now(cx)
  return S.clock
end

local function clamp(v, lo, hi)
  if v < lo then return lo end
  if v > hi then return hi end
  return v
end

-- ---------------------------------------------------------------------------
-- 配置读取（启动器写进 startride/multiplayer.json）
-- ---------------------------------------------------------------------------
local function readConfig()
  local cfg = nil
  pcall(function() cfg = jsonReadFile('startride/multiplayer.json') end)
  if type(cfg) ~= 'table' then
    pcall(function() cfg = jsonReadFile('current/startride/multiplayer.json') end)
  end
  S.cfgLoaded = true
  if type(cfg) ~= 'table' then return false end

  local rules = cfg.modeRules
  if type(rules) ~= 'table' then rules = cfg end   -- 也接受平铺写法

  local function num(key, lo, hi, dst)
    local v = tonumber(rules[key])
    if v and v >= lo and v <= hi then TUNE[dst or key] = v end
  end

  num('countdownSeconds', 0, 30)
  num('stagingSeconds', 2, 60)
  num('captureHoldMs', 500, 60000)
  num('captureStillSpeed', 0, 60)
  num('derbyDamageLimit', 100, 1000000)
  num('resetLimit', 0, 99)
  num('resetCooldownMs', 0, 600000)
  num('finishHoldSeconds', 1, 60)
  num('placementRetrySeconds', 1, 60)
  num('roundSeconds', 0, 7200)
  num('impactRadius', 0.5, 30)
  num('impactMinSpeed', 0, 80)
  num('ramDamageFactor', 0, 100)
  num('ramSelfShare', 0, 1)
  num('ramBonus', 0, 100000)
  num('impactCooldownMs', 0, 10000)
  num('copNavEnabled', 0, 1)
  num('copNavIntervalMs', 100, 10000)
  num('copNavMinMoveM', 0, 500)
  num('reportIntervalMs', 100, 10000)

  if type(rules.copVehicleHint) == 'string' then TUNE.copVehicleHint = rules.copVehicleHint end
  if type(rules.robberVehicleHint) == 'string' then TUNE.robberVehicleHint = rules.robberVehicleHint end
  if tonumber(rules.minPlayers) then
    TUNE.minPlayers = clamp(math.floor(tonumber(rules.minPlayers)), 1, 32)
  end

  return true
end

-- ---------------------------------------------------------------------------
-- 房主判定
-- ---------------------------------------------------------------------------
local function resolveAuthority(win)
  -- 没连上 / 没有房间 / 房间已关 / 中继已断 → 谁都没有权威，玩法一律不启动。
  -- ⚠️ 同时维护 sessionAlive：玩法状态（phase）必须在会话消失时复位，
  --    因为输入限制只认 phase —— 不复位就等于把传送/复位/换车永久锁死。
  --    'disconnected' 是启动器推来的中继状态；'unknown' 是还没收到，不当断线。
  if not win.connected or win.closed or win.relayState == 'disconnected' then
    S.authority = false
    S.sessionAlive = false
    return
  end
  S.sessionAlive = true
  local h = tostring(win.host or '')
  if h == '' then
    -- 房间还没有房主信息：不猜。等 players/host 广播到了再说。
    S.authority = false
    return
  end
  local selfId = tostring(win.selfId or '')
  local selfName = tostring(win.playerName or '')
  S.authority = (h == selfId) or (h == selfName)
end

-- ---------------------------------------------------------------------------
-- 阶段切换
-- ---------------------------------------------------------------------------
local function setPhase(ph, announce)
  if S.phase == ph then return end
  S.phase = ph
  S.phaseAt = S.clock

  if ph == PHASE.staging then
    S.round = S.round + 1
    S.captureSince = nil
    S.captureBy = ''
    S.captureDone = false
    S.winner = ''
    S.eliminated = {}
    S.damage = {}
    S.resetUsed = 0
    -- ⚠️ 复位**冷却**也要跟着局归零：resetUsed 清了而 resetFreeAt 没清的话，
    --    上一局末尾按过复位的人，新局刚开跑按复位就被判「冷却中」。
    S.resetFreeAt = 0
    S.finishReason = ''
    S.ramDamage = {}
    S.nativeDamage = 0
    S.impacts = {}
    S.lastImpactAt = {}
    S.hitFlash = 0
    S.hitNotice = nil
    S.pendingImpact = nil
    S.lastReport = -1
    S.lastSyncKey = ''
    S.forceSync = true
    S.remoteCaptureMs = 0
    S.remoteCaptureBy = ''
    S.remoteRoundLeft = nil
    S.remoteSurvivors = nil
    S.navAt = 0
    S.navPos = nil
    S.navOn = false
    -- 每回合重置就位汇报，并把本回合名单固化下来（算 totalCount 用）
    S.placementReady = {}
    local order = {}
    for _, p in ipairs(S.roster or {}) do
      if type(p) == 'table' and p.id ~= nil then order[#order + 1] = tostring(p.id) end
    end
    S.placementOrder = order
    S.goneAt = {}
    S.leftIds = {}
    S.leftElim = {}
  elseif ph == PHASE.countdown then
    S.countdownFrom = TUNE.countdownSeconds
  elseif ph == PHASE.running then
    S.captureSince = nil
  end

  if announce ~= false then
    S.lastAnnounce = ''
  end
  logMode('阶段 →', ph, '回合', S.round)
end

-- ---------------------------------------------------------------------------
-- 角色分配
--
-- 规则：房主当强盗（或者房主指定的第一个人当强盗），其余按名单顺序轮流当警察。
-- 警察数量 = ceil(人数 * copRatio)，最少 minCops，最多 人数-1（强盗至少一个）。
-- ---------------------------------------------------------------------------
local function assignRoles()
  local ids = { mySelf = true }
  local list = {}

  -- 自己
  list[#list + 1] = { id = S.selfId or 'self', name = S.playerName or '' }

  -- 其他玩家（mpPlayers 由启动器广播）
  local pl = S.lastPlayers
  if type(pl) == 'table' then
    for _, p in ipairs(pl) do
      local pid = tostring(p.id or p.name or '')
      if pid ~= '' and pid ~= tostring(S.selfId or '') then
        list[#list + 1] = { id = pid, name = tostring(p.name or pid) }
      end
    end
  end

  if #list < TUNE.minPlayers then
    return false, '人数不足（需要至少 ' .. tostring(TUNE.minPlayers) .. ' 人）'
  end

  -- 稳定排序，保证两端算出来的角色一致
  table.sort(list, function(a, b) return tostring(a.id) < tostring(b.id) end)

  local copCount = math.ceil(#list * TUNE.copRatio)
  if copCount < TUNE.minCops then copCount = TUNE.minCops end
  if copCount > #list - 1 then copCount = #list - 1 end

  S.roles = {}
  for i, p in ipairs(list) do
    -- 第一个是强盗，其余前 copCount 个是警察
    S.roles[p.id] = (i == 1) and ROLE.robber or ((i - 1) <= copCount and ROLE.cop or ROLE.robber)
  end
  -- 保险：强盗只能有一个
  local robberAssigned = false
  for i, p in ipairs(list) do
    if S.roles[p.id] == ROLE.robber then
      if robberAssigned then S.roles[p.id] = ROLE.cop end
      robberAssigned = true
    end
  end

  S.selfRole = S.roles[S.selfId or 'self'] or ROLE.robber
  S.roster = list
  local sig = {}
  for pid, r in pairs(S.roles) do sig[#sig + 1] = tostring(pid) .. '=' .. tostring(r) end
  table.sort(sig)
  S.roleSig = table.concat(sig, ',')
  return true
end

-- ---------------------------------------------------------------------------
-- 警匪：抓捕判定
--
-- 只有「警察贴住强盗」且**双方都在 running** 才算数。
-- 判定距离用车辆位置，XY 平面距离（Z 可能因为悬挂/地形差很多）。
--
-- 参考 BeamLink 的规则（见其 HUD 文案「逃犯近乎静止时，靠近并保持 5 秒」）：
-- 抓捕需要 **距离足够近** 且 **强盗近乎静止** 两个条件同时成立。
-- 只看距离会导致「强盗高速路过警察 8 米内，进度莫名上涨」——那不是抓捕。
-- ---------------------------------------------------------------------------
local CAPTURE_RADIUS = 8.0    -- 贴住判定半径（米）

local function vehiclePos(id)
  local x, y = nil, nil
  pcall(function()
    if id == S.selfId or id == 'self' then
      local v = be:getPlayerVehicle(0)
      if v then local p = v:getPosition(); x, y = p.x, p.y end
    end
  end)
  return x, y
end

-- 自己车辆的水平速度（米/秒）。取不到时返回 nil = 「未知」，
-- 此时按「未知不阻断」处理（宁可漏判一次，也不要因为 API 抖动把抓捕卡死）。
local function selfSpeed()
  local sp = nil
  pcall(function()
    local v = be:getPlayerVehicle(0)
    if v and v.getVelocity then
      local vel = v:getVelocity()
      if vel then
        local vx, vy = tonumber(vel.x) or 0, tonumber(vel.y) or 0
        sp = math.sqrt(vx * vx + vy * vy)
      end
    end
  end)
  return sp
end

-- 强盗是不是「近乎静止」（引擎里有没有手刹/停住）。
-- 只有本地是强盗时才有意义 —— 远端车我们拿不到它的速度。
local function robberIsStill()
  local sp = selfSpeed()
  if sp == nil then return true end          -- 拿不到速度 → 不阻断
  return sp <= (tonumber(TUNE.captureStillSpeed) or 8.0)
end

local function dist2(ax, ay, bx, by)
  local dx, dy = ax - bx, ay - by
  return math.sqrt(dx * dx + dy * dy)
end

local function notePos(pid, x, y)
  pid = tostring(pid or '')
  if pid == '' then return end
  local old = S.positions[pid]
  local rec = { x = x, y = y, at = S.clock }
  if old then
    rec.px, rec.py, rec.pat = old.x, old.y, old.at
  end
  S.positions[pid] = rec
end

-- ---------------------------------------------------------------------------
-- 位置表维护
--
-- 车辆位置走的是既有的 'vehicle' 包（每 1/30 秒一条，字段 pos={x,y,z}），
-- 不需要为玩法新开一个位置通道 —— 那会让包量翻倍。
-- 这里只把位置缓存下来，供抓捕判定和警察雷达用。
--
-- 自己的位置每帧从引擎现取（不依赖网络回包）。
-- ---------------------------------------------------------------------------
function M.notePositions(data)
  if type(data) ~= 'table' then return end
  local pid = tostring(data.id or '')
  if pid == '' or type(data.pos) ~= 'table' then return end
  local x, y = tonumber(data.pos[1]), tonumber(data.pos[2])
  if not x or not y then return end

  notePos(pid, x, y)
end

local function tickPositions()
  -- 自己的位置
  local sx, sy = vehiclePos('self')
  if sx and sy then notePos(S.selfId or 'self', sx, sy) end

  -- 清掉超过 3 秒没更新的（人走了）
  for pid, rec in pairs(S.positions) do
    if (S.clock - (rec.at or 0)) > 3 then S.positions[pid] = nil end
  end
end

-- ---------------------------------------------------------------------------
-- 玩家离开检测
--
-- 名单里的人在本回合进行中不在了 = 掉线 / 退房。以前完全不处理，后果是：
--   · 德比：他永远算「还活着」→ survivorCount 永远 >= 2 → 回合永远不结束
--   · 警匪：强盗走了没人可抓 → 干等到时限，还判强盗胜
--   · 躲猫猫：躲藏者走了永远算「还没被找到」→ 搜索方永远赢不了
-- 这里给一段宽限期（挡住名单抖动），超时才按「本回合出局」处理；人回来能恢复。
-- ---------------------------------------------------------------------------
local GONE_GRACE_SECONDS = 5

local function liveIdSet(win)
  local pl = win and win.players
  if type(pl) ~= 'table' then return nil end
  local live, n = {}, 0
  for _, p in ipairs(pl) do
    local id = nil
    if type(p) == 'table' then
      id = tostring(p.id or p.name or '')
    elseif type(p) == 'string' or type(p) == 'number' then
      id = tostring(p)
    end
    if id and id ~= '' then
      live[id] = true
      n = n + 1
    end
  end
  if n == 0 then return nil end
  return live
end

local function trackPresence(win)
  local me = tostring(S.selfId or 'self')
  if S.phase == PHASE.lobby then
    S.goneAt = {}
    S.leftIds = {}
    S.leftElim = {}
    return
  end

  local live = liveIdSet(win)
  if not live then return end

  local roster = S.roster or {}
  local liveRoster = 0
  for _, p in ipairs(roster) do
    if live[tostring(p.id)] then liveRoster = liveRoster + 1 end
  end
  if liveRoster <= 0 then return end

  local now = S.clock
  local marks = S.goneAt or {}
  S.goneAt = marks
  local gone = nil

  for _, p in ipairs(roster) do
    local pid = tostring(p.id)
    if pid ~= '' and pid ~= me then
      if live[pid] then
        marks[pid] = nil
        if S.leftElim[pid] then
          S.leftElim[pid] = nil
          S.leftIds[pid] = nil
          if (tonumber(S.damage[pid]) or 0) < (tonumber(TUNE.derbyDamageLimit) or 8000) then
            S.eliminated[pid] = nil
          end
          logMode('玩家回来了，恢复参战:', pid)
        end
      elseif not marks[pid] then
        marks[pid] = now
      elseif (now - marks[pid]) >= GONE_GRACE_SECONDS then
        gone = gone or {}
        gone[#gone + 1] = pid
      end
    end
  end

  if not gone then return end

  for _, pid in ipairs(gone) do
    if not S.leftIds[pid] then
      S.leftIds[pid] = true
      S.leftElim[pid] = true
      S.eliminated[pid] = true
      S.placementReady[pid] = true
      logMode('玩家已离开，本回合按出局处理:', pid)
      if S.mode == 'cops_robber' and S.roles[pid] == ROLE.robber
         and S.phase == PHASE.running and not S.captureDone then
        S.captureDone = true
        S.winner = ROLE.cop
        S.finishReason = 'robber_left'
        setPhase(PHASE.finished, true)
        logMode('强盗已离开 —— 警察胜')
      end
    end
  end
end

-- 取某个角色的现场位置（返回 x, y）
local function rolePos(pid)
  local rec = S.positions[pid]
  if rec then return rec.x, rec.y end
  return nil, nil
end

local function posVel(rec)
  if not rec or not rec.px or not rec.pat then return 0, 0 end
  local dt = (rec.at or 0) - (rec.pat or 0)
  if dt <= 0.02 then return 0, 0 end
  return ((rec.x or 0) - (rec.px or 0)) / dt, ((rec.y or 0) - (rec.py or 0)) / dt
end

local function roleNameOf(pid)
  local want = tostring(pid or '')
  for _, p in ipairs(S.roster or {}) do
    if tostring(p.id) == want then return tostring(p.name or p.id) end
  end
  return want
end

local function damageOf(pid)
  if tostring(pid) == tostring(S.selfId or 'self') then
    return (tonumber(S.nativeDamage) or 0) + (tonumber(S.ramDamage[S.selfId or 'self']) or 0)
  end
  return tonumber(S.damage[pid]) or 0
end
M.damageOf = damageOf

local function survivorCount()
  local n = 0
  for _, p in ipairs(S.roster or {}) do
    if not S.eliminated[p.id] then n = n + 1 end
  end
  return n
end
M.survivors = survivorCount

local function tickCapture(dtReal)
  if S.mode ~= 'cops_robber' then return end
  if S.phase ~= PHASE.running then return end
  if S.captureDone then return end

  -- 只有「自己是强盗」时，本地才知道自己有没有被贴住。
  -- 警察端的抓捕进度靠房主广播的 captureMs 同步（见 applyRemoteState）。
  if S.selfRole ~= ROLE.robber then
    if S.captureSince then S.captureSince = nil end
    return
  end

  -- 找最近的警察
  local best, bestD = nil, nil
  local selfX, selfY = vehiclePos('self')
  if not selfX then return end

  -- 位置优先用缓存的实时位置（远端车每 1/30 秒推一次）
  for pid in pairs(S.roles or {}) do
    if S.roles[pid] == ROLE.cop then
      local px, py = rolePos(pid)
      if px and py then
        local d = dist2(selfX, selfY, px, py)
        if not bestD or d < bestD then best, bestD = pid, d end
      end
    end
  end

  if best and bestD and bestD <= CAPTURE_RADIUS and robberIsStill() then
    if not S.captureSince then
      S.captureSince = S.clock
      S.captureBy = best
    elseif best ~= S.captureBy then
      -- 换了个警察贴：重新计时（避免两个人来回蹭白刷进度）
      S.captureSince = S.clock
      S.captureBy = best
    end

    local heldMs = (S.clock - S.captureSince) * 1000
    if heldMs >= TUNE.captureHoldMs then
      S.captureDone = true
      S.winner = ROLE.cop
      if S.authority then setPhase(PHASE.finished, true) else S.phase = PHASE.finished end
      logMode('强盗被抓，警察胜（贴住 ' .. string.format('%.1f', heldMs / 1000) .. ' 秒）')
    end
  else
    if S.captureSince then
      -- 断开了（或者强盗重新跑起来）：进度清零。BeamLink 这里是「衰减」，但那会导致
      -- 两个玩家在边缘来回蹭就能慢慢刷满 → 改成直接清零，逼警察真的把车逼停。
      S.captureSince = nil
      S.captureBy = ''
    end
  end
end

-- ---------------------------------------------------------------------------
-- 德比：伤害与淘汰
-- ---------------------------------------------------------------------------
local function tickDerby()
  if S.mode ~= 'derby' then return end
  if S.phase ~= PHASE.running then return end

  local me = tostring(S.selfId or 'self')
  local dmg = nil
  pcall(function()
    local v = be:getPlayerVehicle(0)
    if v and v.getDamage then dmg = tonumber(v:getDamage()) end
  end)
  if dmg then S.nativeDamage = dmg end

  local total = damageOf(me)
  S.damage[me] = total
  if not S.eliminated[me] and total >= (tonumber(TUNE.derbyDamageLimit) or 8000) then
    S.eliminated[me] = true
    S.hitNotice = { self = true, name = '', speed = 0, wrecked = true }
    logMode('本机已被淘汰（车损 ' .. string.format('%.0f', total) .. '）')
  end

  if S.authority then
    local alive = survivorCount()
    if alive <= 1 and #(S.roster or {}) >= 2 then
      S.winner = ''
      for _, p in ipairs(S.roster or {}) do
        if not S.eliminated[p.id] then S.winner = tostring(p.id) end
      end
      S.finishReason = 'last_standing'
      setPhase(PHASE.finished, true)
      logMode('德比结束，胜者', S.winner)
    end
  end
end

local function roundTimeLeft()
  local total = tonumber(TUNE.roundSeconds) or 0
  if total <= 0 then return nil end
  if S.phase ~= PHASE.running then return nil end
  local left = total - (S.clock - S.phaseAt)
  if left < 0 then left = 0 end
  return left
end
M.roundTimeLeft = roundTimeLeft

local function viewRoundLeft()
  if S.authority then
    return roundTimeLeft()
  end
  if S.phase ~= PHASE.running then return nil end
  if not S.remoteRoundLeft then return nil end
  local l = S.remoteRoundLeft - (S.clock - (S.remoteRoundLeftAt or 0))
  if l < 0 then l = 0 end
  return l
end
M.viewRoundLeft = viewRoundLeft

local function tickImpacts()
  if S.mode ~= 'derby' and S.mode ~= 'cops_robber' then return end
  if S.phase ~= PHASE.running then return end
  local me = tostring(S.selfId or 'self')

  -- ⚠️ 已淘汰（车报废）的客户端不再产生撞击包。
  --    伤害归属是「谁在算」不是「谁在撞」：车一旦被冻住，谁撞谁在运动学上
  --    分不出来，原来这份客户端会照常算出 closing speed 并把伤害记到**对手**
  --    头上。结果就是「主动撞残骸的人自己掉血」，而淘汰者因为不怕掉血，
  --    反而成了场上最危险的攻击者 —— 跟 survivorCount 不算他完全自相矛盾。
  if S.mode == 'derby' and S.eliminated[me] == true then return end

  local rec = S.positions[me]
  if not rec or type(S.roles) ~= 'table' then return end
  local mvx, mvy = posVel(rec)
  local reach = (tonumber(TUNE.impactRadius) or 3.4) * 2
  local minSpeed = tonumber(TUNE.impactMinSpeed) or 4.0
  local cool = (tonumber(TUNE.impactCooldownMs) or 700) / 1000
  for pid, other in pairs(S.positions) do
    if tostring(pid) ~= me and S.roles[pid] then
      local dx = (other.x or 0) - (rec.x or 0)
      local dy = (other.y or 0) - (rec.y or 0)
      local d = math.sqrt(dx * dx + dy * dy)
      if d > 0.01 and d <= reach then
        local ovx, ovy = posVel(other)
        local nx, ny = dx / d, dy / d
        local closing = math.abs((mvx - ovx) * nx + (mvy - ovy) * ny)
        local last = S.lastImpactAt[pid]
        if closing >= minSpeed and (not last or (S.clock - last) >= cool) then
          S.lastImpactAt[pid] = S.clock
          S.impacts[pid] = (S.impacts[pid] or 0) + 1
          S.hitFlash = 2.5
          S.hitNotice = { self = false, name = roleNameOf(pid), speed = closing }
          if S.mode == 'derby' then
            local dmg = closing * closing * (tonumber(TUNE.ramDamageFactor) or 0.9)
              + (tonumber(TUNE.ramBonus) or 0)
            S.ramDamage[me] = (S.ramDamage[me] or 0) + dmg * (tonumber(TUNE.ramSelfShare) or 0.5)
            S.pendingImpact = { target = tostring(pid), damage = dmg, speed = closing }
          end
        end
      end
    end
  end
end

local function tickNavigation()
  local want = (S.mode == 'cops_robber') and (S.selfRole == ROLE.cop)
    and (S.phase == PHASE.running) and ((tonumber(TUNE.copNavEnabled) or 1) > 0)
  if not want then
    if S.navOn then
      S.navOn = false
      S.navPos = nil
      pcall(function() core_groundMarkers.setPath(nil) end)
    end
    return
  end
  local interval = (tonumber(TUNE.copNavIntervalMs) or 1000) / 1000
  if (S.clock - (S.navAt or 0)) < interval then return end
  local tx, ty = nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.robber then
      local px, py = rolePos(pid)
      if px then tx, ty = px, py end
    end
  end
  if not tx then return end
  S.navAt = S.clock
  if S.navPos then
    local ndx, ndy = tx - S.navPos[1], ty - S.navPos[2]
    if math.sqrt(ndx * ndx + ndy * ndy) < (tonumber(TUNE.copNavMinMoveM) or 15) then return end
  end
  S.navPos = { tx, ty }
  local ok = pcall(function()
    local z = 0
    if be and be.getSurfaceHeightBelow then
      local h = be:getSurfaceHeightBelow(vec3(tx, ty, 0))
      if tonumber(h) then z = tonumber(h) end
    end
    core_groundMarkers.setPath({ { tx, ty, z } }, { clearPathOnReachingTarget = false })
  end)
  S.navOn = ok == true
end

-- ---------------------------------------------------------------------------
-- 阶段推进（只有权威端跑）
-- ---------------------------------------------------------------------------
local function tickAuthority(win)
  if not S.authority then return end
  if S.mode ~= 'cops_robber' and S.mode ~= 'derby' then return end

  local elapsed = S.clock - S.phaseAt

  if S.phase == PHASE.lobby then
    -- 玩法已经选定、且人数够了 → 进就位
    -- ⚠️ 但要尊重「就位超时」的退避冷却，否则会 lobby↔staging 死循环重试。
    if S.retryBlockedUntil and S.clock < S.retryBlockedUntil then
      return
    end
    if S.mode == 'cops_robber' or S.mode == 'derby' then
      local ok, why = assignRoles()
      if ok then
        setPhase(PHASE.staging, true)
        logMode('开始就位（回合 ' .. tostring(S.round) .. '）')
      else
        S.lastBlockReason = why
      end
    end

  elseif S.phase == PHASE.staging then
    -- 就位判定（对齐 BeamLink：等**所有人**汇报 placementReady 再开，超时才算失败）
    --
    -- 原来这里是「8 秒一到就开」——地图还没加载完 / 有人还在传送时
    -- 直接把回合拉起来，结果全员站在上一局的位置上开跑。现在改成：
    --   ① 全员到位 → 立刻进倒数（不必干等满 8 秒）
    --   ② 超时但**至少自己到位了** → 也开（不能因为一个掉线的人卡死全场）
    --   ③ 超时且自己都没到位 → 记 placement_timeout 并退回 lobby（别开着烂局）
    local readyCount, totalCount = 0, 0
    for _, pid in ipairs(S.placementOrder or {}) do
      totalCount = totalCount + 1
      if S.placementReady[pid] then readyCount = readyCount + 1 end
    end
    local selfReady = S.placementReady[S.selfId or 'self'] == true

    if totalCount > 0 and readyCount >= totalCount then
      setPhase(PHASE.countdown, true)
    elseif elapsed >= TUNE.stagingSeconds then
      if selfReady then
        logMode(string.format('就位超时，但本机已到位（%d/%d）→ 照常开',
          readyCount, totalCount))
        setPhase(PHASE.countdown, true)
      else
        S.lastBlockReason = 'placement_timeout'
        logMode(string.format('就位超时且本机未到位（%d/%d）→ 退回准备',
          readyCount, totalCount))
        -- ⚠️ 必须记冷却：退回 lobby 后，下一帧 lobby 分支会立刻
        --    assignRoles() 成功 → 又进 staging → 8 秒后再退回……
        --    变成每 8 秒一次的**死循环重试**（玩家看到界面疯狂闪）。
        --    给一个重试间隔，让玩家有机会先上车。
        S.retryBlockedUntil = S.clock + TUNE.placementRetrySeconds
        setPhase(PHASE.lobby, true)
      end
    end

  elseif S.phase == PHASE.countdown then
    if elapsed >= TUNE.countdownSeconds then
      setPhase(PHASE.running, true)
    end

  elseif S.phase == PHASE.running then
    -- running → finished 由 capture / derby 判定触发
    if not S.captureDone then
      local total = tonumber(TUNE.roundSeconds) or 0
      if total > 0 and elapsed >= total then
        if S.mode == 'cops_robber' then
          S.captureDone = true
          S.winner = ROLE.robber
          S.finishReason = 'time_up'
          setPhase(PHASE.finished, true)
          logMode('回合时限已到，强盗撑满全场')
        else
          local best, bestDmg = '', nil
          for _, p in ipairs(S.roster or {}) do
            if not S.eliminated[p.id] then
              local d = damageOf(p.id)
              if not bestDmg or d < bestDmg then best, bestDmg = tostring(p.id), d end
            end
          end
          if best ~= '' then
            S.winner = best
            S.finishReason = 'time_up'
            setPhase(PHASE.finished, true)
            logMode('德比时限已到，胜者', best)
          end
        end
      end
    end

  elseif S.phase == PHASE.finished then
    if elapsed >= TUNE.finishHoldSeconds then
      setPhase(PHASE.lobby, true)
    end
  end
end

-- ---------------------------------------------------------------------------
-- 对外：每帧
-- ---------------------------------------------------------------------------
function M.tick(dtReal, win)
  if not S.cfgLoaded then pcall(readConfig) end

  S.clock = S.clock + (tonumber(dtReal) or 0.016)

  win = win or {}
  S.selfId     = tostring(win.selfId or 'self')
  S.playerName = tostring(win.playerName or '')
  S.lastPlayers = win.players
  S.mode       = tostring(win.gameMode or 'free_drive')

  resolveAuthority(win)

  -- 会话没了（退房 / 关房 / 中继断）→ 玩法状态整个收回 lobby。
  -- 不复位的话 phase 会停在 running，而输入限制只认 phase，
  -- 玩家回到自由驾驶后会连传送/复位/换车都用不了（见 resolveAuthority 注释）。
  if S.sessionAlive == false and S.phase ~= PHASE.lobby then
    S.phase = PHASE.lobby
    S.selfRole = ROLE.none
    S.round = 0
    S.captureDone = false
    S.placementReady = {}
    S.pendingSpawnPlan = nil
    S.goneAt = {}
    S.leftIds = {}
    S.leftElim = {}
    logMode('联机会话已结束 —— 玩法复位到大厅')
  end

  -- 位置缓存（抓捕判定 / 雷达用）
  pcall(tickPositions)

  -- 玩家离开：名单里的人不在了，超时按本回合出局处理
  pcall(trackPresence, win)

  -- 玩法切走 → 重新开始
  -- ⚠️ 但如果这一帧之前刚收到房主推来的阶段（remotePhaseFresh），就**不能**复位：
  --    否则加入者会把刚跟上的 running 打回 lobby，永远追不上房主。见 applyRemotePhase。
  if S.mode ~= S.lastMode then
    S.lastMode = S.mode
    if S.remotePhaseFresh then
      S.remotePhaseFresh = false      -- 只挡一次（这一帧的这次复位）
    else
      S.phase = PHASE.lobby
      S.selfRole = ROLE.none
      S.round = 0
    end
  end

  -- 房主信息变了 → 重新判权威
  tickAuthority(win)

  -- 判定
  pcall(tickCapture, dtReal)
  pcall(tickDerby)
  pcall(tickImpacts)
  pcall(tickNavigation)

  local flash = tonumber(S.hitFlash) or 0
  if flash > 0 then S.hitFlash = flash - (tonumber(dtReal) or 0.016) end

  -- 结算停留
  if S.phase == PHASE.finished and (S.clock - S.phaseAt) >= TUNE.finishHoldSeconds then
    if S.authority then setPhase(PHASE.lobby, true) end
  end
end

-- ---------------------------------------------------------------------------
-- 对外：给输入层的策略
-- ---------------------------------------------------------------------------
function M.inputPolicy()
  -- 会话没了就一律放行：这是最后一道保险。
  -- phase 因为任何原因卡在局内时，按键都不该继续锁着。
  if S.sessionAlive == false then
    return { roundActive = false, running = false, vehicleLock = false,
             lockReset = false, freezeDrive = false, reason = 'session-gone' }
  end
  local inRound = ROUND_PHASES[S.phase] and true or false
  local running = (S.phase == PHASE.running)

  -- 警匪：running 时连复位都锁（复位=传送逃脱）
  -- 德比：复位保留（翻车要能起来），但走预算 + 冷却
  local lockReset = (S.mode == 'cops_robber' and inRound)

  -- ⚠️ 德比淘汰 = 车已报废（HUD 已经提示「等待本局结束」），但阶段还停在 running。
  --    不额外冻住的话，被淘汰的人还能满油门把活着的人撞出局。
  --    注意这里只是**多加一条**冻油门的理由：roundActive 仍然为真，
  --    所以总闸 startrideInput.applyDriveFreeze 照常放行，冻得住。
  local wrecked = (S.mode == 'derby')
    and (S.eliminated[S.selfId or 'self'] == true)

  return {
    roundActive = inRound,
    running     = running,
    vehicleLock = inRound,
    lockReset   = lockReset,
    -- ⚠️ 就位 / 倒数期间一并冻住油门：staging 里车刚被摆到出生位，
    --    不冻就能立刻开走；countdown 里更是直接抢跑。
    --    running 一开自动解冻（总闸在 startrideInput.applyDriveFreeze）。
    freezeDrive = inRound and ((S.phase ~= PHASE.running) or wrecked),
    reason      = S.mode .. '/' .. S.phase .. (wrecked and '/wrecked' or ''),
  }
end

-- ---------------------------------------------------------------------------
-- 对外：复位预算（德比用；警匪直接锁死）
-- ---------------------------------------------------------------------------
-- ---------------------------------------------------------------------------
-- 德比：远端淘汰同步（补洞）
--
-- 原来的漏洞：S.eliminated 只会在**本机**那一格被写（tickDerby 里写 selfId），
-- 而房主结算条件是「roster 里只剩 1 人没被淘汰」。这意味着真机多人德比里，
-- 房主永远收不到「别人被撞毁了」这件事 → #alive 永远 >= 2 → **回合永远不结束**。
--
-- 修法（只加不改）：每个客户端把自己「刚刚被淘汰」这件事广播出去，
-- 房主收到后替对应玩家记账。仍然是房主权威，加入者只是上报自己的状态。
-- ---------------------------------------------------------------------------
function M.noteRemoteEliminated(pid)
  pid = tostring(pid or '')
  if pid == '' then return end
  -- 别人的淘汰由远端上报；本机那格只由 tickDerby 写，避免被远端包覆盖
  if pid == tostring(S.selfId or 'self') then return end
  S.eliminated[pid] = true
  logMode('远端已被淘汰:', pid)
end

-- 本机是否已被淘汰（主模块用它决定要不要广播）
function M.selfEliminated()
  return S.eliminated[S.selfId or 'self'] == true
end

-- 去重键：主模块拿它判断「本机的淘汰状态是不是刚变过」
function M.eliminatedKey()
  return (S.eliminated[S.selfId or 'self'] == true) and '1' or '0'
end

function M.requestReset()
  if S.mode == 'cops_robber' and ROUND_PHASES[S.phase] then
    return false, '警匪追逐中禁止原地复位'
  end
  if S.mode == 'derby' and S.phase == PHASE.running then
    if S.resetUsed >= TUNE.resetLimit then
      return false, '复位次数已用完（' .. tostring(TUNE.resetLimit) .. ' 次）'
    end
    if S.clock < S.resetFreeAt then
      local left = math.ceil(S.resetFreeAt - S.clock)
      return false, '复位冷却中（' .. tostring(left) .. ' 秒）'
    end
    S.resetUsed = S.resetUsed + 1
    S.resetFreeAt = S.clock + TUNE.resetCooldownMs / 1000
    return true, '复位 ' .. tostring(S.resetUsed) .. '/' .. tostring(TUNE.resetLimit)
  end
  return true, ''
end

function M.noteRemoteDamage(pid, value)
  pid = tostring(pid or '')
  if pid == '' or pid == tostring(S.selfId or 'self') then return end
  local v = tonumber(value)
  if not v or v < 0 then return end
  S.damage[pid] = v
  if S.mode == 'derby' and v >= (tonumber(TUNE.derbyDamageLimit) or 8000) then
    S.eliminated[pid] = true
  end
end

function M.noteRemoteImpact(pid, target, damage, speed)
  if S.mode ~= 'derby' or S.phase ~= PHASE.running then return end
  local me = tostring(S.selfId or 'self')
  local from = tostring(pid or '')
  if from == '' or from == me then return end
  if tostring(target or '') ~= me then return end
  local d = tonumber(damage)
  if not d or d <= 0 then return end
  local cool = (tonumber(TUNE.impactCooldownMs) or 700) / 1000
  local last = S.lastImpactAt[from]
  if last and (S.clock - last) < cool then return end
  S.lastImpactAt[from] = S.clock
  S.impacts[from] = (S.impacts[from] or 0) + 1
  S.ramDamage[me] = (S.ramDamage[me] or 0) + d
  S.hitFlash = 2.5
  S.hitNotice = { self = true, name = roleNameOf(from), speed = tonumber(speed) or 0 }
end

function M.takeImpactPacket()
  local p = S.pendingImpact
  S.pendingImpact = nil
  return p
end

function M.takeHitNotice()
  local n = S.hitNotice
  S.hitNotice = nil
  return n
end

function M.selfDamage()
  return damageOf(S.selfId or 'self')
end

function M.needDamageReport()
  if S.mode ~= 'derby' or S.phase ~= PHASE.running then
    S.lastReport = -1
    return nil
  end
  local interval = (tonumber(TUNE.reportIntervalMs) or 1000) / 1000
  if (S.clock - (S.reportAt or 0)) < interval then return nil end
  S.reportAt = S.clock
  local v = math.floor(damageOf(S.selfId or 'self'))
  if v == S.lastReport then return nil end
  S.lastReport = v
  return v
end

function M.hostSync()
  if not S.authority then return nil end
  if S.mode ~= 'cops_robber' and S.mode ~= 'derby' then return nil end
  if S.phase == PHASE.lobby then return nil end
  local left = roundTimeLeft()
  local hold = S.captureSince and ((S.clock - S.captureSince) * 1000) or 0
  local key = table.concat({
    tostring(S.round), S.phase, tostring(math.floor(hold)),
    tostring(S.captureBy or ''), left and string.format('%.0f', left) or '-',
    tostring(survivorCount()), tostring(S.finishReason or ''),
    tostring(S.winner or ''), tostring(S.roleSig or ''),
  }, '|')
  if key == S.lastSyncKey and not S.forceSync then return nil end
  S.lastSyncKey = key
  S.forceSync = false
  local dmg, elim, roster = {}, {}, {}
  for _, p in ipairs(S.roster or {}) do
    local id = tostring(p.id)
    dmg[id] = math.floor(damageOf(p.id))
    if S.eliminated[p.id] then elim[#elim + 1] = id end
    roster[#roster + 1] = { id = id, name = tostring(p.name or id) }
  end
  return {
    round = S.round, phase = S.phase, winner = tostring(S.winner or ''),
    captureMs = math.floor(hold), captureBy = tostring(S.captureBy or ''),
    roundLeft = left, survivors = survivorCount(),
    damage = dmg, eliminated = elim, roster = roster,
    roles = S.roles, finishReason = tostring(S.finishReason or ''),
  }
end

function M.applyHostSync(t)
  if type(t) ~= 'table' then return end
  if S.authority then return end
  local me = tostring(S.selfId or 'self')
  if tonumber(t.round) then S.round = tonumber(t.round) end
  if tonumber(t.captureMs) then S.remoteCaptureMs = tonumber(t.captureMs) end
  if type(t.captureBy) == 'string' then S.remoteCaptureBy = t.captureBy end
  if tonumber(t.roundLeft) then
    S.remoteRoundLeft = tonumber(t.roundLeft)
    S.remoteRoundLeftAt = S.clock
  end
  if tonumber(t.survivors) then S.remoteSurvivors = tonumber(t.survivors) end
  if type(t.finishReason) == 'string' and t.finishReason ~= '' then S.finishReason = t.finishReason end
  if type(t.winner) == 'string' and t.winner ~= '' then S.winner = t.winner end
  if type(t.roster) == 'table' then
    local list = {}
    for _, e in ipairs(t.roster) do
      if type(e) == 'table' and e.id then
        list[#list + 1] = { id = tostring(e.id), name = tostring(e.name or e.id) }
      end
    end
    if #list > 0 then S.roster = list end
  end
  if type(t.roles) == 'table' then
    S.roles = {}
    for k, v in pairs(t.roles) do S.roles[tostring(k)] = tostring(v) end
    S.selfRole = S.roles[me] or S.selfRole
  end
  if type(t.damage) == 'table' then
    for k, v in pairs(t.damage) do
      local pid = tostring(k)
      if pid ~= me and tonumber(v) then S.damage[pid] = tonumber(v) end
    end
  end
  if type(t.eliminated) == 'table' then
    for _, pid in ipairs(t.eliminated) do
      local k = tostring(pid)
      if k ~= me then S.eliminated[k] = true end
    end
  end
end

function M.derbyResults()
  local list = {}
  for _, p in ipairs(S.roster or {}) do
    list[#list + 1] = {
      id = tostring(p.id),
      name = tostring(p.name or p.id),
      eliminated = S.eliminated[p.id] == true,
      damage = math.floor(damageOf(p.id)),
    }
  end
  table.sort(list, function(a, b)
    if a.eliminated ~= b.eliminated then return not a.eliminated end
    if a.damage ~= b.damage then return a.damage < b.damage end
    return a.id < b.id
  end)
  return list
end

-- ---------------------------------------------------------------------------
-- 对外：查询
-- ---------------------------------------------------------------------------
function M.phase()      return S.phase end
function M.mode()       return S.mode end
function M.round()      return S.round end
function M.selfRole()   return S.selfRole end
function M.isAuthority() return S.authority end
function M.roster()     return S.roster or {} end
function M.roles()      return S.roles or {} end

-- 当前阶段已经过了多少毫秒（HUD 结算淡入淡出用）
function M.phaseAgeMs()
  return math.max(0, (S.clock - S.phaseAt) * 1000)
end

-- 应用远端（房主）广播的出生方案
-- 加入者不自己规划，只接受；同 round 只应用一次。
function M.applySpawnPlan(plan)
  if type(plan) ~= 'table' then return false end
  local r = tonumber(plan.round) or 0
  if r <= (S.lastPlanRound or -1) then return false end
  S.lastPlanRound = r
  S.pendingSpawnPlan = plan
  return true
end

function M.takeSpawnPlan()
  local p = S.pendingSpawnPlan
  S.pendingSpawnPlan = nil
  return p
end

-- 只看不留（摆放层可能一帧里探多次，不能把方案提前取走）
function M.peekSpawnPlan()
  return S.pendingSpawnPlan
end

-- 取到就清（摆放层确认用过后调用）
function M.consumeSpawnPlan()
  S.pendingSpawnPlan = nil
end

-- 就位汇报：摆放层摆好自己后调这个；房主收到远端汇报也调这个（带 pid）。
--
-- BeamLink 的做法是每个客户端上报 `placementReady`，服务端等齐。
-- 我们没有服务端，改成「房主汇总 + 广播名单」，逻辑等价。
function M.notePlacement(pid, ready)
  if type(pid) ~= 'string' or pid == '' then return end
  if ready == false then
    S.placementReady[pid] = nil
  else
    S.placementReady[pid] = true
  end
end

-- 本机是否已汇报到位
function M.selfPlacementReady()
  return S.placementReady[S.selfId or 'self'] == true
end

function M.placementStatus()
  local ready, total = 0, 0
  for _, pid in ipairs(S.placementOrder or {}) do
    total = total + 1
    if S.placementReady[pid] then ready = ready + 1 end
  end
  return ready, total
end

-- 远端阶段同步（房主广播后，加入者直接跟）
function M.applyRemotePhase(phase, round, winner)
  if type(phase) ~= 'string' or phase == '' then return end
  if S.authority then return end        -- 自己是权威就不听别人的
  if PHASE[phase] == nil then return end
  if S.phase ~= phase then
    S.phase = phase
    S.phaseAt = S.clock
    if tonumber(round) then S.round = tonumber(round) end
    logMode('跟随房主阶段 →', phase)
  end
  if winner ~= nil then S.winner = tostring(winner) end
  -- ⚠️ 关键：把这次远端阶段「锁住」。
  --    加入者连上房间时，tick 里的「玩法切走 → 重新开始」块会在**第一帧**
  --    把 phase 打回 lobby。如果房主的 mode-phase 正好在那之前到达
  --    （真机里 room-config/game-mode/mode-phase 是连着推的），
  --    刚收到的 running 会被下一帧的复位**静默抹掉** —— 加入者永远停在 lobby，
  --    而房主那边已经在跑了（且不会再重发同阶段）。这里立个旗子挡住那次复位。
  S.remotePhaseFresh = true
end

function M.snapshot()
  return {
    mode = S.mode,
    phase = S.phase,
    round = S.round,
    -- 给积分模块当幂等身份：同一局每帧都是这个值，跨局必然不同。
    phaseAt = S.phaseAt,
    role = S.selfRole,
    authority = S.authority,
    winner = S.winner,
    captured = S.captureDone,
    captureMs = S.captureSince and ((S.clock - S.captureSince) * 1000)
      or (tonumber(S.remoteCaptureMs) or 0),
    captureHoldMs = TUNE.captureHoldMs,
    secondsLeft = (S.phase == PHASE.countdown)
      and math.max(0, math.ceil(TUNE.countdownSeconds - (S.clock - S.phaseAt))) or nil,
    derbyDamage = damageOf(S.selfId or 'self'),
    derbyLimit = TUNE.derbyDamageLimit,
    roundLeft = viewRoundLeft(),
    roundSeconds = TUNE.roundSeconds,
    finishReason = S.finishReason or '',
    ramCount = S.impacts[S.selfId or 'self'] or 0,
    hitFlash = S.hitFlash or 0,
    captureBy = (S.captureBy ~= '' and S.captureBy) or S.remoteCaptureBy or '',
    -- ⚠️ HUD 结算行读的是 snap.eliminated；原来没导出 → 永远走「你存活到了最后」，
    --    哪怕本机明明被撞毁了。这里补上（只读本机那一格）。
    eliminated = (S.eliminated[S.selfId or 'self'] == true),
    derbyAlive = (S.remoteSurvivors ~= nil) and S.remoteSurvivors or survivorCount(),
    resetUsed = S.resetUsed,
    resetLimit = TUNE.resetLimit,
    roles = S.roles,
  }
end

function M.tune() return TUNE end

-- ---------------------------------------------------------------------------
-- 朝向工具：别用 math.atan2（Lua 5.4 已移除，5.5 也没有）。
-- 用 atan2 的自实现 —— 只有 4 个象限分支，很便宜。
-- ---------------------------------------------------------------------------
local function srAtan2(y, x)
  if x > 0 then return math.atan(y / x) end
  if x < 0 then
    if y >= 0 then return math.atan(y / x) + math.pi end
    return math.atan(y / x) - math.pi
  end
  if y > 0 then return math.pi / 2 end
  if y < 0 then return -math.pi / 2 end
  return 0
end
M.srAtan2 = srAtan2

-- 本机车头方向的水平单位向量（拿不到返回 nil）
local function selfForward()
  local fx, fy = nil, nil
  pcall(function()
    if be == nil or be.getPlayerVehicle == nil then return end
    local v = be:getPlayerVehicle(0)
    if v and v.getDirectionVectorXYZ ~= nil then
      local dx, dy = v:getDirectionVectorXYZ()
      fx, fy = dx, dy
    end
  end)
  if not fx or not fy then return nil end
  local flen = math.sqrt(fx * fx + fy * fy)
  if flen < 0.01 then return nil end
  return fx / flen, fy / flen
end

-- 雷达用：取本机到强盗的 XY 距离与方位角（相对本机车头）
-- 返回 dist, bearing（bearing 为 nil 表示拿不到目标位置）
function M.robberBearing()
  if S.mode ~= 'cops_robber' or S.selfRole ~= ROLE.cop then return nil, nil end

  local meX, meY = vehiclePos('self')
  if not meX then return nil, nil end

  local targetX, targetY = nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.robber then
      local px, py = rolePos(pid)
      if px and py then targetX, targetY = px, py end
    end
  end
  if not targetX then return nil, nil end

  local fx, fy = selfForward()
  if not fx then return nil, nil end

  local dx, dy = targetX - meX, targetY - meY
  local dist = math.sqrt(dx * dx + dy * dy)
  if dist <= 0.5 then return nil, nil end

  local ux, uy = dx / dist, dy / dist
  local ahead = ux * fx + uy * fy
  local side = ux * fy - uy * fx

  return dist, srAtan2(side, ahead)
end

-- ---------------------------------------------------------------------------
-- 强盗端：威胁感知（最近的警察离我多近、在哪个方向）
--
-- 参考 BeamLink 的 _robberThreatState：找最近的警察，按距离分档
-- 「危险 / 警车接近 / 暂时安全」，并给一个 0..1 的强度值给 HUD 画条。
-- 方位用和警察雷达同一套算法（相对车头）。
--
-- 返回 table 或 nil（不在警匪模式 / 不是强盗 / 拿不到位置）
--   { dist=米, bearing=弧度 或 nil, level=0..1, label='危险'|'警车接近'|'暂时安全',
--     direction='前方'|'后方'|'左侧'|'右侧'|nil, captureHeldMs=当前已贴住毫秒 }
-- ---------------------------------------------------------------------------
local THREAT_DANGER = 30.0    -- < 30m 判「危险」
local THREAT_NEAR   = 120.0   -- < 120m 判「警车接近」
local THREAT_RANGE  = 400.0   -- 强度条的满量程

function M.robberThreat()
  if S.mode ~= 'cops_robber' or S.selfRole ~= ROLE.robber then return nil end
  if S.phase ~= PHASE.running then
    -- 非 running 阶段也返回一个「无信号」结构，HUD 好统一处理
    return { signal = false, dist = nil, level = 0, label = '', direction = nil }
  end

  local meX, meY = vehiclePos('self')
  if not meX then return { signal = false, dist = nil, level = 0, label = '', direction = nil } end

  local bestD, bestX, bestY = nil, nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.cop then
      local px, py = rolePos(pid)
      if px and py then
        local d = dist2(meX, meY, px, py)
        if not bestD or d < bestD then bestD, bestX, bestY = d, px, py end
      end
    end
  end

  if not bestD then
    return { signal = false, dist = nil, level = 0, label = '', direction = nil }
  end

  local label = (bestD < THREAT_DANGER and '危险')
             or (bestD < THREAT_NEAR and '警车接近')
             or '暂时安全'
  local level = 1 - bestD / THREAT_RANGE
  if level < 0 then level = 0 elseif level > 1 then level = 1 end

  -- 方位（拿不到车头方向就只给距离）
  local direction = nil
  local fx, fy = selfForward()
  if fx then
    local dx, dy = bestX - meX, bestY - meY
    if bestD > 0.5 then
      local ux, uy = dx / bestD, dy / bestD
      local ahead = ux * fx + uy * fy
      local side = ux * fy - uy * fx
      local deg = math.deg(srAtan2(side, ahead))
      local abs = math.abs(deg)
      direction = (abs <= 15 and '前方')
              or (abs >= 150 and '后方')
              or (deg < 0 and '左侧')
              or '右侧'
    end
  end

  -- 本机（强盗）当前已经被贴住多久
  local heldMs = 0
  if S.captureSince then heldMs = (S.clock - S.captureSince) * 1000 end

  return {
    signal = true,
    dist = bestD,
    level = level,
    label = label,
    direction = direction,
    captureHeldMs = heldMs,
    captureHoldMs = TUNE.captureHoldMs,
  }
end

function M.reset()
  S.phase = PHASE.lobby
  S.round = 0
  S.selfRole = ROLE.none
  S.captureSince = nil
  S.captureDone = false
  S.eliminated = {}
  S.damage = {}
  S.resetUsed = 0
  S.winner = ''
  -- 全量复位：不清冷却的话，reset 之后状态机会一直停在 lobby 不推进
  -- （第 5b 段的超时冷却残留 → 后面的用例全部假阴性）。
  S.retryBlockedUntil = 0
  S.placementReady = {}
  S.placementOrder = {}
  S.pendingSpawnPlan = nil
  S.lastPlanRound = -1
  S.damage = {}
  S.ramDamage = {}
  S.nativeDamage = 0
  S.impacts = {}
  S.lastImpactAt = {}
  S.hitFlash = 0
  S.hitNotice = nil
  S.pendingImpact = nil
  S.finishReason = ''
  S.lastReport = -1
  S.lastSyncKey = ''
  S.forceSync = false
  S.remoteCaptureMs = 0
  S.remoteCaptureBy = ''
  S.remoteRoundLeft = nil
  S.remoteSurvivors = nil
  S.navAt = 0
  S.navPos = nil
  S.navOn = false
end

return M
