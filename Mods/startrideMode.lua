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
  -- 模式准入限制（进入游戏后的硬性限制；0 / '' = 不限制）
  zoneRadius         = 0,     -- 活动区域半径（米；0=不限）。与躲猫猫共用同一口径
  outsideGraceSeconds = 5,    -- 出圈宽限（秒）：超时就按 zoneEliminate 处罚
  zoneEliminate      = 0,     -- 出圈超时处罚：0=拉回半径边缘，1=淘汰/判负
  vehicleTypes       = '',    -- 允许车型（逗号分隔子串，空=不限），按 jbeam 文件名匹配
  vehicleEnforce     = 1,     -- 车型不符时强制替换（0=只警告不替换）
  -- 严格度预设：'' = 跟随各模式自己的口径，其余 = 房主统一指定
  --   loose / standard / strict / off（见下方 PRESET 表）
  -- 只替换「预设」这一层，不覆盖模式级语义例外（比如德比始终允许复位）。
  rulePreset         = '',
  -- 细粒度玩法规则：nil = 跟随模式默认（MODE_RULES / PRESET），房主显式配了才固定。
  -- ⚠️ 只收「引擎侧真能被强制」的规则：拦不住的不许写进来（HUD 会当真事告诉玩家）。
  -- ① 位置 / 场地
  noMapTeleport      = nil,   -- 禁止大地图点击传送
  noHomeAnchor       = nil,   -- 禁止保存 / 读取位置锚点
  noDropAtCam        = nil,   -- 禁止把车落在相机位置
  -- ② 时间 / 节奏
  neutralLock        = nil,   -- 起跑前空档锁定 + 起跑后自动挂一档
  freezeStaging      = nil,   -- 就位 / 倒数期间冻结油门
  noPause            = nil,   -- 禁止暂停游戏
  noSlowMotion       = nil,   -- 禁止慢动作 / 时间缩放
  roundLimit         = nil,   -- 回合时限强制生效
  -- ③ 车辆 / 装备
  noVehicleSwap      = nil,   -- 禁止换车 / 切到他人车辆
  noPartsSwap        = nil,   -- 禁止换零件 / 调校
  noRepair           = nil,   -- 禁止修复车辆 / 加油
  noTrailer          = nil,   -- 禁止挂接拖车
  sameModel          = nil,   -- 全员同款车型
  -- ④ 视野 / 信息
  noFreeCam          = nil,   -- 禁止自由相机（脱离车身看全场）
  noPhotoMode        = nil,   -- 禁止拍照模式
  noWalkMode         = nil,   -- 禁止下车步行
  noMinimap          = nil,   -- 禁止开启小地图
  -- ⑤ 功能 / 作弊
  noManualReset      = nil,   -- 局内禁止手动复位车辆
  noFun              = nil,   -- 局内禁止娱乐功能（力场 / 加速 / 烧胎）
  noAiTraffic        = nil,   -- 局内禁止开关 AI 交通
  noAiMode           = nil,   -- 局内禁止 AI 代驾
  noNodeGrabber      = nil,   -- 局内禁止节点抓取（能拖走他人车辆）
  noWorldEditor      = nil,   -- 局内禁止世界编辑器
  noSpawnVehicle     = nil,   -- 局内禁止生成新车辆
  noRadialSandbox    = nil,   -- 局内禁止沙盒径向菜单
  noDebugMenu        = nil,   -- 局内禁止车辆调试菜单
  noWeather          = nil,   -- 局内禁止改天气 / 时间
  blockGroups        = '',    -- 额外屏蔽的按键组（逗号分隔：fun/vehicle/ai/reset/resetBypass/teleport/menuReset）
  allowGroups        = '',    -- 放行的按键组（同上；警匪的 lockReset 除外，不可放行）
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

  -- 模式准入：活动区域（每回合用 round 当戳，天然跟着新局失效；
  -- 加入者跟房主的 round 是同一个号，两边各记各的、互不干扰）
  zoneRound    = -1,          -- 上面那套区域状态属于哪个回合
  zonePlan     = nil,         -- 出生方案推导的全员共享圈 {x, y, radius}
  zoneFallback = nil,         -- 没有方案时退化成「本机出生位」的圈
  zoneSince    = nil,         -- 本机出圈的起点时刻
  zoneOut      = false,       -- 本机当前是否出圈（HUD 用）
  zonePullAt   = 0,           -- 上次拉回尝试时刻（节流，避免每帧硬拽）
  outside      = {},          -- [playerId] = 出圈起点（房主判远端用）

  -- 模式准入：车型白名单
  vehRound     = -1,          -- 车型状态属于哪个回合
  vehicleOk    = true,        -- 当前车型是否命中白名单
  vehicleName  = '',          -- 当前车型 jbeam 文件名（小写）
  vehicleTries = 0,           -- 本回合已尝试强制替换次数
  vehicleAt    = 0,           -- 上次车型检查/替换尝试时刻
  vehSet       = nil,         -- 白名单解析缓存（集合表）
  vehSetSig    = nil,         -- 缓存对应的原始串
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
-- 模式适配包：每个非自由模式自带的「功能限制 + 位置限制」
-- ---------------------------------------------------------------------------
-- 规则本体在 startrideModePacks.lua（数据表），这里只做三件事：
--   ① 把包里的模式底线 / 显式项预展开成查表（每帧不再现算）
--   ② 把包里的位置限制（活动圈半径 / 出圈宽限）接到区域判定上
--   ③ 给 HUD 一份结构化描述
-- ⚠️ 包加载失败不能拖垮玩法：全部走 nil 兜底，退回旧行为（只有 preset）。
local function loadModePacks()
  local ok, mod = pcall(require, 'startrideModePacks')
  if ok and type(mod) == 'table' then return mod end
  return nil
end
local SRPACK = loadModePacks()

-- 前向声明：规则求值器在文件后半段才定义（要读 PRESET / MODE_RULES），
-- 但区域判定（tickZone 一带）已经要用它 —— 必须在这里占位。
local ruleOn

-- MODE_HARD[mode][key]     = 模式底线（房主也关不掉）
-- MODE_EXPLICIT[mode][key] = 模式自己写明的键（不受房主预设影响）
-- MODE_DESC[mode]          = 给 HUD / 日志的结构化描述
local MODE_HARD, MODE_EXPLICIT, MODE_DESC = {}, {}, {}

-- 模式包里的位置限制（无包 / 自由驾驶返回 nil）
local function modeZone()
  if not SRPACK then return nil end
  local ok, z = pcall(SRPACK.zone, S.mode)
  if ok and type(z) == 'table' and (tonumber(z.radius) or 0) > 0 then return z end
  return nil
end

-- 有效活动圈半径：模式包给基线，房主配置只能「更严」（更小）才算数。
-- 0 = 这个模式不设活动范围。
local function modeZoneRadius()
  local tune = tonumber(TUNE.zoneRadius) or 0
  if SRPACK then
    local ok, r = pcall(SRPACK.zoneRadius, S.mode, tune)
    if ok and tonumber(r) then return tonumber(r) end
  end
  return tune
end

-- 出圈宽限秒数：模式包给的值只在房主没改过默认（5 秒）时生效
local function modeGrace()
  local g = tonumber(TUNE.outsideGraceSeconds) or 5
  if g ~= 5 then return g end
  local z = modeZone()
  local pg = (type(z) == 'table' and tonumber(z.grace)) or 0
  if pg > 0 then return pg end
  return g
end

-- 这个模式要不要判出圈（有位置限制就算）
local function modeHasZone()
  if modeZone() then return true end
  return modeZoneRadius() > 0
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
  num('zoneRadius', 0, 20000)
  num('outsideGraceSeconds', 1, 600)
  num('zoneEliminate', 0, 1)
  num('vehicleEnforce', 0, 1)
  -- 规则白名单：新加一条规则必须同时出现在 TUNE / 这里 / RULE_DEFS 三处，
  -- 少一处就是「配置写了但没人读」的静默失效（_sr_audit_contracts.py 会抓）。
  num('noMapTeleport', 0, 1)
  num('noHomeAnchor', 0, 1)
  num('noDropAtCam', 0, 1)
  num('neutralLock', 0, 1)
  num('freezeStaging', 0, 1)
  num('noPause', 0, 1)
  num('noSlowMotion', 0, 1)
  num('roundLimit', 0, 1)
  num('noVehicleSwap', 0, 1)
  num('noPartsSwap', 0, 1)
  num('noRepair', 0, 1)
  num('noTrailer', 0, 1)
  num('sameModel', 0, 1)
  num('noFreeCam', 0, 1)
  num('noPhotoMode', 0, 1)
  num('noWalkMode', 0, 1)
  num('noMinimap', 0, 1)
  num('noManualReset', 0, 1)
  num('noFun', 0, 1)
  num('noAiTraffic', 0, 1)
  num('noAiMode', 0, 1)
  num('noNodeGrabber', 0, 1)
  num('noWorldEditor', 0, 1)
  num('noSpawnVehicle', 0, 1)
  num('noRadialSandbox', 0, 1)
  num('noDebugMenu', 0, 1)
  num('noWeather', 0, 1)
  -- 严格度预设：只认四个合法值，写错就继续保持「跟随模式」
  if type(rules.preset) == 'string' then
    local pv = string.lower(rules.preset)
    if pv == 'loose' or pv == 'standard' or pv == 'strict' or pv == 'off' then
      TUNE.rulePreset = pv
    elseif pv == '' then
      TUNE.rulePreset = ''
    end
  end
  if type(rules.vehicleTypes) == 'string' then TUNE.vehicleTypes = rules.vehicleTypes end
  if type(rules.blockGroups) == 'string' then TUNE.blockGroups = rules.blockGroups end
  if type(rules.allowGroups) == 'string' then TUNE.allowGroups = rules.allowGroups end

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

-- ---------------------------------------------------------------------------
-- 模式准入：活动区域（位置限制）
--
-- 口径与躲猫猫一致：全员共享一个圈 —— 圆心取出生方案的包围盒中心，
-- 半径取配置值，但不小于出生跨度 + 100m（谁都不能出生在圈外）。
-- 方案本来就广播给全员，圆心天然一致；方案缺失时退化成「本机出生位」
-- 的圈，保证限制永远有一个基准。
--
-- 处罚两种（zoneEliminate）：
--   0 = 宽限过后把本机拉回半径边缘（贴地、车头朝圈内）
--   1 = 宽限过后出局 —— 德比按撞毁淘汰记账；警匪只有强盗出圈才判负
--       （警察出圈照样拉回：警察只是工具人，不该被圈罚掉）
-- 权威性：本机那格由 tickZone 本地即时处理（和冻油门同一个思路）；
-- 房主另用 tickZoneRemote 替远端记账（位置走网络缓存，与抓捕判定同一份）。
-- ---------------------------------------------------------------------------

-- 逗号分隔清单 → 小写集合表（去空白去空项）
local function parseSet(str)
  local set = {}
  if type(str) ~= 'string' then return set end
  for item in string.gmatch(str, '[^,]+') do
    local key = string.lower(item)
    key = string.gsub(key, '^%s+', '')
    key = string.gsub(key, '%s+$', '')
    if key ~= '' then set[key] = true end
  end
  return set
end

-- 从出生方案推导全员共享的圈。plan.slots[].position = {x, y, z}
local function deriveZoneFromPlan(plan)
  if type(plan) ~= 'table' or type(plan.slots) ~= 'table' then return nil end
  local n, minX, maxX, minY, maxY = 0, nil, nil, nil, nil
  for _, s in ipairs(plan.slots) do
    local p = s.position
    if type(p) == 'table' and tonumber(p[1]) and tonumber(p[2]) then
      local x, y = tonumber(p[1]), tonumber(p[2])
      if not minX then
        minX, maxX, minY, maxY = x, x, y, y
      else
        if x < minX then minX = x elseif x > maxX then maxX = x end
        if y < minY then minY = y elseif y > maxY then maxY = y end
      end
      n = n + 1
    end
  end
  if n == 0 or not minX then return nil end
  local cx, cy = (minX + maxX) / 2, (minY + maxY) / 2
  local span = math.sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY)) / 2
  -- 位置限制：半径取「模式包基线 / 房主更严值」，再保证把所有出生点都包进来，
  -- 最后用模式的 radiusFloor 兜底（小场地别把圈压没）。
  local r = modeZoneRadius()
  if r <= 0 or r < span + 100 then r = span + 100 end
  local z = modeZone()
  local floorR = (type(z) == 'table' and tonumber(z.radiusFloor)) or 0
  if floorR > 0 and r < floorR then r = floorR end
  return { x = cx, y = cy, radius = r }
end

-- 记下本回合的全员共享圈（房主/加入者都走这里）。
-- 只在「方案属于当前回合或更新」时记；同一个圈不重复记（摆放层每帧都会调）。
local function noteZonePlan(plan)
  if type(plan) ~= 'table' then return false end
  local r = tonumber(plan.round) or -1
  if r < S.round then return false end          -- 过期方案不要
  local zone = deriveZoneFromPlan(plan)
  if not zone then return false end
  local old = S.zonePlan
  if old and math.abs(old.x - zone.x) < 0.5 and math.abs(old.y - zone.y) < 0.5
     and math.abs((old.radius or 0) - zone.radius) < 0.5 then
    return true
  end
  S.zonePlan = zone
  S.zoneFallback = nil
  logMode('活动区域：圆心 ' .. string.format('%.0f,%.0f', zone.x, zone.y)
    .. ' 半径 ' .. string.format('%.0f', zone.radius) .. 'm')
  return true
end
M.noteZonePlan = noteZonePlan
M.deriveZoneFromPlan = deriveZoneFromPlan

-- 本回合的圈（优先方案圈，退化到本机出生位圈）
local function currentZone()
  if S.zonePlan then return S.zonePlan end
  return S.zoneFallback
end

-- 每回合把区域状态滚掉（round 当戳：房主 staging 时 round+1，
-- 加入者跟房主 round；mode 切走时 round 归 0 也会滚）
local function rollZoneState()
  if S.zoneRound == S.round then return end
  S.zoneRound = S.round
  S.zoneSince = nil
  S.zoneOut = false
  S.zonePullAt = 0
  S.outside = {}
end

-- 贴地高度（拿不到就退回 fallback）
local function groundZ(x, y, fallback)
  local z = fallback or 0
  pcall(function()
    if be and be.getSurfaceHeightBelow then
      local h = be:getSurfaceHeightBelow(vec3(x, y, (fallback or 0) + 2))
      if tonumber(h) then z = tonumber(h) end
    end
  end)
  return z
end

-- 把本机拉回半径边缘（85% 处，留余量免得下一帧又判出圈）。
-- 返回 true = 拉回来了（调用方据此重置出圈计时）。
local function pullBackToZone(radius)
  local zone = currentZone()
  if not zone then return false end
  local x, y = vehiclePos('self')
  if not x then return false end
  local dx, dy = x - zone.x, y - zone.y
  local d = math.sqrt(dx * dx + dy * dy)
  if d < 0.01 then return false end
  local rr = radius * 0.85
  local nx, ny = zone.x + dx / d * rr, zone.y + dy / d * rr

  local veh = nil
  pcall(function()
    if be and be.getPlayerVehicle then veh = be:getPlayerVehicle(0) end
  end)
  if not veh then return false end

  local cz = nil
  pcall(function() local p = veh:getPosition(); if p then cz = tonumber(p.z) end end)
  local z = groundZ(nx, ny, cz or 0) + 0.2

  -- 朝向圈心（和出生摆放同一套算法：水平朝向 → 四元数）
  local rot = nil
  pcall(function()
    local fx, fy = zone.x - nx, zone.y - ny
    local fl = math.sqrt(fx * fx + fy * fy)
    if fl < 1e-4 then fx, fy = 0.0, 1.0 else fx, fy = fx / fl, fy / fl end
    local c = fx
    if c > 1 then c = 1 elseif c < -1 then c = -1 end
    local hw = math.sqrt((1 + c) / 2)
    local hs = math.sqrt((1 - c) / 2)
    if fy < 0 then hs = -hs end
    if quat ~= nil then rot = quat(0, 0, hs, hw) else rot = { x = 0, y = 0, z = hs, w = hw } end
  end)
  if rot == nil then return false end

  local moved = false
  pcall(function()
    if spawn ~= nil and spawn.safeTeleport ~= nil then
      spawn.safeTeleport(veh, vec3(nx, ny, z), rot, nil, nil, nil, true, true, nil, nil)
      moved = true
    end
  end)
  if not moved then
    pcall(function()
      veh:setPosRot(nx, ny, z, rot.x, rot.y, rot.z, rot.w)
      moved = true
    end)
  end
  if not moved then return false end

  -- 通知引擎这辆车换位置了（相机 / 悬挂 / 跟随逻辑靠它复位）
  local vehId = nil
  pcall(function() vehId = veh:getID() end)
  pcall(function()
    if extensions ~= nil and extensions.hook ~= nil and vehId ~= nil then
      extensions.hook('onVehicleResetted', vehId)
    end
  end)
  return true
end

-- 本机出圈处罚（宽限已过）
local function zonePenaltySelf(radius)
  if ruleOn and ruleOn('zoneEliminate') then
    if S.mode == 'derby' then
      if not S.eliminated[S.selfId or 'self'] then
        S.eliminated[S.selfId or 'self'] = true
        S.hitNotice = { self = true, name = '', speed = 0, wrecked = true }
        logMode('离开活动区域超时，本机出局')
      end
      return
    end
    -- 警匪：只有强盗出圈才判负（警察出圈走下面的拉回）
    if S.selfRole == ROLE.robber and not S.captureDone then
      S.captureDone = true
      S.winner = ROLE.cop
      S.finishReason = 'robber_outside'
      if S.authority then setPhase(PHASE.finished, true) else S.phase = PHASE.finished end
      logMode('强盗离开活动区域超时 —— 警察胜')
      return
    end
  end
  if (S.clock - (S.zonePullAt or 0)) < 0.5 then return end
  S.zonePullAt = S.clock
  if pullBackToZone(radius) then
    S.zoneSince = nil
    S.zoneOut = false
    logMode('离开活动区域超时，已拉回边界')
  end
end

-- 本机区域判定（每帧）
local function tickZone()
  local radius = modeZoneRadius()
  if radius <= 0 then
    S.zoneOut = false
    S.zoneSince = nil
    return
  end
  -- 位置限制对所有「有活动圈」的模式生效（警匪 / 德比 / 捉迷藏 / 赛道日）；
  -- 以前写死成前两个 —— 捉迷藏跑出图、赛道日抄近道都没人管。
  if not modeHasZone() then
    S.zoneOut = false
    S.zoneSince = nil
    return
  end
  if S.phase ~= PHASE.running then
    S.zoneOut = false
    S.zoneSince = nil
    return
  end
  rollZoneState()

  local me = S.selfId or 'self'
  -- 已出局 / 已结束的人不再管（德比淘汰后车是冻着的；警匪抓到后直接 finished）
  if S.mode == 'derby' and S.eliminated[me] then return end
  if S.mode == 'cops_robber' and S.captureDone then return end

  local zone = currentZone()
  if not zone then
    -- 没有出生方案：用本机当前位置当圆心（只记一次，之后就是本局的圈）
    if not S.zoneFallback then
      local x, y = vehiclePos('self')
      if x then S.zoneFallback = { x = x, y = y, radius = radius } end
    end
    zone = S.zoneFallback
  end
  if not zone then return end

  local x, y = vehiclePos('self')
  if not x then return end
  if dist2(x, y, zone.x, zone.y) <= zone.radius then
    if S.zoneOut then
      S.zoneOut = false
      logMode('已返回活动区域')
    end
    S.zoneSince = nil
    return
  end

  if not S.zoneSince then
    S.zoneSince = S.clock
    S.zoneOut = true
    logMode('离开活动区域，' .. tostring(math.floor(modeGrace())) .. ' 秒后处罚')
  end
  if (S.clock - S.zoneSince) < modeGrace() then return end
  zonePenaltySelf(zone.radius)
end

-- 房主侧：替远端玩家判出圈（位置走网络缓存，与抓捕判定同一份数据）。
-- 本机那格由 tickZone 本地即时处理，这里跳过。
local function tickZoneRemote()
  if not S.authority then return end
  local radius = modeZoneRadius()
  if radius <= 0 then return end
  if S.phase ~= PHASE.running then return end
  if not modeHasZone() then return end

  local zone = currentZone()
  if not zone then return end
  local grace = modeGrace()
  local me = S.selfId or 'self'
  local marks = S.outside

  -- ⚠️ 循环变量不能赋值（Lua 5.5 里是 const，编译期直接挂）——用新名字接。
  for pidRaw, role in pairs(S.roles or {}) do
    local pid = tostring(pidRaw)
    if pid ~= me then
      local px, py = rolePos(pid)
      if px and py then
        if dist2(px, py, zone.x, zone.y) > zone.radius then
          if not marks[pid] then
            marks[pid] = S.clock
          elseif (S.clock - marks[pid]) >= grace then
            marks[pid] = nil
            if S.mode == 'derby' then
              if not S.eliminated[pid] then
                S.eliminated[pid] = true
                logMode('远端离开活动区域超时，按出局处理:', pid)
              end
            elseif role == ROLE.robber and not S.captureDone then
              S.captureDone = true
              S.winner = ROLE.cop
              S.finishReason = 'robber_outside'
              setPhase(PHASE.finished, true)
              logMode('强盗离开活动区域超时 —— 警察胜')
            end
          end
        else
          marks[pid] = nil
        end
      end
    end
  end
end

-- ---------------------------------------------------------------------------
-- 模式准入：车型白名单
--
-- 按 jbeam 文件名做子串匹配（大小写不敏感）—— 车型文件名千奇百怪
-- （van_petrolhauler.pc / pickup.pc），子串比全等好用得多。
-- 不符时：vehicleEnforce=1 → 用白名单第一项替换（core_vehicles.replaceVehicle，
-- pcall 兜底）；换不成功（车型名不存在 / API 没有）就本回合只警告不硬挡，
-- 并提示玩家自己换 —— 宁可降级也不把玩家卡在错误的车里。
-- ---------------------------------------------------------------------------

-- 规则查询前向声明：真正的实现在「玩法规则目录」一节（在 tickVehicle 之后），
-- 这里必须先占一个 local，否则 tickVehicle 里引用到的是全局 nil。
-- ruleOn 已在上方（模式适配包一节）前向声明，这里不再重复 local（会另起变量）。

-- 当前车型文件名（小写）；拿不到返回 nil = 未知（不阻断）
local function selfJbeam()
  local name = nil
  pcall(function()
    local v = be:getPlayerVehicle(0)
    if v and v.getJBeamFilename then name = tostring(v:getJBeamFilename() or '') end
  end)
  if not name or name == '' then return nil end
  return string.lower(name)
end

-- 白名单命中？空清单 = 不限；拿不到车型名 = 不阻断（和速度判定一个口径）
local function vehicleAllowed(set, name)
  if not set or next(set) == nil then return true end
  if not name then return true end
  for key in pairs(set) do
    if string.find(name, key, 1, true) then return true end
  end
  return false
end

-- 用白名单第一项试替换（返回 true = 调用没抛错；真假下一帧复查）
local function tryReplaceVehicle(set)
  local first = nil
  for k in pairs(set) do first = k break end
  if not first then return false end
  local ok = pcall(function()
    if core_vehicles ~= nil and core_vehicles.replaceVehicle ~= nil then
      core_vehicles.replaceVehicle(first)
    end
  end)
  return ok
end

-- 每回合滚掉车型状态（round 当戳，和区域同一套）
local function rollVehicleState()
  if S.vehRound == S.round then return end
  S.vehRound = S.round
  S.vehicleOk = true
  S.vehicleName = ''
  S.vehicleTries = 0
  S.vehicleAt = 0
  S.vehSet = nil
  S.vehSetSig = nil
end

-- 车型校验 + 强制替换（开局立刻查一次，之后每 1 秒复查）
local function tickVehicle()
  -- 只要有白名单或「全员同款」就要跑：enforce=0 时也要跑 —— 那时要做的是
  -- 「只警告不替换」，不是「什么都不做」（不跑就永远不告诉玩家车型不符）。
  local sameOn = ruleOn('sameModel')
  local hasList = type(TUNE.vehicleTypes) == 'string' and TUNE.vehicleTypes ~= ''
  if not sameOn and not hasList then return end
  if S.phase ~= PHASE.running then return end
  rollVehicleState()

  -- 车型集合：全员同款（房主广播的车型）> 白名单
  -- ⚠️ 同款车的基准是**房主自己的车型**，不是配置里写的串 —— 房主换车时基准自然跟着走。
  local sig, set
  if sameOn and type(S.requiredModel) == 'string' and S.requiredModel ~= '' then
    sig = '@same:' .. S.requiredModel
    set = parseSet(S.requiredModel)
  else
    sig = '@set:' .. tostring(TUNE.vehicleTypes)
  end
  if S.vehSetSig ~= sig then
    S.vehSetSig = sig
    S.vehSet = set or parseSet(TUNE.vehicleTypes)
  end
  if not S.vehSet or next(S.vehSet) == nil then
    S.vehicleOk = true
    return
  end

  if S.vehicleAt > 0 and (S.clock - S.vehicleAt) < 1 then return end
  S.vehicleAt = S.clock

  local name = selfJbeam()
  if name then S.vehicleName = name end
  if vehicleAllowed(S.vehSet, name) then
    if not S.vehicleOk then
      S.vehicleOk = true
      logMode('车型已符合要求：' .. tostring(name))
    end
    return
  end

  S.vehicleOk = false
  if not ruleOn('vehicleEnforce') then
    logMode('车型不在允许列表（当前 ' .. tostring(name) .. '），请自行更换')
    return
  end
  if S.vehicleTries >= 3 then
    logMode('车型不在允许列表，自动替换多次未成功，请自行更换为：' .. tostring(TUNE.vehicleTypes))
    return
  end
  S.vehicleTries = S.vehicleTries + 1
  logMode('车型不在允许列表（当前 ' .. tostring(name) .. '）→ 尝试替换为 ' .. tostring(TUNE.vehicleTypes))
  tryReplaceVehicle(S.vehSet)
end

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
  -- 模式准入：活动区域（本机即时 + 房主替远端记账）、车型白名单
  pcall(tickZone)
  pcall(tickZoneRemote)
  pcall(tickVehicle)

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
-- ---------------------------------------------------------------------------
-- 细粒度玩法规则：按模式给一套默认，房主可在启动器里逐条覆盖
--
-- 之前只有「整组屏蔽按键」这种粗口径，玩家感知就是「一堆键莫名其妙不能用」。
-- 这里改成**看得见说得清的规则条目**：每条都对应一条具体的玩法限制，
-- 且 HUD 会把它列出来（snapshot.rules），玩家一眼知道自己被限了什么、为什么。
-- ---------------------------------------------------------------------------
-- 分类标题（HUD 分栏用）
local CAT_TEXT = {
  zone    = '位置',
  time    = '时间',
  vehicle = '车辆',
  view    = '视野',
  func    = '功能',
}

-- 规则目录：键 / 分类 / 中文说明。
-- ⚠️ 只收**引擎侧真能被强制**的规则。拦不住的宁可不写 ——
--    HUD 上列一条实际不起作用的规则，比不列更糟。
local RULE_DEFS = {
  -- ① 位置 / 场地
  { 'zoneEliminate',  'zone',    '出圈超时直接淘汰' },
  { 'noMapTeleport',  'zone',    '禁止大地图点击传送' },
  { 'noHomeAnchor',   'zone',    '禁止保存 / 读取位置锚点' },
  { 'noDropAtCam',    'zone',    '禁止把车落在相机位置' },
  -- ② 时间 / 节奏
  { 'neutralLock',    'time',    '起跑前空档锁定 · 起跑后自动挂一档' },
  { 'freezeStaging',  'time',    '就位 / 倒数期间冻结油门' },
  { 'noPause',        'time',    '禁止暂停游戏' },
  { 'noSlowMotion',   'time',    '禁止慢动作 / 时间缩放' },
  { 'roundLimit',     'time',    '回合时限强制生效' },
  -- ③ 车辆 / 装备
  { 'noVehicleSwap',  'vehicle', '禁止换车 / 切到他人车辆' },
  { 'noPartsSwap',    'vehicle', '禁止换零件 / 调校' },
  { 'noRepair',       'vehicle', '禁止修复车辆 / 加油' },
  { 'noTrailer',      'vehicle', '禁止挂接拖车' },
  { 'vehicleEnforce', 'vehicle', '车型不符强制替换' },
  { 'sameModel',      'vehicle', '全员同款车型' },
  -- ④ 视野 / 信息
  { 'noFreeCam',      'view',    '禁止自由相机' },
  { 'noPhotoMode',    'view',    '禁止拍照模式' },
  { 'noWalkMode',     'view',    '禁止下车步行' },
  { 'noMinimap',      'view',    '禁止开启小地图' },
  -- ⑤ 功能 / 作弊
  { 'noManualReset',  'func',    '禁止手动复位车辆' },
  { 'noFun',          'func',    '禁止娱乐功能（力场 / 加速 / 烧胎）' },
  { 'noAiTraffic',    'func',    '禁止 AI 车流' },
  { 'noAiMode',       'func',    '禁止 AI 代驾' },
  { 'noNodeGrabber',  'func',    '禁止节点抓取（可拖走他人车辆）' },
  { 'noWorldEditor',  'func',    '禁止世界编辑器' },
  { 'noSpawnVehicle', 'func',    '禁止生成新车辆' },
  { 'noRadialSandbox','func',    '禁止沙盒径向菜单' },
  { 'noDebugMenu',    'func',    '禁止车辆调试菜单' },
  { 'noWeather',      'func',    '禁止修改天气 / 时间' },
}

-- 严格度预设：模式只需要挂一个预设 + 少量覆盖，不用逐条写满 29 个键
local PRESET = {
  off = {},
  -- 宽松：只挡「传送 / 抢跑 / 慢动作」这类直接破坏公平的手段，驾驶自由不动
  loose = { noMapTeleport = 1, noHomeAnchor = 1, noDropAtCam = 1,
            neutralLock = 1, noSlowMotion = 1,
            noSpawnVehicle = 1, noRadialSandbox = 1 },
  -- 标准：竞技口径（复位 / 换车 / 相机 / 娱乐 / AI / 编辑器全收）
  standard = { noMapTeleport = 1, noHomeAnchor = 1, noDropAtCam = 1,
               neutralLock = 1, freezeStaging = 1, noPause = 1,
               noSlowMotion = 1, roundLimit = 1,
               noVehicleSwap = 1, noPartsSwap = 1, noRepair = 1,
               vehicleEnforce = 1,
               noFreeCam = 1, noWalkMode = 1,
               noManualReset = 1, noFun = 1, noAiTraffic = 1, noAiMode = 1,
               noNodeGrabber = 1, noWorldEditor = 1, noSpawnVehicle = 1,
               noRadialSandbox = 1, noDebugMenu = 1 },
}

-- 严格 = 标准 + 信息面（拍照 / 小地图）、拖车、同款车、天气
do
  local s = {}
  for k, v in pairs(PRESET.standard) do s[k] = v end
  s.noPhotoMode = 1
  s.noMinimap   = 1
  s.noTrailer   = 1
  s.sameModel   = 1
  s.noWeather   = 1
  PRESET.strict = s
end

-- 模式默认：preset + 模式级显式覆盖（数字键直接盖掉预设）
local MODE_RULES = {
  -- 警匪追逐：最严口径。复位 = 原地逃脱，必须禁
  cops_robber = { preset = 'strict' },
  -- 德比：翻车要能爬起来 → 留复位（走预算 + 冷却）；车撞烂了要能修，否则秒出局
  derby       = { preset = 'strict', noManualReset = 0, sameModel = 0, noRepair = 0 },
  -- 捉迷藏：躲藏期搜索者被冻住；车型不统一（躲的人越多越好藏）
  hide_seek   = { preset = 'strict', sameModel = 0 },
  -- 赛道日：只管起跑程序与娱乐功能，换车 / 相机随便
  track_day   = { preset = 'loose', noManualReset = 1, noRepair = 1 },
  -- 自由驾驶：不设任何限制
  free_drive  = { preset = 'off' },
}

-- 模式包预展开：把 startrideModePacks 里每个模式的 hard / explicit / 描述取出来。
-- 包缺失（旧安装 / require 失败）时这三张表是空的 → ruleOn 自然退回手写的 MODE_RULES。
do
  if SRPACK then
    local okAll, modes = pcall(SRPACK.modes)
    if okAll and type(modes) == 'table' then
      local i
      for i = 1, #modes do
        local m = modes[i]
        local okH, h = pcall(SRPACK.hard, m)
        if okH and type(h) == 'table' then MODE_HARD[m] = h end
        local okE, e = pcall(SRPACK.explicit, m)
        if okE and type(e) == 'table' then MODE_EXPLICIT[m] = e end
        local okD, d = pcall(SRPACK.describe, m)
        if okD and type(d) == 'table' then MODE_DESC[m] = d end
        local okP, pn = pcall(SRPACK.preset, m)
        if okP and type(pn) == 'string' and type(MODE_RULES[m]) == 'table' then
          MODE_RULES[m].preset = pn
        end
      end
    end
  end
end

-- 由 RULE_DEFS 派生三个查表（避免三处手抄不一致）
local RULE_TEXT, RULE_CAT, RULE_ORDER = {}, {}, {}
for i = 1, #RULE_DEFS do
  local d = RULE_DEFS[i]
  RULE_TEXT[d[1]] = d[3]
  RULE_CAT[d[1]] = d[2]
  RULE_ORDER[i] = d[1]
end

-- 取一条规则的最终值：房主显式配了用配置 → 模式级覆盖 → 预设默认
-- ⚠️ 注意这里是**赋值**不是 `local function`：ruleOn 已在文件前部声明过，
--    重新 local 会另起一个只有下文可见的新变量，tickVehicle 拿到的还是 nil。
-- 规则求值核心：**按模式名**算，不读 S.mode。
-- 这样旁路玩法（捉迷藏有自己独立的规则主体）也能拿到「本模式限制」的同一份答案，
-- 不用再抄一遍 29 条 —— 抄了就必然和这里漂移（HUD 显示 A、实际生效 B）。
local function ruleResolve(mode, name)
  mode = tostring(mode or '')
  -- ① 模式底线：进了这个模式就必须遵守，房主配置也关不掉。
  --    这是「模式自带限制」的硬核部分 —— 底线能被放松的话，限制等于没有。
  local hd = MODE_HARD[mode]
  if type(hd) == 'table' then
    local hv = hd[name]
    if hv ~= nil then return hv == true end
  end
  -- ② 房主逐条覆写（更严永远有效；想关掉非底线项也允许）
  local v = tonumber(TUNE[name])
  if v ~= nil then return v > 0 end
  -- ③ 模式适配包里「本模式特意写明」的项（不受下面的严格度预设影响）
  local ex = MODE_EXPLICIT[mode]
  if type(ex) == 'table' then
    local ev = ex[name]
    if ev ~= nil then return ev == true end
  end
  -- ④ 旧手写表里的模式语义例外（模式包缺失时的兜底）
  local mr = MODE_RULES[mode]
  if type(mr) ~= 'table' then return false end
  local m = tonumber(mr[name])
  if m ~= nil then return m > 0 end
  -- 房主统一指定了严格度就用房主的，否则用模式自己的。
  -- ⚠️ 只替换「预设」这一层：模式级显式覆盖（上面的 mr[name]）已经在前面返回了，
  --    所以德比「允许复位」这类语义例外不会被房主的 strict 撬掉。
  local pname = TUNE.rulePreset
  local p = (type(pname) == 'string' and PRESET[pname]) or PRESET[mr.preset]
  if type(p) ~= 'table' then return false end
  return (tonumber(p[name]) or 0) > 0
end

ruleOn = function(name) return ruleResolve(S.mode, name) end

-- 给旁路玩法 / 启动器取「某个模式实际生效的限制清单」。
-- 返回值与 snapshot.ruleItems 同构，HUD 的规则卡可以直接吃。
function M.ruleItemsFor(mode)
  local out = {}
  for i = 1, #RULE_ORDER do
    local name = RULE_ORDER[i]
    if ruleResolve(mode, name) then
      out[#out + 1] = {
        key = name, cat = RULE_CAT[name],
        catText = CAT_TEXT[RULE_CAT[name]] or '其他',
        text = RULE_TEXT[name],
      }
    end
  end
  return out
end

-- 当前生效的规则清单（纯文本，旧调用方 / 日志用）
local function activeRules()
  local out = {}
  for i = 1, #RULE_ORDER do
    local name = RULE_ORDER[i]
    if ruleOn(name) then out[#out + 1] = RULE_TEXT[name] end
  end
  return out
end

-- 带分类的规则清单（HUD 分栏展示 / 面板逐条列出用）
local function activeRuleItems()
  local out = {}
  for i = 1, #RULE_ORDER do
    local name = RULE_ORDER[i]
    if ruleOn(name) then
      out[#out + 1] = {
        key = name, cat = RULE_CAT[name],
        catText = CAT_TEXT[RULE_CAT[name]] or '其他',
        text = RULE_TEXT[name],
      }
    end
  end
  return out
end

function M.inputPolicy()
  -- 会话没了就一律放行：这是最后一道保险。
  -- phase 因为任何原因卡在局内时，按键都不该继续锁着。
  -- 空策略：所有规则键都补齐 false。
  -- ⚠️ 键必须**永远齐全**：输入层 setPolicy 是「~= nil 才覆盖」，
  --    少一个键就会残留上一个玩法 / 上一局的值（_sr_audit_policy.py 会抓）。
  local function emptyPolicy(reason)
    local p = { roundActive = false, running = false, vehicleLock = false,
                lockReset = false, freezeDrive = false, reason = reason or '',
                blockGroups = '', allowGroups = '',
                gearLock = false, gearTarget = nil }
    for i = 1, #RULE_ORDER do p[RULE_ORDER[i]] = false end
    return p
  end

  if S.sessionAlive == false then
    return emptyPolicy('session-gone')
  end
  local inRound = ROUND_PHASES[S.phase] and true or false
  local running = (S.phase == PHASE.running)

  -- 警匪默认连复位都锁（复位=传送逃脱），德比默认保留（翻车要能起来，走预算 + 冷却）。
  -- ⚠️ 这条不再是「警匪恒锁」的硬编码：房主可以在启动器里把 noManualReset 配成 0 放行，
  --    模式只提供默认值（MODE_RULES），最终口径以规则为准。
  local lockReset = inRound and ruleOn('noManualReset')

  -- ⚠️ 德比淘汰 = 车已报废（HUD 已经提示「等待本局结束」），但阶段还停在 running。
  --    不额外冻住的话，被淘汰的人还能满油门把活着的人撞出局。
  --    注意这里只是**多加一条**冻油门的理由：roundActive 仍然为真，
  --    所以总闸 startrideInput.applyDriveFreeze 照常放行，冻得住。
  local wrecked = (S.mode == 'derby')
    and (S.eliminated[S.selfId or 'self'] == true)

  -- 起跑程序：staging / countdown 把挡位按在空档（玩家挂不上），
  -- running 一到就自动挂一档并把换挡权交还玩家。
  -- ⚠️ gearTarget 只给「这一帧应该在哪一档」；输入层自己幂等，不会每帧下发。
  local gearLock, gearTarget = false, nil
  if inRound and ruleOn('neutralLock') then
    if running then
      gearTarget = 1
    else
      gearLock   = true
      gearTarget = 0
    end
  end

  local p = emptyPolicy(S.mode .. '/' .. S.phase .. (wrecked and '/wrecked' or ''))
  p.roundActive = inRound
  p.running     = running
  p.vehicleLock = inRound and ruleOn('noVehicleSwap')
  p.lockReset   = lockReset
  -- ⚠️ 就位 / 倒数期间一并冻住油门：staging 里车刚被摆到出生位，
  --    不冻就能立刻开走；countdown 里更是直接抢跑。
  --    running 一开自动解冻（总闸在 startrideInput.applyDriveFreeze）。
  --    是否冻由 freezeStaging 规则决定 —— 有人就是想让玩家在就位期热车。
  p.freezeDrive = inRound and (wrecked or
    (S.phase ~= PHASE.running and ruleOn('freezeStaging')))
  p.gearLock    = gearLock
  p.gearTarget  = gearTarget
  -- 模式准入：按键组放行/屏蔽（逗号分隔，由输入层解析）。
  -- ⚠️ 警匪的 lockReset 不可被 allowGroups 放行 —— 复位=传送逃脱，
  --    这条底线由输入层把关（见 startrideInput.groupOverride）。
  p.blockGroups = TUNE.blockGroups
  p.allowGroups = TUNE.allowGroups

  -- 29 条规则一次性铺开：局内才生效，局外一律 false
  for i = 1, #RULE_ORDER do
    local n = RULE_ORDER[i]
    p[n] = inRound and ruleOn(n) or false
  end
  return p
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
  if S.mode ~= 'cops_robber' and S.mode ~= 'derby' and not ruleOn('sameModel') then
    return nil
  end
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
    -- 全员同款车型：房主自己的 jbeam 文件名就是本局的基准车型
    model = selfJbeam() or '',
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
  -- 全员同款车型：房主广播的基准车型
  if type(t.model) == 'string' and t.model ~= '' then
    local m = string.lower(t.model)
    if S.requiredModel ~= m then
      S.requiredModel = m
      logMode('本局基准车型：' .. m)
    end
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
  noteZonePlan(plan)      -- 顺手记下全员共享的活动圈（加入者路径）
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
  local zoneNow = currentZone()
  -- 🔴 警匪雷达：HUD 的「目标雷达 / 被追捕」面板读的是 snap.robberDist /
  --    robberBearing / threat，但这里以前谁都没写 → 面永远显「无信号」、仪表盘指针不动。
  --    M.robberBearing / M.robberThreat 本身是好的（只是没人调），这里接上。
  --    ⚠️ 这两个函数在本文件后半段才定义，靠 M 表查，运行时才有值 → 用 type 兜底。
  local robDist, robBear, robThreat = nil, nil, nil
  if S.mode == 'cops_robber' then
    if type(M.robberBearing) == 'function' then
      robDist, robBear = M.robberBearing()
    end
    if type(M.robberThreat) == 'function' then
      robThreat = M.robberThreat()
    end
  end
  return {
    mode = S.mode,
    phase = S.phase,
    round = S.round,
    -- 给积分模块当幂等身份：同一局每帧都是这个值，跨局必然不同。
    phaseAt = S.phaseAt,
    role = S.selfRole,
    authority = S.authority,
    -- 等待开局（lobby 提示卡用）：当前人数与「为什么还不开局」。
    -- 单人进特殊模式时回合起不来是设计（minPlayers），但不告诉玩家就是「没装上」的错觉。
    lobbyPlayers = (function()
      local n = 1
      local pl = S.lastPlayers
      if type(pl) == 'table' then
        for _, p in ipairs(pl) do
          local pid = tostring(p.id or p.name or '')
          if pid ~= '' and pid ~= tostring(S.selfId or '') then n = n + 1 end
        end
      end
      return n
    end)(),
    minPlayers = TUNE.minPlayers,
    blockReason = S.lastBlockReason or '',
    winner = S.winner,
    captured = S.captureDone,
    -- 警匪雷达（见函数开头说明：以前是死字段）
    robberDist = robDist,
    robberBearing = robBear,
    threat = robThreat,
    captureMs = S.captureSince and ((S.clock - S.captureSince) * 1000)
      or (tonumber(S.remoteCaptureMs) or 0),
    captureHoldMs = TUNE.captureHoldMs,
    -- 本局已用时（秒）：只有 running 才有意义，其它阶段 nil。
    -- 赛道日面板用它显示「已跑」，不编造圈速（真正圈速要先有起终点计时门）。
    elapsed = (S.phase == PHASE.running) and math.max(0, S.clock - (S.phaseAt or S.clock)) or nil,
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
    -- 模式准入：区域 / 车型 / 按键组（HUD 与局内提示用）
    -- 位置限制的半径：方案圈 > 模式包基线（房主只能更严，见 modeZoneRadius）
    zoneRadius = zoneNow and zoneNow.radius or modeZoneRadius(),
    zoneOut = (S.zoneOut == true),
    -- 本机到圆心距离：HUD 规则卡要告诉玩家「还差多少米回圈」，
    -- 只给半径的话出圈时反而看不到自己在往哪边走。
    zoneDist = (function()
      local z = zoneNow
      if not z then return nil end
      local x, y = vehiclePos('self')
      if not x then return nil end
      return math.sqrt(dist2(x, y, z.x, z.y))
    end)(),
    -- 出圈倒计时必须和 tickZone 用同一个宽限源（模式包可能把它调长了），
    -- 否则 HUD 显示「3 秒后拉回」而实际 8 秒才罚 —— 玩家会以为 HUD 坏了。
    zoneGraceLeft = (S.zoneSince
      and math.max(0, math.ceil(modeGrace() - (S.clock - S.zoneSince))))
      or nil,
    zoneEliminate = TUNE.zoneEliminate,
    vehicleTypes = TUNE.vehicleTypes,
    vehicleEnforce = TUNE.vehicleEnforce,
    vehicleOk = (S.vehicleOk ~= false),
    vehicleName = S.vehicleName,
    blockGroups = TUNE.blockGroups,
    allowGroups = TUNE.allowGroups,
    -- 细粒度规则明细（HUD 逐条展示「本局限了什么」）
    rules = activeRules(),
    -- 带分类的结构化版本（HUD 按 位置/时间/车辆/视野/功能 分栏）
    ruleItems = activeRuleItems(),
    ruleTotal = #RULE_ORDER,
    rulePreset = TUNE.rulePreset or '',
    gearLocked = (S.phase ~= PHASE.running) and (ROUND_PHASES[S.phase] ~= nil)
                 and ruleOn('neutralLock'),
    vehicleRequired = (type(TUNE.vehicleTypes) == 'string' and TUNE.vehicleTypes ~= ''),
    -- 模式适配包（HUD 的模式组件与「本模式限制」标题都用它；纯数据、只读）
    pack = MODE_DESC[S.mode],
    packLabel = (MODE_DESC[S.mode] and MODE_DESC[S.mode].label) or '',
    packFuncCount = (MODE_DESC[S.mode] and MODE_DESC[S.mode].funcCount) or 0,
    packPosCount = (MODE_DESC[S.mode] and MODE_DESC[S.mode].posCount) or 0,
    zoneGrace = modeGrace(),
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
  -- 模式准入状态一并归零（否则下一局拿着上一圈的圆心/车型状态）
  S.zoneRound = -1
  S.zonePlan = nil
  S.zoneFallback = nil
  S.zoneSince = nil
  S.zoneOut = false
  S.zonePullAt = 0
  S.outside = {}
  S.vehRound = -1
  S.vehicleOk = true
  S.vehicleName = ''
  S.vehicleTries = 0
  S.vehicleAt = 0
  S.vehSet = nil
  S.vehSetSig = nil
end

return M
