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
  roundSeconds      = 240,    -- 搜索期时长（归零 = 躲藏方胜）
  findRadius        = 12.0,   -- 发现半径（米）
  findHoldMs        = 2000,   -- 贴近多久算找到（毫秒）
  findStillSpeed    = 0,      -- 躲藏者「近乎静止」阈值；0 = 不启用静止要求
  minPlayers        = 2,      -- 少于此人数不启动
  seekerCount       = 1,      -- 搜索者人数（固定 1，保留可调）
  -- 出生规划参数（本模块自规划，不走走廊/围场那两条路）
  hiderSpread       = 220,    -- 躲藏者之间期望间距（米）
  seekerStandoff    = 260,    -- 搜索者与最近躲藏者的最小距离（米）
  hiderClusterMin   = 90,     -- 躲藏者两两最小距离（米）
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

  local function num(key, lo, hi)
    local v = tonumber(rules[key])
    if v and v >= lo and v <= hi then TUNE[key] = v end
  end

  num('hideSeconds', 0, 300)
  num('hideRoundSeconds', 30, 3600)
  num('findRadius', 2, 200)
  num('findHoldMs', 200, 60000)
  num('seekerCount', 1, 8)
  num('hiderSpread', 30, 2000)
  num('seekerStandoff', 30, 3000)

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
    S.findSince = nil
    S.findBy = ''
    S.remoteFindTarget = ''
    S.remoteFindMs = 0
    S.stage = STAGE.hiding
    S.stageAt = S.clock
    S.placementReady = {}
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
  if st == STAGE.seek then
    logHS('躲藏期结束，搜索开始')
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
  local elapsed = S.clock - S.stageAt

  if S.stage == STAGE.hiding then
    if elapsed >= TUNE.hideSeconds then
      setStage(STAGE.seek)
    end
  elseif S.stage == STAGE.seek then
    if aliveHiders() <= 0 then
      S.winner = ROLE.seeker
      S.reason = 'all_found'
      setPhase(PHASE.finished, true)
      logHS('全部躲藏者被找到 —— 搜索方胜')
    elseif elapsed >= TUNE.roundSeconds then
      S.winner = ROLE.hider
      S.reason = 'time_up'
      setPhase(PHASE.finished, true)
      logHS('搜索时间耗尽 —— 躲藏方胜')
    end
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

local function tickFindAuthority()
  if S.phase ~= PHASE.running then return end
  if S.stage ~= STAGE.seek then return end

  -- 找「离某个搜索者最近、且在半径内」的躲藏者
  local target, targetSeeker, targetD = nil, nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local hx, hy = rolePos(pid)
      if hx and hy then
        local sk, d = nearestSeekerTo(hx, hy)
        if sk and d and d <= TUNE.findRadius then
          if not targetD or d < targetD then
            target, targetSeeker, targetD = pid, sk, d
          end
        end
      end
    end
  end

  if not target then
    -- 没人被贴住：进度清零（不做衰减）
    if S.findSince then
      S.findSince = nil
      S.findBy = ''
    end
    return
  end

  -- 换了目标 → 重新计时
  if S.findBy ~= target then
    S.findSince = S.clock
    S.findBy = target
    return
  end

  local heldMs = (S.clock - (S.findSince or S.clock)) * 1000
  if heldMs >= TUNE.findHoldMs then
    S.found[target] = true
    -- 被找到的人转成搜索者（BeamLink：被找到的人加入搜索方）
    if S.roles[target] == ROLE.hider then S.roles[target] = ROLE.seeker end
    S.findSince = nil
    S.findBy = ''
    logHS(tostring(target) .. ' 被找到（贴住 '
      .. string.format('%.1f', heldMs / 1000) .. ' 秒）→ 转入搜索方')
    -- 立刻算一次胜负（最后一个被找到 → 直接结算）
    if aliveHiders() <= 0 then
      S.winner = ROLE.seeker
      S.reason = 'all_found'
      setPhase(PHASE.finished, true)
    end
  end
end

-- 本地（搜索者）预测：本机贴近某个躲藏者时给出进度，HUD 不必等房主广播。
local function tickFindLocal()
  if S.phase ~= PHASE.running then return end
  if S.stage ~= STAGE.seek then return end
  if S.selfRole ~= ROLE.seeker then
    if S.localTarget then S.localTarget = nil; S.localMs = 0 end
    return
  end

  local mx, my = vehiclePos()
  if not mx then return end

  local best, bestD = nil, nil
  for pid, role in pairs(S.roles or {}) do
    if role == ROLE.hider and not S.found[pid] then
      local px, py = rolePos(pid)
      if px and py then
        local d = dist2(mx, my, px, py)
        if d <= TUNE.findRadius and (not bestD or d < bestD) then best, bestD = pid, d end
      end
    end
  end

  if not best then
    S.localTarget = nil
    S.localMs = 0
    return
  end
  if S.localTarget ~= best then
    S.localTarget = best
    S.localSince = S.clock
  end
  S.localMs = (S.clock - (S.localSince or S.clock)) * 1000
  S.localDist = bestD
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
local function tickAuthority(win)
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
    tickFindAuthority()

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
  if not win.connected or win.closed then
    S.authority = false
  else
    local h = tostring(win.host or '')
    if h == '' then
      S.authority = false
    else
      S.authority = (h == tostring(win.selfId or '')) or (h == tostring(win.playerName or ''))
    end
  end

  pcall(tickPositions)

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

  tickAuthority(win)

  pcall(tickFindLocal)

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
  local inRound = ROUND_PHASES[S.phase] and true or false
  local running = (S.phase == PHASE.running)
  local hidingSeeker = (running and S.stage == STAGE.hiding and S.selfRole == ROLE.seeker)

  local lockReset = (inRound and running and S.selfRole == ROLE.seeker)
    or (running and S.stage == STAGE.hiding and S.selfRole == ROLE.seeker)

  return {
    roundActive = inRound,
    running     = running,
    vehicleLock = inRound,
    lockReset   = lockReset,
    -- 躲藏期的搜索者连油门都不能给（由主模组读取这个字段做油门封锁）
    freezeDrive = hidingSeeker,
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
    return math.max(0, TUNE.hideSeconds - (S.clock - S.stageAt))
  end
  return math.max(0, TUNE.roundSeconds - (S.clock - S.stageAt))
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

  return {
    round = state.round,
    kind = 'hideseek',
    slots = slots,
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
end

return M
