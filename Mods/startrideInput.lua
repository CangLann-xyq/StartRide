-- ============================================================================
-- StartRide 输入限制层（按钮限制）
-- ============================================================================
-- 这是「警匪追逐 / 德比」等正式玩法的公共地基：玩法开始后，把「作弊类」按键
-- 屏蔽掉，避免玩家用传送 / 复位 / 换车绕过规则。
--
-- 设计来源参考 BeamLink 的 updateOfficialModeRuleFilters / updateModeTeleportFilter，
-- 但**按 StartRide 自己的约定重写**，并且绕开了它踩过的几个坑：
--
--   坑①  core_input_actionFilter 在老版本 BeamNG 上不存在。
--        BeamLink 直接调 core_input_actionFilter.setGroup(...)，老版本上整个
--        update 函数就抛错中断了。这里所有访问前先判空，缺了这个 API 就整层
--        静默降级（宁可不禁用，也不能让扩展崩掉）。
--   坑②  setGroup / addAction 每帧都调。
--        BeamLink 没做幂等，每帧重设一遍。这里用「意图键」缓存，只有禁/放状态
--        真的翻转时才调 API。
--   坑③  teleportToPoi 是函数级猴补丁，必须能还原。
--        地图传送不走 input action，只能替换函数体。原函数保存在
--        M._origTeleportToPoi，退出玩法/扩展卸载时一定还原 —— 否则玩家
--        离开房间后地图传送永久失效，而且下一次再进玩法会套娃包装。
--   坑④  ESC 菜单里的复位用的是**另一套 action 名**（recoverVehicle /
--        resetVehicle），跟键盘的 reset_physics 不是一回事，要单独一组。
--   坑⑤  上值上限 60（Lua 5.1）。
--        所有可调常量收进 TUNE，动作名清单收进 ACT，避免函数里散落大量
--        文件级 local 引用。
--
-- 调用约定（由 startride_mod.lua 驱动）：
--   M.setPolicy({ running = bool, roundActive = bool, vehicleLock = bool,
--                 lockReset = bool, reason = string })
--   M.tick(dtReal)      -- 每帧，负责幂等下发 + AI 压制 + 猴补丁
--   M.disableAll()      -- 退出玩法：全部放行 + 还原猴补丁
-- ============================================================================

local M = {}

-- ---------------------------------------------------------------------------
-- 可调常量（集中一处，便于控制上值数量）
-- ---------------------------------------------------------------------------
local TUNE = {
  -- AI 交通每次压制的间隔（秒）。不需要每帧，AI 自己会重开。
  aiInterval      = 1.0,
  -- ESC 菜单复位在「任何联机房间」都拦掉（不只正式玩法）。
  blockMenuAlways = true,
  -- 猴补丁期间是否连「非本玩法」的地图点击数也一起挡（默认只挡玩法内）。
  logPolicy       = true,
}

-- ---------------------------------------------------------------------------
-- 动作名清单
--
-- ⚠️ 这里**优先用引擎自己的 actionTemplates**（core/input/actionFilter.lua 导出
--    的 createActionTemplate），而不是手抄 action 名。原因：
--      · 原生模板会跟着游戏版本走 —— 我们手抄的清单会随版本漂移，漏掉新动作。
--      · 原生模板维护得更全（例：funStuff 有 14 个动作，手抄很容易只写 5 个）。
--    API 不可用（老版本 / 加载顺序）时才退回下面的兜底清单。
--
--    注意 funStuff：engine 自带的 funStuff 里有 forceField / funBoost 等，
--    但**不含** gameplay_interact 与 toggleRadialMenuMulti，这两个单独补。
--    注意 competitive 模板 = 传送 + 菜单 + 物理控制 + AI + 换车 + 自由相机 +
--    funStuff + 编辑器，正好覆盖我们要的绝大部分，是最省事的一组。
-- ---------------------------------------------------------------------------
local ACT = {
  -- 娱乐 / 快捷：力场、加速、多选径向菜单、互动
  fun          = { 'forceField', 'funBoost', 'funBoostBackwards',
                   'toggleRadialMenuMulti', 'gameplay_interact' },
  -- 载具切换：换车、换零件（= 逃离现场 / 换更硬的车）
  vehicle      = { 'vehicle_selector', 'parts_selector' },
  -- AI 交通开关
  ai           = { 'toggleAITraffic', 'toggleTraffic' },
  -- 物理复位（普通）
  reset        = { 'reset_physics', 'recover_vehicle', 'recover_vehicle_alt',
                   'recover_to_last_road' },
  -- 绕开复位限制的旁路（全量复位 / 重载车 / 暂停）
  resetBypass  = { 'reset_all_physics', 'reload_vehicle', 'reload_all_vehicles',
                   'pause' },
  -- 传送 / 位置记忆
  teleport     = { 'loadHome', 'saveHome', 'dropPlayerAtCamera',
                   'dropPlayerAtCameraNoReset', 'dropCameraAtPlayer',
                   'toggleCamera' },
  -- ESC 菜单用的另一套名字（跟键盘 action 不是同一批）
  menuReset    = { 'recoverVehicle', 'resetVehicle' },
}

-- 用引擎模板覆盖手抄清单（有就用引擎的，没有就保留兜底）
-- ⚠️ 这个函数必须在 `local S` **之后**定义：它读写 S.templatesAdopted，
--    放在前面会闭包到一个 nil 的全局 S（Lua 里 local 声明之前引用同名变量
--    会当成全局）→ 一跑就 "attempt to index a nil value (global 'S')"。
--    定义位置见下方 S 声明之后。

-- 分组名（前缀 + 用途）。setGroup 的组名要稳定，别每次拼随机串。
local GROUP = {
  fun         = 'srModeFun',
  vehicle     = 'srModeVehicle',
  ai          = 'srModeAi',
  reset       = 'srModeReset',
  resetBypass = 'srModeResetBypass',
  teleport    = 'srModeTeleport',
  menuReset   = 'srModeMenuReset',
}

-- ---------------------------------------------------------------------------
-- 运行状态
-- ---------------------------------------------------------------------------
local S
S = {
  -- 上层声明的意图
  want = {
    running     = false,   -- 玩法进行中（含倒计时）
    roundActive = false,   -- 局内（staging / countdown / running 都算）
    vehicleLock = false,   -- 锁车（赛中不许换车换零件）
    lockReset   = false,   -- 连复位也锁（警匪：复位=传送逃脱）
  },
  -- 已下发的状态（用于幂等比较）
  applied = {},
  -- 猴补丁的原函数
  _origTeleportToPoi = nil,
  _patchInstalled   = false,
  -- AI 压制节流
  aiTimer = 0,
  -- 是否已吸收引擎原生 actionTemplates
  templatesAdopted = false,
  -- 最近一次错误（面板可读）
  lastError = '',
  ready     = false,
  apiOK     = false,
  apiReported = false,
}

-- ---------------------------------------------------------------------------
-- 吸收引擎自带的 actionTemplates（定义必须在 S 之后，见上方说明）
-- ---------------------------------------------------------------------------
local function adoptEngineTemplates(af)
  if af == nil or af.createActionTemplate == nil then return false end
  if S.templatesAdopted then return true end

  local ok, res = pcall(function()
    local t = af.createActionTemplate
    return {
      teleport    = t({ 'vehicleTeleporting' }),
      vehicle     = t({ 'vehicleMenues' }),
      ai          = t({ 'aiControls' }),
      reset       = t({ 'resetPhysics', 'vehicleRecovery' }),
      resetBypass = t({ 'pause' }),
    }
  end)
  if not ok or type(res) ~= 'table' then return false end

  for key, list in pairs(res) do
    if type(list) == 'table' and #list > 0 then ACT[key] = list end
  end

  -- 补上引擎模板没覆盖、但我们确实要拦的（去重后追加）
  local function push(list, extra)
    for _, name in ipairs(extra) do
      local found = false
      for _, have in ipairs(list) do
        if have == name then found = true break end
      end
      if not found then list[#list + 1] = name end
    end
  end
  push(ACT.fun,         { 'forceField', 'funBoost', 'funBoostBackwards',
                          'toggleRadialMenuMulti', 'gameplay_interact' })
  push(ACT.teleport,    { 'toggleCamera' })
  push(ACT.resetBypass, { 'reset_all_physics', 'reload_vehicle',
                          'reload_all_vehicles', 'pause' })

  S.templatesAdopted = true
  return true
end

-- ---------------------------------------------------------------------------
-- 工具：安全取 core_input_actionFilter
--
-- ⚠️ 坑①（两层的）：老版本 BeamNG 没有这个模块；
--    而且**即使有**，在我们的扩展 onExtensionLoaded 跑的那一刻它可能还没加载完 ——
--    所以不能只在文件顶层探一次就定生死，那会把「本来能用」误判成「不可用」。
--    这里改成**惰性探测 + 每次自愈**：拿到就置 apiOK，拿不到就下一帧再试。
-- ---------------------------------------------------------------------------
local function inputFilter()
  local af = core_input_actionFilter
  if af == nil then
    -- 还没加载出来：保持 apiOK=false，下次再探（自愈）
    if S.apiOK then S.apiOK = false end
    return nil
  end
  -- ⚠️ 不要用 type(x) == 'function' 判方法是否存在。
  --    BeamNG 的扩展大多是 Lua 表（确实是 function），但 C 侧绑定 / 宿主对象
  --    可能把方法暴露成 userdata；那样判会把「能用」误判成「不可用」。
  --    只要**非 nil** 就当它存在，真调用时用 pcall 兜住。
  if af.setGroup == nil or af.addAction == nil then
    if S.apiOK then S.apiOK = false end
    return nil
  end
  if not S.apiOK then
    S.apiOK = true
    S.lastError = ''
  end
  return af
end

-- 只在「从不可用变成可用」时报一次，避免刷日志
local function reportApiOnce()
  if S.apiOK and not S.apiReported then
    S.apiReported = true
    pcall(function()
      log('I', 'startride', '[StartRide] 玩法按键限制已就绪')
    end)
  end
end

-- ---------------------------------------------------------------------------
-- 幂等下发一组开关
--
-- groupList : 组名（GROUP.xxx）
-- actions   : 动作名数组
-- blocked   : 是否屏蔽
-- ---------------------------------------------------------------------------
local function applyGroup(key, groupName, actions, blocked)
  local want = blocked and true or false

  local af = inputFilter()
  if not af then
    -- API 还没就绪：**不要**写 applied。写了就等于把「禁用」标成已完成，
    -- 等 API 后来加载出来时状态没变、又不会重下发 → 按键永远不禁用。
    -- 保持原值，下一帧继续试。
    return
  end

  if S.applied[key] == want then return end   -- 坑②：状态没变就不调

  -- API 刚就绪时，先把引擎原生模板吸收进来（只做一次），再下发
  adoptEngineTemplates(af)

  local ok, err = pcall(function()
    af.setGroup(groupName, actions)
    af.addAction(0, groupName, want)
  end)
  if ok then
    S.applied[key] = want
  else
    S.lastError = 'setGroup(' .. tostring(groupName) .. '): ' .. tostring(err)
    -- 失败不要写 applied，下一帧还会重试
  end
end

-- ---------------------------------------------------------------------------
-- 猴补丁：地图传送（坑③）
--
-- freeroam_bigMapMode.teleportToPoi 不走 input action，只能换函数体。
-- 只在第一次打补丁时保存原函数，并且**只保存一次**（避免套娃）。
-- ---------------------------------------------------------------------------
local function installTeleportPatch(blocked)
  local mod = freeroam_bigMapMode
  -- 同 inputFilter 的道理：别用 type()=='function' 判，宿主对象可能是 userdata。
  if mod == nil or mod.teleportToPoi == nil then
    return false
  end

  if blocked then
    if S._patchInstalled then return true end          -- 已经打过，别套娃
    local orig = mod.teleportToPoi
    if orig == nil then return false end
    S._origTeleportToPoi = orig
    mod.teleportToPoi = function(...)
      -- 玩法已经结束就把控制权交还原函数（用户可能中途退出玩法）
      if not S.want.roundActive then
        local back = S._origTeleportToPoi
        if back then return back(...) end
        return false
      end
      return false
    end
    S._patchInstalled = true
    S.lastError = ''
    return true
  end

  -- 还原（必须还原到**原函数**，不是我们包装的那一层）
  if S._patchInstalled and S._origTeleportToPoi then
    local ok = pcall(function()
      freeroam_bigMapMode.teleportToPoi = S._origTeleportToPoi
    end)
    if ok then
      S._patchInstalled = false
      S._origTeleportToPoi = nil
    end
  end
  return false
end

-- ---------------------------------------------------------------------------
-- AI 交通强制关掉
--
-- 玩法里 AI 车会干扰（撞飞玩家、刷在出生走廊上）。这里定期把玩家自己的
-- AI 关掉，并把交通量压到 0。**不要每帧调**，AI 模块自己会有开销。
-- ---------------------------------------------------------------------------
local function suppressAi()
  -- GE 侧的交通开关
  pcall(function()
    if core_traffic then
      -- 别用 type()=='function' 判：宿主对象的方法可能是 userdata
      if core_traffic.setTrafficEnabled ~= nil then
        core_traffic.setTrafficEnabled(false)
      end
      if core_traffic.setTrafficDensity ~= nil then
        core_traffic.setTrafficDensity(0)
      end
    end
  end)

  -- 本车 AI 模式（玩家可能按过 AI 键）
  pcall(function()
    if be == nil then return end
    -- 别用 type()=='function' 判：宿主对象的方法可能是 userdata
    if be.getPlayerVehicle == nil then return end
    local veh = be:getPlayerVehicle(0)
    if veh and veh.queueLuaCommand then
      veh:queueLuaCommand("if ai and ai.setMode then ai.setMode('disabled') end")
    end
  end)
end

-- ---------------------------------------------------------------------------
-- 对外：声明意图
-- ---------------------------------------------------------------------------
function M.setPolicy(p)
  if type(p) ~= 'table' then return end
  local w = S.want

  if p.roundActive ~= nil then w.roundActive = (p.roundActive == true) end
  if p.running     ~= nil then w.running     = (p.running == true) end
  if p.vehicleLock ~= nil then w.vehicleLock = (p.vehicleLock == true) end
  if p.lockReset   ~= nil then w.lockReset   = (p.lockReset == true) end

  -- running 隐含 roundActive（调用方忘了传也不会漏禁）
  if w.running then w.roundActive = true end

  if TUNE.logPolicy then
    local tag = w.roundActive and (w.running and 'RUN' or 'PREP') or 'IDLE'
    if S.lastTag ~= tag then
      S.lastTag = tag
      if p.reason then
        pcall(function()
          log('I', 'startride', '[StartRide] 输入限制 ' .. tag .. ' ← ' .. tostring(p.reason))
        end)
      end
    end
  end
end

-- ---------------------------------------------------------------------------
-- 对外：每帧驱动
-- ---------------------------------------------------------------------------
function M.tick(dtReal)
  local w = S.want
  local active = w.roundActive

  -- 惰性探测：API 一旦就绪就记一次日志
  inputFilter()
  reportApiOnce()

  -- 组开关（按重要性排列：先锁复位/传送，再锁换车，最后锁娱乐）
  applyGroup('teleport',    GROUP.teleport,    ACT.teleport,    active)
  applyGroup('resetBypass', GROUP.resetBypass, ACT.resetBypass, active)
  applyGroup('reset',       GROUP.reset,       ACT.reset,       active or w.lockReset)
  applyGroup('vehicle',     GROUP.vehicle,     ACT.vehicle,     active or w.vehicleLock)
  applyGroup('fun',         GROUP.fun,         ACT.fun,         w.running)
  applyGroup('ai',          GROUP.ai,          ACT.ai,          w.running)

  -- ESC 菜单复位：按配置，任何房间都拦（真正玩法里再加一层）
  applyGroup('menuReset', GROUP.menuReset, ACT.menuReset,
             TUNE.blockMenuAlways or active)

  -- 地图传送猴补丁
  -- ⚠️ 用「本帧应该装 && 实际没装」来驱动重试，而不是直接比 active 与 _patchInstalled：
  --    freeroam_bigMapMode 可能比我们晚加载出来，第一次装不上时 _patchInstalled 是 false，
  --    而 active 也是 true，若直接比就会以为「已一致」而永不重试。
  if active then
    if not S._patchInstalled then
      installTeleportPatch(true)
    end
  else
    if S._patchInstalled then
      installTeleportPatch(false)
    end
  end

  -- AI 压制（节流）
  if w.running then
    S.aiTimer = S.aiTimer + (tonumber(dtReal) or 0.016)
    if S.aiTimer >= TUNE.aiInterval then
      S.aiTimer = 0
      pcall(suppressAi)
    end
  else
    S.aiTimer = 0
  end

  S.ready = true
end

-- ---------------------------------------------------------------------------
-- 对外：全部放行（退出玩法 / 卸载）
-- ---------------------------------------------------------------------------
function M.disableAll()
  S.want.running = false
  S.want.roundActive = false
  S.want.vehicleLock = false
  S.want.lockReset = false

  local af = inputFilter()
  if af then
    for key, groupName in pairs(GROUP) do
      if S.applied[key] then
        pcall(function() af.addAction(0, groupName, false) end)
        S.applied[key] = false
      end
    end
  end
  S.applied = {}

  pcall(installTeleportPatch, false)
  S.aiTimer = 0
end

-- ---------------------------------------------------------------------------
-- 对外：诊断（面板 / 日志用）
-- ---------------------------------------------------------------------------
function M.status()
  local blocked = {}
  for key, v in pairs(S.applied) do
    if v then blocked[#blocked + 1] = key end
  end
  table.sort(blocked)

  return {
    ready     = S.ready,
    apiOK     = S.apiOK,
    active    = S.want.roundActive,
    running   = S.want.running,
    blocked   = table.concat(blocked, '/'),
    patched   = S._patchInstalled,
    lastError = S.lastError,
  }
end

-- ---------------------------------------------------------------------------
-- 内部：初始化
--
-- ⚠️ 这里**不做**一次性探测。core_input_actionFilter 可能比我们晚加载，
--    顶层探一次会把本来能用的环境误判成不可用。改成 tick 里每次惰性探测（见 inputFilter）。
-- ---------------------------------------------------------------------------
S.apiOK = false

return M
