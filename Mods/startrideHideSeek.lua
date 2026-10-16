-- ============================================================================
-- StartRide 捉迷藏玩法（规则主体）
-- ============================================================================
-- 角色：  搜索者（seeker）1 人 + 躲藏者（hider）N 人
-- 阶段：  复用阶段机的 lobby / staging / countdown / running / finished，
--        但在 running 内部再切两个**局内阶段**（stage）：
--          hiding  躲藏期 —— 搜索者被冻住（不能动车），躲藏者四散藏好
--          seek    搜索期 —— 搜索者放行，去把躲藏者找出来
--
-- 发现判定（对齐 BeamLink 的「躲猫猫」语义，是我们自己推的最合理版本）：
--   搜索者的车进入躲藏者 R 米范围内 → 开始计时；连续保持 HOLD 毫秒 → 该躲藏者被找到。
--   中途搜索者跑开 → 进度清零（不做衰减：衰减会让两个人在边缘来回蹭就刷满）。
--   被找到的躲藏者**变成搜索者**（BeamLink 面板文案「被找到的人加入搜索方」），
--   这样人越多越难藏，回合不会无限拖。
--
-- 胜负：
--   搜索方胜 —— 所有躲藏者都被找到
--   躲藏方胜 —— 计时归零时还有人没被找到（BeamLink 里是「逃脱区开放」，
--              我们没有服务端权威的逃脱区，改成纯计时收敛，更好理解）
--
-- 权威性：房主（host）说了算，和警匪/德比一致。躲藏者是否被找到**由房主判定**
-- 并广播（`hide-seek-found` 包），因为「谁在谁旁边」只有房主能统一裁决；
-- 客户端自己只做「本地预测」（搜索者自己贴近时立刻看到进度条）。
--
-- ⚠️ Lua 5.1 上限：本文件独立成一个模块，就是因为主模组顶层 local 已经 133 个，
--    再往里塞会撞 200 上限（撞了直接编译失败、整个扩展不加载）。
--
-- 上值控制：所有可调参数收进 TUNE；跨模块取数走 setDeps 注入。
-- ============================================================================

local M = {}

-- ---------------------------------------------------------------------------
-- 可调参数（由启动器写进 startride/multiplayer.json 的 modeRules，readConfig 读）
-- ---------------------------------------------------------------------------
local TUNE = {
  hideSeconds       = 30,     -- 躲藏期时长（搜索者被冻住）
  roundSeconds      = 240,    -- 搜索期时长（归零 = 躲藏方胜；启动器 hideRoundSeconds 覆盖此处）
  findRadius        = 12.0,   -- 发现半径（米）
  findHoldMs        = 2000,   -- 贴近多久算找到（毫秒）
  findStillSpeed    = 0,      -- 躲藏者「近乎静止」阈值；0 = 不启用静止要求
  minPlayers        = 2,      -- 少于此人数不启动
  seekerCount       = 1,      -- 搜索者人数（固定 1，保留可调）
  -- 出生规划参数（本模块自规划，不走走廊/围场那两条路）
  seekerStandoff    = 260,    -- 搜索者与最近躲藏者的最小距离（米）
  hiderClusterMin   = 90,     -- 躲藏者两两最小距离（米）
  detectNear        = 5.0,
  detectMid         = 10.0,
  detectFar         = 18.0,
  rateNear          = 1.5,
  rateMid           = 0.8,
  rateFar           = 0.35,
  detectDecay       = 0.3,
  escapeAfterSeconds = 100,
  escapeRadius      = 15.0,
  zoneRadius        = 900.0,
  outsideGraceSeconds = 25,
  skillCooldownSeconds = 45,
  pingSeconds       = 10,
  breakSeconds      = 6,
  stagingSeconds    = 8,
  countdownSeconds  = 5,
  finishHoldSeconds = 6,
  placementRetrySeconds = 5,
}

local PHASE = {
  lobby     = 'lobby',
  staging   = 'staging',
  countdown = 'countdown',
  running   = 'running',
  finished  = 'finished',
}

local ROLE = {
  seeker = 'seeker',
  hider  = 'hider',
  none   = 'none',
}

-- 局内阶段（running 内部）
local STAGE = {
  hiding = 'hiding',
  seek   = 'seek',
  escape = 'escape',
}

local ROUND_PHASES = {
  [PHASE.staging]   = true,
  [PHASE.countdown] = true,
  [PHASE.running]   = true,
}

-- ---------------------------------------------------------------------------
-- 运行状态
-- ---------------------------------------------------------------------------
local S = {
  mode      = 'hide_seek',
  phase     = PHASE.lobby,
  stage     = STAGE.hiding,
  round     = 0,
  selfRole  = ROLE.none,
  host      = false,
  authority = false,

  phaseAt   = 0,
  stageAt   = 0,
  clock     = 0,
  countdownFrom = 0,

  winner    = '',            -- 'seeker' / 'hider' / ''
  reason    = '',            -- 结算原因（给 HUD 用）

  -- 角色分配
  roles     = {},            -- [pid] = 'seeker' / 'hider'
  roster    = {},            -- { {id=, name=}, ... }（稳定排序后）
  -- 已经找到的躲藏者（找到后 role 也改成 seeker，这里留一份名单给 HUD 计数）
  found     = {},            -- [pid] = true

  -- 位置缓存 [pid] = { x, y, at }
  positions = {},

  -- 发现进度（本机是搜索者时本地预算；房主用于判定）
  findSince = nil,           -- 当前贴近的起点
  findBy    = '',            -- 当前被贴近的躲藏者 id
  -- 房主广播的进度（加入者展示）
  remoteFindTarget = '',
  remoteFindMs     = 0,

  -- 就位汇报
  placementReady   = {},
  placementOrder   = {},
  retryBlockedUntil = 0,

  lastAnnounce = '',
  lastSentKey  = '',
  cfgLoaded    = false,
  lastBlockReason = '',

  -- 出生方案
  pendingSpawnPlan = nil,
  lastPlanRound    = -1,
  plannedRound     = -1,

  -- 结算广播去重
  lastFoundSentKey = '',

  seekAt       = 0,
  detection    = 0,
  proximity    = '',
  detectTarget = '',
  detectBy     = '',
  skillReadyAt = 0,
  skillUsed    = '',
  shieldUntil  = 0,
  pingTarget   = '',
  pingUntil    = 0,
  zone         = nil,
  escape       = nil,
  outside      = {},
  escapedBy    = '',
  localOutsideSince = nil,
  shield       = {},
  localDetection = 0,
  localBand    = '',
  goneAt       = {},
  leftIds      = {},
  leftElim     = {},
}

-- ---------------------------------------------------------------------------
-- 注入依赖（由主模组一次性 setDeps）
-- ---------------------------------------------------------------------------
local deps = {
  getVehicle    = nil,
  getLevelId    = nil,
  logMode       = nil,
  shuffle       = nil,   -- 可选：确定性打乱用；不注入则内部用简单 LCG
}

function M.setDeps(d)
  if type(d) ~= 'table' then return end
  for _, k in ipairs({ 'getVehicle', 'getLevelId', 'logMode', 'shuffle' }) do
    if d[k] ~= nil then deps[k] = d[k] end
  end
end

-- ---------------------------------------------------------------------------
-- 工具
-- ---------------------------------------------------------------------------
local function logHS(...)
  if deps.logMode ~= nil then
    pcall(deps.logMode, ...)
    return
  end
  local parts = { '[StartRide] 捉迷藏：' }
  for _, v in ipairs({ ... }) do parts[#parts + 1] = tostring(v) end
  pcall(function() log('I', 'startride', table.concat(parts, ' ')) end)
end

local function clamp(v, lo, hi)
  if v < lo then return lo end
  if v > hi then return hi end
  return v
end

local function dist2(ax, ay, bx, by)
  local dx, dy = ax - bx, ay - by
  return math.sqrt(dx * dx + dy * dy)
end

local function vehiclePos()
  local x, y = nil, nil
  pcall(function()
    local v = nil
    if deps.getVehicle ~= nil then
      local ok, got = pcall(deps.getVehicle)
      if ok then v = got end
    elseif be ~= nil and be.getPlayerVehicle ~= nil then
      v = be:getPlayerVehicle(0)
    end
    if v then local p = v:getPosition(); x, y = p.x, p.y end
  end)
  return x, y
end

local function selfSpeed()
  local sp = nil
  pcall(function()
    local v = nil
    if deps.getVehicle ~= nil then
      local ok, got = pcall(deps.getVehicle)
      if ok then v = got end
    elseif be ~= nil and be.getPlayerVehicle ~= nil then
      v = be:getPlayerVehicle(0)
    end
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

-- ---------------------------------------------------------------------------
-- 配置读取
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
  if type(rules) ~= 'table' then rules = cfg end

  local function num(key, lo, hi, dst)
    local v = tonumber(rules[key])
    if v and v >= lo and v <= hi then TUNE[dst or key] = v end
  end

  num('hideSeconds', 0, 300)
  num('hideRoundSeconds', 30, 3600, 'roundSeconds')
  num('findRadius', 2, 200)
  num('findHoldMs', 200, 60000)
  num('seekerCount', 1, 8)
  num('hiderClusterMin', 30, 2000)
  num('seekerStandoff', 30, 3000)
  num('detectNear', 1, 400)
  num('detectMid', 2, 600)
  num('detectFar', 3, 900)
  num('rateNear', 0.05, 20)
  num('rateMid', 0.05, 20)
  num('rateFar', 0.05, 20)
  num('detectDecay', 0, 5)
  num('escapeAfterSeconds', 5, 3600)
  num('escapeRadius', 3, 300)
  num('zoneRadius', 50, 5000)
  num('outsideGraceSeconds', 3, 600)
  num('skillCooldownSeconds', 1, 600)
  num('pingSeconds', 1, 120)
  num('breakSeconds', 0, 60)

  if type(rules.findRadius) == 'number' then
    local fr = tonumber(rules.findRadius) or 0
    if fr > 0 then
      TUNE.detectFar = math.max(fr, TUNE.detectMid or 10)
      if TUNE.detectMid > TUNE.detectFar then TUNE.detectMid = TUNE.detectFar end
      if TUNE.detectNear > TUNE.detectMid then TUNE.detectNear = TUNE.detectMid end
    end
  end

  return true
end

-- ---------------------------------------------------------------------------
-- 角色分配
--
-- 规则：按 id 稳定排序后，第 1 个当搜索者，其余全是躲藏者。
-- 稳定排序是硬要求 —— 两端算出来的角色必须完全一致，否则房主说「你是躲藏者」、
-- 本地说「我是搜索者」，输入限制会两头打架。
-- ---------------------------------------------------------------------------
local function assignRoles()
  local list = {}
  list[#list + 1] = { id = S.selfId or 'self', name = S.playerName or '' }

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

  table.sort(list, function(a, b) return tostring(a.id) < tostring(b.id) end)

  local seekerN = math.floor(TUNE.seekerCount)
  if seekerN < 1 then seekerN = 1 end
  if seekerN > #list - 1 then seekerN = #list - 1 end

  S.roles = {}
  for i, p in ipairs(list) do
    S.roles[p.id] = (i <= seekerN) and ROLE.seeker or ROLE.hider
  end

  S.selfRole = S.roles[S.selfId or 'self'] or ROLE.hider
  S.roster = list
  return true
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
    S.winner = ''
    S.reason = ''
    S.found = {}
    -- ⚠️ 下面三个是**局内临时状态**，必须跟着局一起归零：
    --    escapedBy —— 不清的话，上一局有人逃出后，之后每一局的躲藏期一结束就会被
    --                tickStageAuthority 判成「有躲藏者逃出 → 躲藏方胜」，玩法直接
    --                坏掉（现场看起来只是「这局刚开始怎么就结束了」）。
    --    outside   —— 出圈计时的起点时间戳；残留会让新局刚进搜索期就按旧时间戳
    --                算够宽限，把站在圈外的人当场判出局。
    --    shield    —— 隐身剩余时间（按 S.clock 比较，本来会自然过期，一起清干净）。
    --    zone / escape 不在这里清：它们由大厅出生计划下发，不是局内状态。
    S.escapedBy = ''
    S.outside = {}
    S.shield = {}
    S.findSince = nil
    S.findBy = ''
    S.remoteFindTarget = ''
    S.remoteFindMs = 0
    S.stage = STAGE.hiding
    S.stageAt = S.clock
    S.placementReady = {}
    S.goneAt = {}
    S.leftIds = {}
    S.leftElim = {}
    local order = {}
    for _, p in ipairs(S.roster or {}) do
      if type(p) == 'table' and p.id ~= nil then order[#order + 1] = tostring(p.id) end
    end
    S.placementOrder = order
  elseif ph == PHASE.countdown then
    S.countdownFrom = TUNE.countdownSeconds or 5
  elseif ph == PHASE.running then
    S.stage = STAGE.hiding
    S.stageAt = S.clock
    S.findSince = nil
    S.findBy = ''
  end

  if announce ~= false then S.lastAnnounce = '' end
  logHS('阶段 →', ph, '回合', S.round)
end

-- ---------------------------------------------------------------------------
-- 位置缓存
-- ---------------------------------------------------------------------------
function M.notePositions(data)
  if type(data) ~= 'table' then return end
  local pid = tostring(data.id or '')
  if pid == '' or type(data.pos) ~= 'table' then return end
  local x, y = tonumber(data.pos[1]), tonumber(data.pos[2])
  if not x or not y then return end
  S.positions[pid] = { x = x, y = y, at = S.clock }
end

local function tickPositions()
  local sx, sy = vehiclePos()
  if sx and sy then
    S.positions[S.selfId or 'self'] = { x = sx, y = sy, at = S.clock }
  end
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
          if S.roles[pid] == ROLE.hider then S.found[pid] = nil end
          logHS('玩家回来了，恢复参战:', pid)
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
      S.found[pid] = true
      S.placementReady[pid] = true
      logHS('玩家已离开，本回合按出局处理:', pid)
    end
  end
end

local function rolePos(pid)
  local rec = S.positions[pid]
  if rec then return rec.x, rec.y end
  return nil, nil
end

-- ---------------------------------------------------------------------------
-- 局内阶段推进（权威端）
--   hiding → seek          躲藏期结束
--   seek   → finished      全员被找到（搜索方胜）/ 计时归零（躲藏方胜）
-- ---------------------------------------------------------------------------
local function setStage(st)
  if S.stage == st then return end
  S.stage = st
  S.stageAt = S.clock
  S.findSince = nil
  S.findBy = ''
  S.detection = 0
  S.detectTarget = ''
  S.detectBy = ''
  S.proximity = ''
  if st == STAGE.seek then
    S.seekAt = S.clock
    logHS('躲藏期结束，搜索开始')
  elseif st == STAGE.escape then
    logHS('逃脱区已开放')
  end
end

local function aliveHiders()
  local n = 0
  for _, p in ipairs(S.roster or {}) do
    local pid = tostring(p.id)
    if S.roles[pid] == ROLE.hider and not S.found[pid] then n = n + 1 end
  end
  return n
end

local function tickStageAuthority()
  if S.phase ~= PHASE.running then return end

  if S.stage == STAGE.hiding then
    if (S.clock - S.stageAt) >= (tonumber(TUNE.hideSeconds) or 30) then
      setStage(STAGE.seek)
    end
    return
  end

  if aliveHiders() <= 0 then
    S.winner = ROLE.seeker
    S.reason = 'all_found'
    setPhase(PHASE.finished, true)
    logHS('全部躲藏者被找到 —— 搜索方胜')
    return
  end

  if S.escapedBy ~= '' then
    S.winner = ROLE.hider
    S.reason = 'escaped'
    setPhase(PHASE.finished, true)
    logHS('有躲藏者逃出 —— 躲藏方胜')
    return
  end

  local seekElapsed = S.clock - (S.seekAt or S.stageAt)
  if S.stage == STAGE.seek and seekElapsed >= (tonumber(TUNE.escapeAfterSeconds) or 100) then
    setStage(STAGE.escape)
  end
  if seekElapsed >= (tonumber(TUNE.roundSeconds) or 240) then
    S.winner = ROLE.hider
    S.reason = 'time_up'
    setPhase(PHASE.finished, true)
    logHS('搜索时间耗尽 —— 躲藏方胜')
  end
end

-- ---------------------------------------------------------------------------
-- 发现判定（权威端）
--
-- 对每个「还在躲的躲藏者」，找最近的搜索者；只要有一个搜索者进了半径并贴住，
-- 就按**那一个躲藏者**独立计时（多个躲藏者可以同时被打进度，但玩家通常只盯一个）。
-- 简化：全局只维护**一个**当前目标（最近的、在半径内的躲藏者），
-- 因为搜索者同一时刻只可能贴住一个人；换来的是状态极小、两端好同步。
-- ---------------------------------------------------------------------------
local function nearestSeekerTo(hx, hy)
  local best, bestD = nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.seeker then
      local px, py = rolePos(pid)
      if px and py then
        local d = dist2(hx, hy, px, py)
        if not bestD or d < bestD then best, bestD = pid, d end
      end
    end
  end
  return best, bestD
end

local function proximityBand(d)
  if not d then return '', 0 end
  local near = tonumber(TUNE.detectNear) or 5
  local mid  = tonumber(TUNE.detectMid) or 10
  local far  = tonumber(TUNE.detectFar) or 18
  if d <= near then return 'near', (tonumber(TUNE.rateNear) or 1.5) end
  if d <= mid  then return 'middle', (tonumber(TUNE.rateMid) or 0.8) end
  if d <= far  then return 'far', (tonumber(TUNE.rateFar) or 0.35) end
  return '', 0
end

local function decayDetection(dt)
  if S.detection <= 0 then return end
  S.detection = math.max(0, S.detection - (tonumber(TUNE.detectDecay) or 0.3) * dt)
  if S.detection <= 0 then
    S.detection = 0
    S.detectTarget = ''
    S.detectBy = ''
    S.proximity = ''
  end
end

local function zoneCenter(kind)
  local z = (kind == 'escape') and S.escape or S.zone
  if type(z) ~= 'table' then return nil, nil, nil end
  local x, y = tonumber(z.x), tonumber(z.y)
  if not x or not y then return nil, nil, nil end
  local r = tonumber(z.radius)
    or ((kind == 'escape') and (tonumber(TUNE.escapeRadius) or 15) or (tonumber(TUNE.zoneRadius) or 900))
  return x, y, r
end

local function selfOutside()
  if S.selfRole ~= ROLE.hider then return false end
  local cx, cy, r = zoneCenter('zone')
  if not cx then return false end
  local mx, my = vehiclePos()
  if not mx then return false end
  return dist2(mx, my, cx, cy) > r
end

local function tickFindAuthority(dt)
  if S.phase ~= PHASE.running then return end
  if S.stage == STAGE.hiding then return end
  local step = tonumber(dt) or 0.016
  if step > 0.25 then step = 0.25 end

  local target, targetSeeker, targetD = nil, nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local shielded = (tonumber(S.shield[pid]) or 0) > S.clock
      if not shielded then
        local hx, hy = rolePos(pid)
        if hx and hy then
          local sk, d = nearestSeekerTo(hx, hy)
          if sk and d and d <= (tonumber(TUNE.detectFar) or 18) then
            if not targetD or d < targetD then
              target, targetSeeker, targetD = pid, sk, d
            end
          end
        end
      end
    end
  end

  if not target then
    decayDetection(step)
    return
  end

  if S.detectTarget ~= target then
    S.detectTarget = target
    S.detection = 0
  end
  local band, rate = proximityBand(targetD)
  if band == '' then
    decayDetection(step)
    return
  end

  S.proximity = band
  S.detectBy = targetSeeker
  S.detection = math.min(1, S.detection + rate * step)
  if S.detection < 1 then return end

  S.found[target] = true
  if S.roles[target] == ROLE.hider then S.roles[target] = ROLE.seeker end
  S.detection = 0
  S.detectTarget = ''
  S.detectBy = ''
  S.proximity = ''
  logHS(tostring(target) .. ' 被找到 → 转入搜索方')
  if aliveHiders() <= 0 then
    S.winner = ROLE.seeker
    S.reason = 'all_found'
    setPhase(PHASE.finished, true)
  end
end

local function tickEscapeAuthority()
  if S.phase ~= PHASE.running then return end
  if S.stage ~= STAGE.escape then return end
  if S.escapedBy ~= '' then return end
  local cx, cy, r = zoneCenter('escape')
  if not cx then return end
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local px, py = rolePos(pid)
      if px and py and dist2(px, py, cx, cy) <= r then
        S.escapedBy = tostring(pid)
        return
      end
    end
  end
end

local function tickZoneAuthority(dt)
  if S.phase ~= PHASE.running then return end
  if S.stage == STAGE.hiding then return end
  local cx, cy, r = zoneCenter('zone')
  if not cx then return end
  local grace = tonumber(TUNE.outsideGraceSeconds) or 25
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local px, py = rolePos(pid)
      if px and py then
        if dist2(px, py, cx, cy) > r then
          if not S.outside[pid] then
            S.outside[pid] = S.clock
            logHS(tostring(pid) .. ' 离开活动区域，开始倒计时')
          elseif (S.clock - S.outside[pid]) >= grace then
            S.outside[pid] = nil
            S.found[pid] = true
            if S.roles[pid] == ROLE.hider then S.roles[pid] = ROLE.seeker end
            logHS(tostring(pid) .. ' 离开区域超时出局')
            if aliveHiders() <= 0 then
              S.winner = ROLE.seeker
              S.reason = 'outside'
              setPhase(PHASE.finished, true)
            end
          end
        else
          S.outside[pid] = nil
        end
      end
    end
  end
end

-- 本地（搜索者）预测：本机贴近某个躲藏者时给出进度，HUD 不必等房主广播。
local function tickFindLocal(dt)
  if S.phase ~= PHASE.running then return end
  if S.stage == STAGE.hiding then return end
  if S.selfRole ~= ROLE.seeker then
    if S.localTarget then S.localTarget = nil end
    S.localMs = 0
    return
  end

  local mx, my = vehiclePos()
  if not mx then return end
  local step = tonumber(dt) or 0.016
  if step > 0.25 then step = 0.25 end

  local best, bestD = nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local px, py = rolePos(pid)
      if px and py then
        local d = dist2(mx, my, px, py)
        if d <= (tonumber(TUNE.detectFar) or 18) and (not bestD or d < bestD) then
          best, bestD = pid, d
        end
      end
    end
  end

  if not best then
    if S.localTarget then S.localTarget = nil end
    S.localMs = 0
    if S.localDetection and S.localDetection > 0 then
      S.localDetection = math.max(0, S.localDetection - (tonumber(TUNE.detectDecay) or 0.3) * step)
    end
    return
  end

  if S.localTarget ~= best then
    S.localTarget = best
    S.localDetection = 0
  end
  local band, rate = proximityBand(bestD)
  S.localBand = band
  S.localDist = bestD
  if band == '' then return end
  S.localDetection = math.min(1, (S.localDetection or 0) + rate * step)
  S.localMs = (S.localDetection or 0) * (tonumber(TUNE.findHoldMs) or 2000)
end

function M.useSkill()
  if S.phase ~= PHASE.running then return false, '现在不能用技能' end
  if S.stage == STAGE.hiding then return false, '躲藏期还不能用技能' end
  local role = S.selfRole
  if role ~= ROLE.hider and role ~= ROLE.seeker then return false, '观战不能用技能' end
  if S.clock < (S.skillReadyAt or 0) then
    return false, '技能冷却中 ' .. tostring(math.ceil((S.skillReadyAt or 0) - S.clock)) .. ' 秒'
  end
  S.skillReadyAt = S.clock + (tonumber(TUNE.skillCooldownSeconds) or 45)
  if role == ROLE.hider then
    S.skillUsed = 'break'
    S.pendingSkill = { id = tostring(S.selfId or 'self'), skill = 'break' }
    S.shieldUntil = S.clock + (tonumber(TUNE.breakSeconds) or 6)
    S.detection = 0
    S.detectTarget = ''
    S.detectBy = ''
    S.proximity = ''
    logHS('使用技能：脱离')
    return true, '已脱离'
  end
  S.skillUsed = 'ping'
  S.pendingSkill = { id = tostring(S.selfId or 'self'), skill = 'ping' }
  local mx, my = vehiclePos()
  local best, bestD = nil, nil
  if mx then
    for pid, r in pairs(S.roles or {}) do
      if r == ROLE.hider and not S.found[pid] then
        local px, py = rolePos(pid)
        if px and py then
          local d = dist2(mx, my, px, py)
          if not bestD or d < bestD then best, bestD = pid, d end
        end
      end
    end
  end
  S.pingTarget = tostring(best or '')
  S.pingUntil = S.clock + (tonumber(TUNE.pingSeconds) or 10)
  logHS('使用技能：标记')
  return true, '已标记'
end

function M.skillState()
  local readyIn = math.max(0, (S.skillReadyAt or 0) - S.clock)
  return {
    skill = (S.selfRole == ROLE.hider) and 'break' or (S.selfRole == ROLE.seeker) and 'ping' or '',
    ready = readyIn <= 0,
    cooldownSeconds = math.ceil(readyIn),
    shieldLeft = math.max(0, (S.shieldUntil or 0) - S.clock),
    pingTarget = (S.pingUntil or 0) > S.clock and S.pingTarget or '',
    pingLeft = math.max(0, (S.pingUntil or 0) - S.clock),
  }
end

function M.takeSkillPacket()
  local p = S.pendingSkill
  S.pendingSkill = nil
  return p
end

function M.applyRemoteSkill(pid, skill)
  pid = tostring(pid or '')
  skill = tostring(skill or '')
  if pid == '' then return end
  if skill == 'break' then
    S.shield[pid] = S.clock + (tonumber(TUNE.breakSeconds) or 6)
    logHS(tostring(pid) .. ' 使用了脱离')
  elseif skill == 'ping' then
    S.pingTarget = pid
    S.pingUntil = S.clock + (tonumber(TUNE.pingSeconds) or 10)
  end
end

function M.applyRemoteEscape(pid)
  pid = tostring(pid or '')
  if pid == '' then return end
  S.escapedBy = pid
end

-- 加入者：接收房主广播的发现进度
function M.applyRemoteFound(target, heldMs, foundList)
  if type(foundList) == 'table' then
    for _, pid in ipairs(foundList) do
      local k = tostring(pid)
      S.found[k] = true
      if S.roles[k] == ROLE.hider then S.roles[k] = ROLE.seeker end
    end
  end
  S.remoteFindTarget = tostring(target or '')
  S.remoteFindMs = tonumber(heldMs) or 0
end

-- 加入者：接收局内阶段
function M.applyRemoteStage(stage)
  if S.authority then return end
  if stage ~= STAGE.hiding and stage ~= STAGE.seek then return end
  if S.stage ~= stage then
    S.stage = stage
    S.stageAt = S.clock
    if stage == STAGE.seek then S.seekAt = S.clock end
    S.detection = 0
    S.proximity = ''
    S.detectTarget = ''
    logHS('跟随房主局内阶段 →', stage)
  end
end

function M.applyRemoteFoundList(foundList, roles)
  if type(foundList) ~= 'table' then return end
  for _, pid in ipairs(foundList) do S.found[tostring(pid)] = true end
  if type(roles) == 'table' then
    for k, v in pairs(roles) do
      if type(k) == 'string' and (v == ROLE.seeker or v == ROLE.hider) then
        S.roles[k] = v
      end
    end
  end
end

-- ---------------------------------------------------------------------------
-- 阶段推进（权威端）
-- ---------------------------------------------------------------------------
local function tickAuthority(win, dt)
  if not S.authority then return end
  if S.mode ~= 'hide_seek' then return end

  local elapsed = S.clock - S.phaseAt

  if S.phase == PHASE.lobby then
    if S.retryBlockedUntil and S.clock < S.retryBlockedUntil then return end
    local ok, why = assignRoles()
    if ok then
      setPhase(PHASE.staging, true)
      logHS('开始就位（回合 ' .. tostring(S.round) .. '）')
    else
      S.lastBlockReason = why
    end

  elseif S.phase == PHASE.staging then
    local readyCount, totalCount = 0, 0
    for _, pid in ipairs(S.placementOrder or {}) do
      totalCount = totalCount + 1
      if S.placementReady[pid] then readyCount = readyCount + 1 end
    end
    local selfReady = S.placementReady[S.selfId or 'self'] == true

    if totalCount > 0 and readyCount >= totalCount then
      setPhase(PHASE.countdown, true)
    elseif elapsed >= (TUNE.stagingSeconds or 8) then
      if selfReady then
        logHS(string.format('就位超时，但本机已到位（%d/%d）→ 照常开',
          readyCount, totalCount))
        setPhase(PHASE.countdown, true)
      else
        S.lastBlockReason = 'placement_timeout'
        logHS(string.format('就位超时且本机未到位（%d/%d）→ 退回准备',
          readyCount, totalCount))
        S.retryBlockedUntil = S.clock + (TUNE.placementRetrySeconds or 5)
        setPhase(PHASE.lobby, true)
      end
    end

  elseif S.phase == PHASE.countdown then
    if elapsed >= (TUNE.countdownSeconds or 5) then
      setPhase(PHASE.running, true)
    end

  elseif S.phase == PHASE.running then
    tickStageAuthority()
    tickFindAuthority(dt)
    tickEscapeAuthority()
    tickZoneAuthority(dt)

  elseif S.phase == PHASE.finished then
    if elapsed >= (TUNE.finishHoldSeconds or 6) then
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
  S.mode       = tostring(win.gameMode or 'hide_seek')

  -- 房主判定（与警匪/德比同一套口径）
  -- ⚠️ sessionAlive 与 authority 分开记：玩法状态要在会话消失时复位，
  --    否则 phase 停在局内，而输入限制只认 phase → 按键永久锁死。
  if not win.connected or win.closed or win.relayState == 'disconnected' then
    S.authority = false
    S.sessionAlive = false
  else
    S.sessionAlive = true
    local h = tostring(win.host or '')
    if h == '' then
      S.authority = false
    else
      S.authority = (h == tostring(win.selfId or '')) or (h == tostring(win.playerName or ''))
    end
  end

  -- 会话没了（退房 / 关房 / 中继断）→ 玩法状态整个收回 lobby。
  -- 理由同警匪：输入限制只由 phase 决定，不复位就会一直锁着按键。
  if S.sessionAlive == false and S.phase ~= PHASE.lobby then
    S.phase = PHASE.lobby
    S.selfRole = ROLE.none
    S.round = 0
    S.roles = {}
    S.found = {}
    S.remoteFindTarget = ''
    S.remoteFindMs = 0
    logHS('联机会话已结束 —— 玩法复位到大厅')
  end

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
      S.roles = {}
      S.found = {}
    end
  end

  tickAuthority(win, dtReal)

  pcall(tickFindLocal, dtReal)

  if S.phase == PHASE.running and S.selfRole == ROLE.hider and not S.found[S.selfId or 'self'] then
    if selfOutside() then
      if not S.localOutsideSince then S.localOutsideSince = S.clock end
    else
      S.localOutsideSince = nil
    end
  else
    S.localOutsideSince = nil
  end

  -- 结算停留
  if S.phase == PHASE.finished and (S.clock - S.phaseAt) >= (TUNE.finishHoldSeconds or 6) then
    if S.authority then setPhase(PHASE.lobby, true) end
  end
end

-- ---------------------------------------------------------------------------
-- 对外：输入策略
--
-- ⚠️ 关键设计：躲藏期**只有搜索者被冻住**，躲藏者必须能跑（他们要去藏）。
--    所以 inputPolicy 不是全局锁，而是按角色区分：
--      seeker + hiding  → 全锁（连复位都锁，否则搜索者按复位就能提前出发）
--      hider  + hiding  → 正常行驶
--      seek   阶段      → 全员锁复位/传送（对齐警匪：复位=传送逃脱）
-- ---------------------------------------------------------------------------
function M.inputPolicy()
  -- 会话没了就一律放行（含 freezeDrive）：最后一道保险，见 tick 里的复位。
  if S.sessionAlive == false then
    return { roundActive = false, running = false, vehicleLock = false,
             lockReset = false, freezeDrive = false, reason = 'session-gone' }
  end
  local inRound = ROUND_PHASES[S.phase] and true or false
  local running = (S.phase == PHASE.running)
  local hidingSeeker = (running and S.stage == STAGE.hiding and S.selfRole == ROLE.seeker)

  -- 准备期（就位 / 倒数）全员锁复位；比赛期只锁搜索者 —— 躲藏者的复位
  -- 由 requestReset 兜底拒绝，留着按键还能给玩家一句「为什么不行」的提示。
  local lockReset = inRound and ((S.phase ~= PHASE.running) or S.selfRole == ROLE.seeker)

  -- 就位 / 倒数期间全员冻住（不然倒计时里能抢跑）；躲藏期的搜索者也冻。
  local freezeDrive = hidingSeeker or (inRound and not running)

  return {
    roundActive = inRound,
    running     = running,
    vehicleLock = inRound,
    lockReset   = lockReset,
    freezeDrive = freezeDrive,
    reason      = S.mode .. '/' .. S.phase .. '/' .. tostring(S.stage),
  }
end

-- ---------------------------------------------------------------------------
-- 对外：复位预算
-- ---------------------------------------------------------------------------
function M.requestReset()
  if S.phase == PHASE.running then
    if S.selfRole == ROLE.seeker then
      return false, '你是搜索者，本局禁止原地复位'
    end
    return false, '捉迷藏进行中禁止原地复位'
  end
  if ROUND_PHASES[S.phase] then
    return false, '本局准备中禁止原地复位'
  end
  return true, ''
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
function M.stage()      return S.stage end
function M.found()      return S.found or {} end

function M.phaseAgeMs()
  return math.max(0, (S.clock - S.phaseAt) * 1000)
end

function M.hidersLeft()
  return aliveHiders()
end

function M.hidersFound()
  local n = 0
  for _, p in ipairs(S.roster or {}) do
    if S.found[tostring(p.id)] then n = n + 1 end
  end
  return n
end

-- 局内阶段的剩余秒数（HUD 用）
function M.stageSecondsLeft()
  if S.phase ~= PHASE.running then return nil end
  if S.stage == STAGE.hiding then
    return math.max(0, (tonumber(TUNE.hideSeconds) or 30) - (S.clock - S.stageAt))
  end
  return math.max(0, (tonumber(TUNE.roundSeconds) or 240) - (S.clock - (S.seekAt or S.stageAt)))
end

-- ---------------------------------------------------------------------------
-- 出生方案（本模块自己规划：躲藏者沿地图道路四散，搜索者单独放远处）
--
-- 为什么不用 startrideSpawn 的走廊/围场：
--   那两条是为「警匪（一线拉开）」和「德比（围一圈）」设计的，
--   捉迷藏要的是「多点分散 + 搜索者远离所有人」，几何完全不同。
--   而且本模块已经在做位置/角色的全部计算，规划留在这里最省状态。
-- ---------------------------------------------------------------------------
local function mapNodes()
  if not map or map.getMap == nil then return nil end
  pcall(function() if map.load then map.load() end end)
  local ok, mapData = pcall(function() return map.getMap() end)
  if not ok or type(mapData) ~= 'table' then return nil end
  return mapData.nodes, mapData
end

local function collectRoadNodes(nodes)
  local out = {}
  for nodeId, node in pairs(nodes or {}) do
    if type(node) == 'table' and type(node.pos) == 'table'
       and (tonumber(node.radius) or 0) >= 3.25 then
      -- 只取「至少两个可用连接」的路点，避免把车放到断头路/停车场里
      local links = 0
      for _, link in pairs(node.links or {}) do
        if type(link) == 'table'
           and (tonumber(link.drivability) or 0) >= 0.7
           and link.oneWay ~= true
           and tostring(link.type or '') ~= 'private' then
          links = links + 1
        end
      end
      if links >= 2 then
        out[#out + 1] = { id = tostring(nodeId), x = tonumber(node.pos.x) or 0,
                          y = tonumber(node.pos.y) or 0, z = tonumber(node.pos.z) or 0 }
      end
    end
  end
  table.sort(out, function(a, b) return a.id < b.id end)
  return out
end

local function surfaceAt(x, y, zRef)
  -- ⚠️ `vec3 and vec3(...) or {...}` 是错的：在无头环境里 vec3 是 nil，
  --    而 Lua 的 and/or 链在这里恰好能兜住，但一旦 vec3 存在却返回 nil
  --    （引擎某些装配下会这样）就会悄悄退回普通表 —— 参数类型不对，
  --    与其猜，不如显式分支。
  local probe = nil
  if vec3 ~= nil then
    local ok, v = pcall(vec3, x, y, zRef + 2)
    if ok then probe = v end
  end
  if probe == nil then probe = { x = x, y = y, z = zRef + 2 } end

  local surface = nil
  pcall(function()
    if be and be.getSurfaceHeightBelow then
      surface = be:getSurfaceHeightBelow(probe)
    end
  end)
  return surface
end

-- 简易确定性打乱（Fisher-Yates + LCG）
local function shuffleList(list, seed)
  local s = math.floor(tonumber(seed) or 1)
  for i = #list, 2, -1 do
    s = (s * 1103515245 + 12345) % 2147483648
    local j = (s % i) + 1
    list[i], list[j] = list[j], list[i]
  end
  return list
end

function M.buildSpawnPlan(state)
  if type(state) ~= 'table' then return nil end

  local seed = math.floor(tonumber(state.spawnLayoutSeed) or 1)
  local nodes = mapNodes()
  if not nodes then return nil end
  local roads = collectRoadNodes(nodes)
  if #roads < 4 then return nil end

  shuffleList(roads, seed + (tonumber(state.spawnLayoutOrdinal) or 1) * 7919)

  -- 分类玩家
  local seekers, hiders = {}, {}
  for _, p in ipairs(state.players or {}) do
    local role = tostring(p.role or '')
    if role == ROLE.seeker then seekers[#seekers + 1] = p
    elseif role == ROLE.hider then hiders[#hiders + 1] = p end
  end
  if #seekers == 0 then return nil end

  local picked = {}      -- 已用坐标
  local function farEnough(x, y, minD)
    for _, q in ipairs(picked) do
      local dx, dy = x - q.x, y - q.y
      if math.sqrt(dx * dx + dy * dy) < minD then return false end
    end
    return true
  end

  local function takeNode(minD)
    for i = 1, #roads do
      local n = roads[i]
      if farEnough(n.x, n.y, minD) then
        table.remove(roads, i)
        picked[#picked + 1] = { x = n.x, y = n.y }
        return n
      end
    end
    -- 放宽：拿任意剩下的
    if #roads > 0 then
      local n = table.remove(roads, 1)
      picked[#picked + 1] = { x = n.x, y = n.y }
      return n
    end
    return nil
  end

  local slots = {}

  -- 躲藏者先挑（分散）
  for _, p in ipairs(hiders) do
    local n = takeNode(TUNE.hiderClusterMin)
    if not n then return nil end
    local surf = surfaceAt(n.x, n.y, n.z)
    if surf == nil then return nil end
    slots[#slots + 1] = {
      peerId = p.peerId,
      role = ROLE.hider,
      position = { n.x, n.y, (tonumber(surf) or n.z) + 0.35 },
      forward = { 0, 1, 0 },
    }
  end

  -- 搜索者：远离所有躲藏者
  for _, p in ipairs(seekers) do
    local n = takeNode(TUNE.seekerStandoff)
    if not n then
      n = takeNode(TUNE.hiderClusterMin)
    end
    if not n then return nil end
    local surf = surfaceAt(n.x, n.y, n.z)
    if surf == nil then return nil end
    slots[#slots + 1] = {
      peerId = p.peerId,
      role = ROLE.seeker,
      position = { n.x, n.y, (tonumber(surf) or n.z) + 0.35 },
      forward = { 0, 1, 0 },
    }
  end

  local zone = nil
  local escape = nil
  if #picked >= 2 then
    local minX, maxX, minY, maxY = picked[1].x, picked[1].x, picked[1].y, picked[1].y
    for _, q in ipairs(picked) do
      if q.x < minX then minX = q.x end
      if q.x > maxX then maxX = q.x end
      if q.y < minY then minY = q.y end
      if q.y > maxY then maxY = q.y end
    end
    local cx, cy = (minX + maxX) / 2, (minY + maxY) / 2
    local span = math.sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY)) / 2
    zone = { x = cx, y = cy, radius = math.max(tonumber(TUNE.zoneRadius) or 900, span + 300) }
    local bestNode, bestD = nil, nil
    for _, n in ipairs(roads) do
      local d = math.sqrt((n.x - cx) * (n.x - cx) + (n.y - cy) * (n.y - cy))
      if d >= span + 120 and (not bestD or d < bestD) then bestNode, bestD = n, d end
    end
    if not bestNode then
      for _, n in ipairs(roads) do
        local d = math.sqrt((n.x - cx) * (n.x - cx) + (n.y - cy) * (n.y - cy))
        if not bestD or d > bestD then bestNode, bestD = n, d end
      end
    end
    if bestNode then
      escape = { x = bestNode.x, y = bestNode.y, radius = tonumber(TUNE.escapeRadius) or 15 }
    end
  end

  S.zone = zone
  S.escape = escape

  return {
    round = state.round,
    kind = 'hideseek',
    slots = slots,
    zone = zone,
    escape = escape,
  }
end

function M.takeSpawnPlan()
  local p = S.pendingSpawnPlan
  S.pendingSpawnPlan = nil
  return p
end

function M.peekSpawnPlan() return S.pendingSpawnPlan end
function M.consumeSpawnPlan() S.pendingSpawnPlan = nil end

function M.applySpawnPlan(plan)
  if type(plan) ~= 'table' then return false end
  local r = tonumber(plan.round) or 0
  if r <= (S.lastPlanRound or -1) then return false end
  S.lastPlanRound = r
  S.pendingSpawnPlan = plan
  if type(plan.zone) == 'table' then S.zone = plan.zone end
  if type(plan.escape) == 'table' then S.escape = plan.escape end
  return true
end

-- ---------------------------------------------------------------------------
-- 就位汇报（与警匪/德比同一套「房主汇总」）
-- ---------------------------------------------------------------------------
function M.notePlacement(pid, ready)
  if type(pid) ~= 'string' or pid == '' then return end
  if ready == false then S.placementReady[pid] = nil
  else S.placementReady[pid] = true end
end

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

-- ---------------------------------------------------------------------------
-- 远端阶段同步
-- ---------------------------------------------------------------------------
function M.applyRemotePhase(phase, round, winner)
  if type(phase) ~= 'string' or phase == '' then return end
  if S.authority then return end
  if PHASE[phase] == nil then return end
  if S.phase ~= phase then
    S.phase = phase
    S.phaseAt = S.clock
    if tonumber(round) then S.round = tonumber(round) end
    if phase == PHASE.running then
      S.stage = STAGE.hiding
      S.stageAt = S.clock
    end
    logHS('跟随房主阶段 →', phase)
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

-- ---------------------------------------------------------------------------
-- 快照（HUD / 主模组取数）
-- ---------------------------------------------------------------------------
function M.snapshot()
  local dist = nil
  if S.phase == PHASE.running then
    local mx, my = vehiclePos()
    if mx then
      -- 躲藏者：显示最近的搜索者距离（躲藏者最需要知道的一件事）
      -- 搜索者：显示最近的未找到躲藏者距离
      local want = (S.selfRole == ROLE.hider) and ROLE.seeker or ROLE.hider
      local bestD = nil
      for pid, role in pairs(S.roles or {}) do
        if role == want and (want ~= ROLE.hider or not S.found[pid]) then
          local px, py = rolePos(pid)
          if px and py then
            local d = dist2(mx, my, px, py)
            if not bestD or d < bestD then bestD = d end
          end
        end
      end
      dist = bestD
    end
  end

  return {
    mode = S.mode,
    phase = S.phase,
    stage = S.stage,
    round = S.round,
    -- 给积分模块当幂等身份：同一局每帧都是这个值，跨局必然不同。
    phaseAt = S.phaseAt,
    role = S.selfRole,
    authority = S.authority,
    winner = S.winner,
    reason = S.reason,
    foundCount = M.hidersFound(),
    hidersLeft = aliveHiders(),
    hiderTotal = (function()
      local n = 0
      for _, p in ipairs(S.roster or {}) do
        if S.roles[tostring(p.id)] == ROLE.hider or S.found[tostring(p.id)] then n = n + 1 end
      end
      return n
    end)(),
    secondsLeft = (S.phase == PHASE.countdown)
      and math.max(0, math.ceil((TUNE.countdownSeconds or 5) - (S.clock - S.phaseAt))) or nil,
    stageSecondsLeft = M.stageSecondsLeft(),
    hideSeconds = TUNE.hideSeconds,
    roundSeconds = TUNE.roundSeconds,
    findRadius = TUNE.findRadius,
    findHoldMs = TUNE.findHoldMs,
    -- 本机看到的目标距离
    targetDist = dist,
    -- 发现进度：本机是搜索者用本地预测，否则用房主广播
    findTarget = (S.selfRole == ROLE.seeker) and (S.localTarget or '') or S.remoteFindTarget,
    findMs = (S.selfRole == ROLE.seeker) and (S.localMs or 0) or S.remoteFindMs,
    roles = S.roles,
    detection = (S.selfRole == ROLE.seeker) and (S.localDetection or 0) or S.detection,
    proximity = (S.selfRole == ROLE.seeker) and (S.localBand or '') or S.proximity,
    seekSecondsLeft = (S.phase == PHASE.running and S.stage ~= STAGE.hiding)
      and math.max(0, math.ceil((tonumber(TUNE.roundSeconds) or 240) - (S.clock - (S.seekAt or S.stageAt)))) or nil,
    escapeSecondsLeft = (S.phase == PHASE.running and S.stage == STAGE.seek)
      and math.max(0, math.ceil((tonumber(TUNE.escapeAfterSeconds) or 100) - (S.clock - (S.seekAt or S.stageAt)))) or nil,
    outsideSeconds = S.localOutsideSince
      and math.max(0, math.ceil((tonumber(TUNE.outsideGraceSeconds) or 25) - (S.clock - S.localOutsideSince))) or nil,
    zone = S.zone,
    escape = S.escape,
    escapedBy = S.escapedBy,
    skill = M.skillState(),
    paused = false,
  }
end

function M.tune() return TUNE end

-- ---------------------------------------------------------------------------
-- 复位（玩法切换 / 卸载）
-- ---------------------------------------------------------------------------
function M.reset()
  S.phase = PHASE.lobby
  S.stage = STAGE.hiding
  S.round = 0
  S.selfRole = ROLE.none
  S.winner = ''
  S.reason = ''
  S.roles = {}
  S.found = {}
  S.positions = {}
  S.findSince = nil
  S.findBy = ''
  S.localTarget = nil
  S.localMs = 0
  S.remoteFindTarget = ''
  S.remoteFindMs = 0
  S.retryBlockedUntil = 0
  S.placementReady = {}
  S.placementOrder = {}
  S.pendingSpawnPlan = nil
  S.lastPlanRound = -1
  S.plannedRound = -1
  S.lastAnnounce = ''
  S.lastSentKey = ''
  S.lastFoundSentKey = ''
  S.seekAt = 0
  S.detection = 0
  S.proximity = ''
  S.detectTarget = ''
  S.detectBy = ''
  S.skillReadyAt = 0
  S.skillUsed = ''
  S.shieldUntil = 0
  S.pingTarget = ''
  S.pingUntil = 0
  S.zone = nil
  S.escape = nil
  S.outside = {}
  S.escapedBy = ''
  S.localOutsideSince = nil
  S.shield = {}
  S.localDetection = 0
  S.localBand = ''
  S.pendingSkill = nil
end

return M
