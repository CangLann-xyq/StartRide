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
--                 lockReset = bool, freezeDrive = bool, reason = string,
--                 blockGroups = string, allowGroups = string })
--   blockGroups / allowGroups：逗号分隔的组名，只在本回合内生效。
--   可用组名：fun/vehicle/parts/ai/reset/resetBypass/cam/anchor/dropcam/
--   bigmap/menuReset/gear/grabber/slowmo/photo/walk/editor/spawn/coupler/minimap
--   （teleport 作为别名组，一次覆盖 cam+anchor+dropcam+bigmap）
--   allowGroups 放行不了 lockReset（警匪局里复位=传送逃脱，这条底线不让配置撬动）。
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
  -- 娱乐 / 快捷：力场、加速、烧胎、开门闩、多选径向菜单、互动
  -- （引擎 funStuff 模板共 14 项，这里手抄一份兜底；adoptEngineTemplates 会换成引擎清单）
  fun          = { 'forceField', 'funBoost', 'funBoostBackwards', 'funBoom',
                   'funBreak', 'funExtinguish', 'funFire', 'funHinges',
                   'funTires', 'funRandomTire', 'funFling', 'funFlingDownward',
                   'latchesOpen', 'latchesClose', 'toggleRadialMenuMulti',
                   'gameplay_interact' },
  -- 换车：换车菜单 + 直接切到场上另一辆车（比换车菜单更隐蔽的逃离手段）
  vehicle      = { 'vehicle_selector', 'switch_next_vehicle',
                   'switch_previous_vehicle', 'switch_next_vehicle_multiseat' },
  -- 换零件 / 调校 / 车辆调试菜单
  parts        = { 'parts_selector', 'vehicledebugMenu' },
  -- AI 交通开关
  ai           = { 'toggleAITraffic', 'toggleTraffic' },
  -- 物理复位（普通）
  reset        = { 'reset_physics', 'recover_vehicle', 'recover_vehicle_alt',
                   'recover_to_last_road' },
  -- 绕开复位限制的旁路（全量复位 / 重载车 / 暂停）
  resetBypass  = { 'reset_all_physics', 'reload_vehicle', 'reload_all_vehicles',
                   'pause' },
  -- 自由相机（键盘切车外镜头 / 把相机丢回车上）
  cam          = { 'toggleCamera', 'dropCameraAtPlayer' },
  -- 位置锚点：存 / 读 Home 点 = 一个随身传送门
  anchor       = { 'loadHome', 'saveHome' },
  -- 把车直接落在相机位置（开局抢位 / 贴脸下车）
  dropcam      = { 'dropPlayerAtCamera', 'dropPlayerAtCameraNoReset' },
  -- 大地图（配合 teleportToPoi 猴补丁，堵住「地图点两下就飞过去」）
  bigmap       = { 'toggleBigMap' },
  -- ESC 菜单用的另一套名字（跟键盘 action 不是同一批）
  menuReset    = { 'recoverVehicle', 'resetVehicle' },
  -- 换挡：手动挡升降挡 + 换挡模式 + **直接选挡（gear1..gear8 / gearN / gearR）**
  -- ⚠️ 只列「玩家主动挂挡」的动作，不含油门/刹车 —— 空档锁的目的是
  --    让车在起跑线原地待命，而不是把车冻住。
  -- ⚠️ gear1..gear8 / gearN 必须列：只锁 gearUp/gearDown 的话，
  --    玩家按数字键能直接跳挡，空档锁形同虚设。
  gear         = { 'gearUp', 'gearDown', 'gearShiftUp', 'gearShiftDown',
                   'shiftUp', 'shiftDown', 'toggleShiftMode',
                   'toggleShiftSequential', 'toggleShifterMode',
                   'gearR', 'gearN', 'gear1', 'gear2', 'gear3', 'gear4',
                   'gear5', 'gear6', 'gear7', 'gear8' },
  -- 节点抓取：能把别人的整台车拖着走 / 拽翻
  grabber      = { 'nodegrabberAction', 'nodegrabberGrab', 'nodegrabberRender',
                   'nodegrabberStrength', 'nodegrabberPadGrab', 'nodegrabberPadMode' },
  -- 物理时间缩放：慢动作能看清对手意图、卡判定
  slowmo       = { 'slower_motion', 'faster_motion', 'toggle_slow_motion' },
  -- 拍照模式 / 下车步行（步行能穿墙、脱离车身看全场）
  photo        = { 'photomode' },
  walk         = { 'toggleWalkingMode' },
  -- 世界编辑器 / 对象编辑器 / 安全模式 / App 编辑
  editor       = { 'editorToggle', 'objectEditorToggle', 'editorSafeModeToggle',
                   'appedit' },
  -- 沙盒径向菜单（能修车、加油、生成车、刷 AI、传送）+ 赛道生成器
  spawn        = { 'toggleRadialMenuSandbox', 'toggleRadialMenuPlayerVehicle',
                   'toggleRadialMenuFavorites', 'menu_item_focus_lr',
                   'menu_item_focus_ud', 'toggleTrackBuilder' },
  -- 拖车挂接
  coupler      = { 'couplersLock', 'couplersToggle', 'couplersUnlock' },
  -- 小地图开关
  minimap      = { 'toggle_minimap' },
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
  parts       = 'srModeParts',
  ai          = 'srModeAi',
  reset       = 'srModeReset',
  resetBypass = 'srModeResetBypass',
  cam         = 'srModeCam',
  anchor      = 'srModeAnchor',
  dropcam     = 'srModeDropCam',
  bigmap      = 'srModeBigMap',
  menuReset   = 'srModeMenuReset',
  gear        = 'srModeGear',
  grabber     = 'srModeGrabber',
  slowmo      = 'srModeSlowMo',
  photo       = 'srModePhoto',
  walk        = 'srModeWalk',
  editor      = 'srModeEditor',
  spawn       = 'srModeSpawn',
  coupler     = 'srModeCoupler',
  minimap     = 'srModeMinimap',
}

-- 组别名：房主在启动器里写的旧组名继续有效。
-- teleport = 相机 + 位置锚点 + 落车 + 大地图（原来打包成一组，现在拆细了但配置不用改）
local ALIAS_OF = {
  cam     = 'teleport',
  anchor  = 'teleport',
  dropcam = 'teleport',
  bigmap  = 'teleport',
}

-- 细粒度规则名（策略表里逐项传，不再是「一整组」）
-- 全量规则键 = startrideMode 的 RULE_DEFS 全表（29 条）。
-- ⚠️ 必须一条不漏：setPolicy 是「~= nil 才覆盖」，少一条就会残留上一个玩法的值。
-- 其中 zoneEliminate / neutralLock / freezeStaging / roundLimit /
-- vehicleEnforce / sameModel 由玩法层自己消费（区域、起跑、车型逻辑），
-- 输入层只负责**存下来**并在 status 里报条数 —— 不进这里就等于被丢弃。
local RULE_KEYS = {
  'zoneEliminate', 'noMapTeleport', 'noHomeAnchor', 'noDropAtCam',
  'neutralLock', 'freezeStaging', 'noPause', 'noSlowMotion', 'roundLimit',
  'noVehicleSwap', 'noPartsSwap', 'noRepair', 'noTrailer',
  'vehicleEnforce', 'sameModel',
  'noFreeCam', 'noPhotoMode', 'noWalkMode', 'noMinimap',
  'noManualReset', 'noFun', 'noAiTraffic', 'noAiMode',
  'noNodeGrabber', 'noWorldEditor', 'noSpawnVehicle', 'noRadialSandbox',
  'noDebugMenu', 'noWeather',
}

-- ---------------------------------------------------------------------------
-- 逗号分隔清单 → 小写集合表
--
-- 输入层自己留一份解析器：模组之间不共享 local（各文件独立加载），
-- 而且组名配置会原样传进来，用签名缓存避免每帧重复解析。
-- ---------------------------------------------------------------------------
local function splitList(str)
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
    freezeDrive = false,   -- 冻住油门/刹车（躲猫猫躲藏期的搜索者）
    -- 配置的按键组放行/屏蔽（逗号分隔；只在本回合内生效）
    blockGroups = '',
    allowGroups = '',
    -- 细粒度玩法规则表（name -> bool），由玩法模块按模式给出
    rules = {},
    -- 这一帧是否真的收到了规则表（false = 老调用方，退回「局内全锁」的保守口径）
    hasRules  = false,
    -- 锁住换挡动作（起跑前空档待命）
    gearLock   = false,
  },
  -- 强制挂档的执行状态（nil = 还没下发过；数字 = 已下发的目标档位）
  gearTarget = nil,
  gearSent   = nil,
  -- 放行/屏蔽清单的解析缓存（串没变就不重解析）
  blockSet = nil,
  allowSet = nil,
  blockSig  = nil,
  allowSig  = nil,
  -- 已下发的状态（用于幂等比较）
  applied = {},
  freezeApplied   = nil,
  freezeVehicleId = nil,
  -- 猴补丁的原函数
  _origTeleportToPoi = nil,
  _patchInstalled   = false,
  _origSetTod       = nil,
  _origCycleTod     = nil,
  _envPatched       = false,
  _origQaToggle     = nil,
  _qaPatched        = false,
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

  -- ⚠️ 只挑「引擎模板和我们的组一一对应」的那几组。
  --    vehicleMenues 里既有换车也有换零件，套到 vehicle 组会把 parts 一起锁死，
  --    所以 vehicle / parts / fun / teleport 拆分组一律用手抄清单，不用模板。
  local ok, res = pcall(function()
    local t = af.createActionTemplate
    return {
      ai          = t({ 'aiControls' }),
      reset       = t({ 'resetPhysics', 'vehicleRecovery' }),
      resetBypass = t({ 'pause' }),
      cam         = t({ 'freeCam' }),
      editor      = t({ 'editor' }),
      grabber     = t({ 'nodegrabber' }),
      photo       = t({ 'photoMode' }),
      walk        = t({ 'walkingMode' }),
      gear        = t({ 'setShifterMode' }),
      coupler     = t({ 'couplers' }),
      minimap     = t({ 'miniMap' }),
      bigmap      = t({ 'bigMap' }),
      spawn       = t({ 'radialMenuActivate', 'radialMenu', 'trackBuilder' }),
    }
  end)
  if not ok or type(res) ~= 'table' then return false end

  -- **并集**而不是覆盖：引擎清单跟着版本走（会多出新动作），但我们的手抄清单
  -- 里有它没有的（gearUp / gearDown、dropPlayerAtCamera 等）——
  -- 一旦用引擎清单整组覆盖，那几个就会静默漏掉，玩家按了照样生效。
  local function merge(key, extra)
    local list = ACT[key]
    if type(list) ~= 'table' then list = {} ACT[key] = list end
    for i = 1, #extra do
      local name = extra[i]
      local found = false
      for j = 1, #list do
        if list[j] == name then found = true break end
      end
      if not found then list[#list + 1] = name end
    end
  end

  for key, list in pairs(res) do
    if type(list) == 'table' and #list > 0 then merge(key, list) end
  end

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
-- 猴补丁：天气 / 时间（坑③ 的同类）
--
-- core_environment.setTimeOfDay 不在任何 input action 里（UI 滑块直接调），
-- 想禁「局内改天气 / 改时间」只能包函数。cyclTimeOfDay 是快捷键翻页那条路径。
-- 同样只存一次原函数，还原回到原函数而不是我们这一层。
-- ---------------------------------------------------------------------------
local function installEnvPatch(blocked)
  local env = core_environment
  if env == nil or env.setTimeOfDay == nil then return false end

  if blocked then
    if S._envPatched then return true end
    S._origSetTod = env.setTimeOfDay
    S._origCycleTod = env.cycleTimeOfDay
    env.setTimeOfDay = function(...)
      if not S.want.rules.noWeather then
        local back = S._origSetTod
        if back then return back(...) end
      end
      return false
    end
    if S._origCycleTod ~= nil then
      env.cycleTimeOfDay = function(...)
        if not S.want.rules.noWeather then
          local back = S._origCycleTod
          if back then return back(...) end
        end
        return false
      end
    end
    S._envPatched = true
    return true
  end

  if S._envPatched then
    pcall(function()
      if S._origSetTod ~= nil then core_environment.setTimeOfDay = S._origSetTod end
      if S._origCycleTod ~= nil then core_environment.cycleTimeOfDay = S._origCycleTod end
    end)
    S._envPatched = false
    S._origSetTod = nil
    S._origCycleTod = nil
  end
  return false
end

-- ---------------------------------------------------------------------------
-- 猴补丁：沙盒径向菜单（修车 / 加油 / 生成车 / 刷 AI / 传送的总入口）
--
-- 这些能力**不全**是 input action —— UI 里点一下直接调 core_quickAccess.toggle，
-- 连按键都不用按。所以在动作名之外再包一层：局内只允许打开非沙盒分类。
-- ⚠️ 只拦 '/sandbox/' 路径，别把玩家正常的车辆配置/暂停菜单一起封死。
-- ---------------------------------------------------------------------------
local function installQuickAccessPatch(blocked)
  local qa = core_quickAccess
  if qa == nil or qa.toggle == nil then return false end

  if blocked then
    if S._qaPatched then return true end
    S._origQaToggle = qa.toggle
    qa.toggle = function(path, ...)
      if S.want.roundActive and type(path) == 'string'
         and string.find(path, '/sandbox/', 1, true) ~= nil then
        return false
      end
      local back = S._origQaToggle
      if back then return back(path, ...) end
      return false
    end
    S._qaPatched = true
    return true
  end

  if S._qaPatched and S._origQaToggle ~= nil then
    pcall(function() core_quickAccess.toggle = S._origQaToggle end)
    S._qaPatched = false
    S._origQaToggle = nil
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

local function playerVehicle()
  local veh = nil
  pcall(function()
    if be ~= nil and be.getPlayerVehicle ~= nil then
      veh = be:getPlayerVehicle(0)
    end
  end)
  return veh
end

-- ---------------------------------------------------------------------------
-- 强制挂档（起跑前空档锁 / 起跑后自动挂一档）
--
-- ⚠️ 引擎侧没有稳定的「设置档位」GE 接口，只能往车辆 Lua 里丢命令。
--    这里一次性塞两个候选 API，都不存在就静默放弃 —— 宁可只锁住换挡键，
--    也不能让输入层崩掉。命令在车辆侧执行，出错不会回传。
-- ⚠️ 幂等：只有目标档位真的变化时才下发（S.gearSent 缓存）。
-- ---------------------------------------------------------------------------
local function forceGear(index)
  if index == nil then
    S.gearSent = nil
    return
  end
  if S.gearSent == index then return end

  local ok = pcall(function()
    local veh = be ~= nil and be.getPlayerVehicle ~= nil and be:getPlayerVehicle(0) or nil
    if veh == nil or veh.queueLuaCommand == nil then return end
    local n = tonumber(index) or 0
    -- 候选①：主控制器换挡（手动 / 自动都认）
    -- 候选②：部分版本的写法
    veh:queueLuaCommand(
      'if controller and controller.mainController then '
      .. 'if controller.mainController.shiftToGear then controller.mainController.shiftToGear(' .. n .. ') '
      .. 'elseif controller.mainController.setGear then controller.mainController.setGear(' .. n .. ') end end')
  end)
  if ok then S.gearSent = index end
end

-- 每帧按玩法模块给的档位目标执行（目标变了才真正下发）
local function applyGearTarget()
  forceGear(S.want.gearTarget)
end

local function applyDriveFreeze()
  local want = (S.want.freezeDrive == true) and (S.want.roundActive == true)
  local veh = playerVehicle()
  if veh == nil then return end
  local id = nil
  pcall(function() id = veh:getID() end)
  if S.freezeApplied == want and S.freezeVehicleId == id then return end
  local ok = pcall(function()
    veh:queueLuaCommand('controller.setFreeze(' .. (want and '1' or '0') .. ')')
  end)
  if ok then
    S.freezeApplied = want
    S.freezeVehicleId = id
  end
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
  if p.freezeDrive ~= nil then w.freezeDrive = (p.freezeDrive == true) end
  if type(p.blockGroups) == 'string' then w.blockGroups = p.blockGroups end
  if type(p.allowGroups) == 'string' then w.allowGroups = p.allowGroups end

  -- 细粒度规则：没传就不改（各玩法模块自己逐帧给全量意图）
  if p.gearLock ~= nil then w.gearLock = (p.gearLock == true) end
  for i = 1, #RULE_KEYS do
    local k = RULE_KEYS[i]
    if p[k] ~= nil then
      w.rules[k] = (p[k] == true)
      w.hasRules = true
    end
  end
  -- 档位目标：nil 表示「交还给玩家」，数字表示强制挂到该档
  if p.gearTarget ~= nil then
    if type(p.gearTarget) == 'number' then w.gearTarget = p.gearTarget else w.gearTarget = nil end
  end

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
-- 配置的放行/屏蔽覆盖
--
-- 只在**本回合内**生效（roundActive）：大厅里不该被玩法配置绑住手脚。
-- ⚠️ allowGroups 放行不了 reset 组的 lockReset —— 警匪局里复位=传送逃脱，
--    这条底线不让配置撬动；其余组（含 blockMenuAlways 的菜单复位）都听配置。
-- blockGroups 优先级高于 allowGroups（两边都配了按屏蔽算，安全侧优先）。
-- ⚠️ 组名是驼峰（menuReset / resetBypass），而清单解析时转了小写 ——
--    这里查找也必须转，否则 camelCase 组永远匹配不上（I8 踩过）。
-- ---------------------------------------------------------------------------
local function groupOverride(key, base)
  if not S.want.roundActive then return base end
  local k = string.lower(key)
  local alias = ALIAS_OF[key]

  -- 精确组名
  if S.blockSet and S.blockSet[k] then return true end
  -- 别名组（teleport 一次性覆盖 cam/anchor/dropcam/bigmap）
  if alias and S.blockSet and S.blockSet[alias] then return true end

  local allow = nil
  if S.allowSet and S.allowSet[k] then allow = true end
  if alias and S.allowSet and S.allowSet[alias] then allow = true end

  if allow then
    if k == 'reset' and S.want.lockReset then return true end
    return false
  end
  return base
end

-- ---------------------------------------------------------------------------
-- 对外：每帧驱动
-- ---------------------------------------------------------------------------
function M.tick(dtReal)
  local w = S.want
  local active = w.roundActive

  -- 放行/屏蔽清单按签名缓存解析
  if S.blockSig ~= w.blockGroups then
    S.blockSig = w.blockGroups
    S.blockSet = splitList(w.blockGroups)
  end
  if S.allowSig ~= w.allowGroups then
    S.allowSig = w.allowGroups
    S.allowSet = splitList(w.allowGroups)
  end

  -- 惰性探测：API 一旦就绪就记一次日志
  inputFilter()
  reportApiOnce()

  -- 组开关：**逐条规则**驱动，不再是「局内就把一切全锁」。
  -- 每条规则都可能被房主配置的放行/屏蔽清单改写（groupOverride / 别名组）。
  -- ⚠️ 这些 local 必须在所有 applyGroup 之前算完：Lua 里 local 声明之前引用
  --    同名变量会当成**全局**（拿到 nil），组会静默永不屏蔽（I8 踩过）。
  --
  -- legacy：老调用方只传 roundActive、不传规则表时，退回「局内全锁」的保守口径，
  -- 免得升级过渡期出现「限制全丢」的窗口。
  local legacy = active and not w.hasRules
  local R = w.rules
  local function on(name) return R[name] == true or (legacy and name ~= 'noWeather') end

  local funOn      = groupOverride('fun',     on('noFun'))
  local aiOn       = groupOverride('ai',      on('noAiTraffic'))
  local vehOn      = groupOverride('vehicle', on('noVehicleSwap') or w.vehicleLock)
  local partsOn    = groupOverride('parts',   on('noPartsSwap') or on('noDebugMenu'))
  local camOn      = groupOverride('cam',     on('noFreeCam'))
  local anchorOn   = groupOverride('anchor',  on('noHomeAnchor'))
  local dropOn     = groupOverride('dropcam', on('noDropAtCam'))
  local bigmapOn   = groupOverride('bigmap',  on('noMapTeleport'))
  local resetOn    = groupOverride('reset',   on('noManualReset') or w.lockReset)

  -- 顺序：先锁复位/传送这类「一键逃逸」，再锁换车，最后锁娱乐。
  applyGroup('resetBypass', GROUP.resetBypass, ACT.resetBypass, groupOverride('resetBypass', on('noPause')))
  applyGroup('reset',       GROUP.reset,       ACT.reset,       resetOn)
  applyGroup('cam',         GROUP.cam,         ACT.cam,         camOn)
  applyGroup('anchor',      GROUP.anchor,      ACT.anchor,      anchorOn)
  applyGroup('dropcam',     GROUP.dropcam,     ACT.dropcam,     dropOn)
  applyGroup('bigmap',      GROUP.bigmap,      ACT.bigmap,      bigmapOn)
  applyGroup('vehicle',     GROUP.vehicle,     ACT.vehicle,     vehOn)
  applyGroup('parts',       GROUP.parts,       ACT.parts,       partsOn)
  -- ⚠️ fun / ai 也按规则拦：staging / countdown 同样算局内。
  --    只看 running 的话，倒计时里按 funBoost / forceField 就能提前窜出去，
  --    而出生位是这一局唯一一次「全员同点起跑」的机会，抢跑直接毁掉公平性。
  applyGroup('fun',         GROUP.fun,         ACT.fun,         funOn)
  applyGroup('ai',          GROUP.ai,          ACT.ai,          aiOn)
  applyGroup('grabber',     GROUP.grabber,     ACT.grabber,     groupOverride('grabber', on('noNodeGrabber')))
  applyGroup('slowmo',      GROUP.slowmo,      ACT.slowmo,      groupOverride('slowmo',  on('noSlowMotion')))
  applyGroup('photo',       GROUP.photo,       ACT.photo,       groupOverride('photo',   on('noPhotoMode')))
  applyGroup('walk',        GROUP.walk,        ACT.walk,        groupOverride('walk',    on('noWalkMode')))
  applyGroup('editor',      GROUP.editor,      ACT.editor,      groupOverride('editor',  on('noWorldEditor')))
  applyGroup('spawn',       GROUP.spawn,       ACT.spawn,
             groupOverride('spawn', on('noSpawnVehicle') or on('noRadialSandbox') or on('noRepair')))
  applyGroup('coupler',     GROUP.coupler,     ACT.coupler,     groupOverride('coupler', on('noTrailer')))
  applyGroup('minimap',     GROUP.minimap,     ACT.minimap,     groupOverride('minimap', on('noMinimap')))

  -- 换挡锁：起跑前（staging / countdown）把挡位按在空档，玩家挂不上去；
  -- 起跑信号一到玩法模块就把 gearLock 放开并给 gearTarget=1，自动挂一档起步。
  applyGroup('gear', GROUP.gear, ACT.gear, groupOverride('gear', w.gearLock))
  pcall(applyGearTarget)

  -- ESC 菜单复位：按配置，任何房间都拦（真正玩法里再加一层）
  applyGroup('menuReset', GROUP.menuReset, ACT.menuReset,
             groupOverride('menuReset', TUNE.blockMenuAlways or on('noManualReset')))

  -- 地图传送猴补丁
  -- ⚠️ 用「本帧应该装 && 实际没装」来驱动重试，而不是直接比 active 与 _patchInstalled：
  --    freeroam_bigMapMode 可能比我们晚加载出来，第一次装不上时 _patchInstalled 是 false，
  --    而 active 也是 true，若直接比就会以为「已一致」而永不重试。
  -- ⚠️ 配置放行了 bigmap/teleport 组就不装（组名意味着什么就兑现什么）；
  --    roundActive 结束时照常还原。
  if bigmapOn then
    if not S._patchInstalled then installTeleportPatch(true) end
  else
    if S._patchInstalled then installTeleportPatch(false) end
  end

  -- 天气 / 时间
  if on('noWeather') then
    if not S._envPatched then installEnvPatch(true) end
  else
    if S._envPatched then installEnvPatch(false) end
  end

  -- 沙盒径向菜单（修车 / 加油 / 生成车 / 刷 AI 的总入口）
  local qaBlocked = active and (on('noRepair') or on('noSpawnVehicle') or on('noRadialSandbox'))
  if qaBlocked then
    if not S._qaPatched then installQuickAccessPatch(true) end
  else
    if S._qaPatched then installQuickAccessPatch(false) end
  end

  pcall(applyDriveFreeze)

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
  S.want.freezeDrive = false
  S.want.rules = {}
  S.want.hasRules = false
  S.freezeApplied = nil
  -- 放行/屏蔽清单一并清掉：不清的话 disableAll 之后配置里的 blockGroups
  -- 还会通过 groupOverride 继续屏蔽（退出玩法=全部放行，必须放干净）
  S.blockSet = nil
  S.allowSet = nil
  S.blockSig = nil
  S.allowSig = nil
  pcall(applyDriveFreeze)

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
  pcall(installEnvPatch, false)
  pcall(installQuickAccessPatch, false)
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
    frozen    = (S.freezeApplied == true),
    lastError = S.lastError,
    blockGroups = S.want.blockGroups or '',
    allowGroups = S.want.allowGroups or '',
    -- 本帧真正生效的规则条数（诊断 / HUD 用）
    ruleCount = (function()
      local n = 0
      local R = S.want.rules or {}
      for i = 1, #RULE_KEYS do
        if R[RULE_KEYS[i]] == true then n = n + 1 end
      end
      return n
    end)(),
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
