-- StartRide 模式适配包（单一真源）
--
-- 一句话：**每个非自由驾驶的联机模式，进局就自带一套「游戏内功能限制 + 位置限制」**，
--         不需要房主在设置里逐条勾；房主只能在模式底线之上收紧，不能放松。
--
-- 为什么单独一个文件：
--   原来 29 条规则只按「同一个 preset + 几个例外」下发，模式之间的差异要靠
--   一堆 if 散落在 startrideMode 里推断。玩家反馈「进了德比还能换车」「进了捉迷藏
--   还能飞出去」—— 根因就是模式自己没有权威的限制清单。
--   这里把「模式 → 限制」做成数据，Mode 只负责读，HUD 和启动器只负责展示。
--
-- 两类限制（对应玩家说的「游戏内的功能和位置限制」）：
--   功能（func）：换车 / 换零件 / 维修 / 挂车 / 暂停 / 慢动作 / 娱乐功能 / AI 代驾 /
--                 节点抓取 / 世界编辑器 / 生成车辆 / 沙盒径向菜单 / 调试菜单 / 天气 /
--                 拍照 / 自由相机 / 下车步行 / 小地图 / 复位 / 空档锁定 / 就位冻结 /
--                 回合时限 / 车型强制 / 同款车
--   位置（pos） ：活动范围（半径 + 出圈宽限 + 出圈后果）/ 地图点击传送 / 位置锚点 /
--                 把车落在相机处
--
-- 两层强度：
--   hard  = 模式底线。**房主也关不掉** —— 关了玩法就不成立（例：警匪能传送＝瞬移逃跑）。
--   limit = 模式默认。房主逐条覆写 / 切预设可以改（例：德比留给玩家有限次复位）。
--
-- ⚠️ 优先级链（见 startrideMode.ruleOn）：
--        hard  >  TUNE 覆写  >  limit  >  PRESET
--    hard 放在最前是刻意的：模式底线不能被房主配置撬掉。
--    反过来「更严」永远有效 —— 房主把某条设成 1，只要不是 hard 里明确为 0 就照办。

local M = {}

-- ---------------------------------------------------------------------------
-- 规则键分域
-- ---------------------------------------------------------------------------
-- 位置类（玩家说的「位置限制」）：只管「能不能换位置 / 能待在多大的地方」
local POS_KEYS = { 'zoneEliminate', 'noMapTeleport', 'noHomeAnchor', 'noDropAtCam' }

-- 功能类（玩家说的「游戏内功能限制」）：其余全部
local FUNC_KEYS = {
  'neutralLock', 'freezeStaging', 'noPause', 'noSlowMotion', 'roundLimit',
  'noVehicleSwap', 'noPartsSwap', 'noRepair', 'noTrailer', 'vehicleEnforce', 'sameModel',
  'noFreeCam', 'noPhotoMode', 'noWalkMode', 'noMinimap',
  'noManualReset', 'noFun', 'noAiTraffic', 'noAiMode', 'noNodeGrabber',
  'noWorldEditor', 'noSpawnVehicle', 'noRadialSandbox', 'noDebugMenu', 'noWeather',
}

-- 全量顺序（功能在前、位置在后；HUD 分栏按这个顺序走）
local ALL_KEYS = {}
do
  local i
  for i = 1, #FUNC_KEYS do ALL_KEYS[#ALL_KEYS + 1] = FUNC_KEYS[i] end
  for i = 1, #POS_KEYS do ALL_KEYS[#ALL_KEYS + 1] = POS_KEYS[i] end
end

local IS_POS, IS_KNOWN = {}, {}
do
  local i
  for i = 1, #POS_KEYS do IS_POS[POS_KEYS[i]] = true; IS_KNOWN[POS_KEYS[i]] = true end
  for i = 1, #FUNC_KEYS do IS_KNOWN[FUNC_KEYS[i]] = true end
end

-- ---------------------------------------------------------------------------
-- 五个模式包
-- ---------------------------------------------------------------------------
-- zone.radius 单位米；0 = 不设活动范围（自由驾驶）。
-- zone.radiusFloor：出生方案算出来的圈如果比它还小，用这个兜底（防止小场地把圈压没）。
local PACKS = {

  free_drive = {
    label = '自由驾驶',
    preset = 'off',
    component = '',
    hard  = {},
    limit = {},
    zone  = { radius = 0, grace = 0, outAction = 'none' },
    widgets = {},
  },

  -- 警匪追逐：追捕的公平性全靠「双方都跑不出去、都不能换车修车」
  cops_robber = {
    label = '警匪追逐',
    preset = 'strict',
    component = 'chase',
    hard = {
      noMapTeleport = 1,   -- 点地图瞬移＝直接甩掉追捕
      noHomeAnchor  = 1,   -- 锚点＝随时回出生点
      noDropAtCam   = 1,   -- 把车落在相机处＝空中换位
      noVehicleSwap = 1,   -- 换车＝换台快车接着跑
      noWalkMode    = 1,   -- 下车就能钻进小路 / 换乘别人的车
    },
    limit = {
      noPartsSwap = 1, noRepair = 1, noTrailer = 1,
      noPause = 1, noSlowMotion = 1,
      noFun = 1, noAiMode = 1, noAiTraffic = 0,   -- 车流留着（混进车流更好玩）
      noNodeGrabber = 1, noWorldEditor = 1, noSpawnVehicle = 1,
      noRadialSandbox = 1, noDebugMenu = 1, noWeather = 1,
      noPhotoMode = 1, noFreeCam = 1,
      noMinimap = 0,       -- 追捕必须看地图
      noManualReset = 1,   -- 复位＝原地脱身
      neutralLock = 1, freezeStaging = 1, roundLimit = 1,
      vehicleEnforce = 0, sameModel = 0,   -- 双方车型自选
      zoneEliminate = 0,   -- 出圈拉回，不直接淘汰（警察误出圈不该判负）
    },
    zone = { radius = 1500, grace = 8, outAction = 'pull', radiusFloor = 900 },
    widgets = { 'chaseRoute', 'chaseSuspect' },
  },

  -- 德比：撞到最后一台。能修能复位（否则翻车＝出局），但绝不能传送 / 换车 / 挂车
  derby = {
    label = '德比',
    preset = 'strict',
    component = 'derby',
    hard = {
      noMapTeleport = 1,
      noHomeAnchor  = 1,
      noDropAtCam   = 1,
      noVehicleSwap = 1,
    },
    limit = {
      noPartsSwap = 1, noRepair = 0,       -- 撞烂了要能修，否则秒出局
      noTrailer = 1,                        -- 挂拖车＝半个无敌
      noPause = 1, noSlowMotion = 1,
      noFun = 1, noAiMode = 1, noAiTraffic = 1,
      noNodeGrabber = 1,                    -- 拖别人的车＝作弊
      noWorldEditor = 1, noSpawnVehicle = 1,
      noRadialSandbox = 1, noDebugMenu = 1, noWeather = 1,
      noPhotoMode = 1, noFreeCam = 1, noWalkMode = 0,  -- 车毁了下车走两步是合理的
      noMinimap = 0,                        -- 要看对手在哪
      noManualReset = 0,                    -- 留复位（翻车要能爬起来），靠次数预算 + 冷却约束
      neutralLock = 1, freezeStaging = 1, roundLimit = 1,
      vehicleEnforce = 0, sameModel = 0,
      zoneEliminate = 0,                    -- 被撞出场边不该判负，拉回来
    },
    zone = { radius = 400, grace = 10, outAction = 'pull', radiusFloor = 260 },
    widgets = { 'derbyBoard' },
  },

  -- 捉迷藏：躲藏方要能藏住，所以「看得见」的限制比「跑得快」的限制更重要
  hide_seek = {
    label = '捉迷藏',
    preset = 'strict',
    component = 'seek',
    hard = {
      noMapTeleport = 1,
      noHomeAnchor  = 1,
      noDropAtCam   = 1,
      noVehicleSwap = 1,   -- 躲好之后换车＝重新刷位置 / 换更快车逃跑
    },
    limit = {
      noPartsSwap = 1, noRepair = 0,        -- 卡住了允许修
      noTrailer = 1,
      noPause = 1, noSlowMotion = 1,
      noFun = 1, noAiMode = 1, noAiTraffic = 0,   -- 车流是玩法的一部分（混进 AI 车）
      noNodeGrabber = 1, noWorldEditor = 1, noSpawnVehicle = 1,
      noRadialSandbox = 1, noDebugMenu = 1, noWeather = 1,
      noPhotoMode = 1, noFreeCam = 1, noWalkMode = 1,
      -- 小地图会直接把躲藏着的位置标出来（躲藏方一眼看穿搜索者路线），锁掉
      noMinimap = 1,
      -- 复位在捉迷藏里等同于「换个位置重新藏」，一律拒绝；
      -- 按键层也锁掉，这样玩家按下去直接没反应，而不是按了再被拒（少一次困惑）
      noManualReset = 1,
      neutralLock = 1, freezeStaging = 1, roundLimit = 1,
      vehicleEnforce = 0, sameModel = 0,
      zoneEliminate = 0,                    -- 出圈拉回（跑出图外＝耍赖）
    },
    zone = { radius = 2600, grace = 12, outAction = 'pull', radiusFloor = 1500 },
    widgets = { 'seekHunt' },
  },

  -- 赛道日：唯一在乎的是「别抄近道、别用娱乐功能刷圈」
  track_day = {
    label = '赛道日',
    preset = 'loose',
    component = 'track',
    hard = {
      noMapTeleport = 1,   -- 传送＝直接跳到终点
      noHomeAnchor  = 1,
      noDropAtCam   = 1,
    },
    limit = {
      noPartsSwap = 0,     -- 练车 / 调校随便
      noRepair = 0,
      noTrailer = 1,
      noPause = 1, noSlowMotion = 1,
      noFun = 1, noAiMode = 1, noAiTraffic = 1,   -- 圈速不能靠 AI 代驾
      noNodeGrabber = 1, noWorldEditor = 1, noSpawnVehicle = 1,
      noRadialSandbox = 1, noDebugMenu = 1, noWeather = 1,
      noPhotoMode = 0, noFreeCam = 1, noWalkMode = 0,
      noMinimap = 0,
      noManualReset = 1,   -- 冲出去了复位＝白送一次近道
      neutralLock = 1, freezeStaging = 1, roundLimit = 1,
      vehicleEnforce = 0, sameModel = 0,
      zoneEliminate = 0,
    },
    zone = { radius = 1800, grace = 12, outAction = 'pull', radiusFloor = 1200 },
    widgets = { 'trackLap' },
  },
}

-- ---------------------------------------------------------------------------
-- 查询接口
-- ---------------------------------------------------------------------------
local FALLBACK = PACKS.free_drive

local function packOf(mode)
  local p = PACKS[tostring(mode or '')]
  if type(p) == 'table' then return p end
  return FALLBACK
end

function M.modes()
  local out = {}
  for k in pairs(PACKS) do out[#out + 1] = k end
  table.sort(out)
  return out
end

function M.has(mode) return type(PACKS[tostring(mode or '')]) == 'table' end

function M.label(mode) return packOf(mode).label end

function M.component(mode) return packOf(mode).component or '' end

function M.preset(mode) return packOf(mode).preset or 'off' end

-- 模式自己写明的键（limit 与 hard 合并，hard 覆盖）。
-- 和 M.limits 的区别：limits 会把预设铺满 29 键，explicit 只留「本模式特意规定的」——
-- 房主切换严格度预设时，只有显式项不被影响，其余仍按预设走。
function M.explicit(mode)
  local p = packOf(mode)
  local out = {}
  local k, v
  local lim = p.limit
  if type(lim) == 'table' then
    for k, v in pairs(lim) do
      if IS_KNOWN[k] then out[k] = (tonumber(v) or 0) > 0 end
    end
  end
  local hard = p.hard
  if type(hard) == 'table' then
    for k, v in pairs(hard) do
      if IS_KNOWN[k] then out[k] = (tonumber(v) or 0) > 0 end
    end
  end
  return out
end

function M.zone(mode)
  local z = packOf(mode).zone
  if type(z) ~= 'table' then return { radius = 0, grace = 0, outAction = 'none' } end
  return z
end

function M.widgets(mode)
  local w = packOf(mode).widgets
  if type(w) ~= 'table' then return {} end
  return w
end

function M.isPos(key) return IS_POS[key] == true end

function M.isKnown(key) return IS_KNOWN[key] == true end

function M.keys() return ALL_KEYS end

function M.funcKeys() return FUNC_KEYS end

function M.posKeys() return POS_KEYS end

-- 模式底线：房主不可关闭的键 → true
function M.hard(mode)
  local h = packOf(mode).hard
  if type(h) ~= 'table' then return {} end
  return h
end

-- 展开成「29 键齐全」的布尔表：先铺 limit，再让 hard 覆盖（底线永远赢）
function M.limits(mode)
  local p = packOf(mode)
  local out = {}
  local i, k
  for i = 1, #ALL_KEYS do out[ALL_KEYS[i]] = false end
  local lim = p.limit
  if type(lim) == 'table' then
    for k, v in pairs(lim) do
      if IS_KNOWN[k] then out[k] = (tonumber(v) or 0) > 0 end
    end
  end
  local hard = p.hard
  if type(hard) == 'table' then
    for k, v in pairs(hard) do
      if IS_KNOWN[k] then out[k] = (tonumber(v) or 0) > 0 end
    end
  end
  return out
end

-- 统计：功能几项 / 位置几项（HUD 标题与启动器提示用）
function M.counts(mode)
  local lim = M.limits(mode)
  local f, p = 0, 0
  local i
  for i = 1, #FUNC_KEYS do if lim[FUNC_KEYS[i]] then f = f + 1 end end
  for i = 1, #POS_KEYS  do if lim[POS_KEYS[i]]  then p = p + 1 end end
  return f, p
end

-- 给 HUD / 日志用的结构化描述（纯数据，不含文案 —— 说明文字仍由 Mode 的规则目录提供）
function M.describe(mode)
  local p = packOf(mode)
  local lim = M.limits(mode)
  local funcs, poss = {}, {}
  local i
  for i = 1, #FUNC_KEYS do
    if lim[FUNC_KEYS[i]] then funcs[#funcs + 1] = FUNC_KEYS[i] end
  end
  for i = 1, #POS_KEYS do
    if lim[POS_KEYS[i]] then poss[#poss + 1] = POS_KEYS[i] end
  end
  local hardList = {}
  local hard = p.hard
  if type(hard) == 'table' then
    for k, v in pairs(hard) do
      if (tonumber(v) or 0) > 0 then hardList[#hardList + 1] = k end
    end
    table.sort(hardList)
  end
  return {
    key       = tostring(mode or ''),
    label     = p.label,
    component = p.component or '',
    funcs     = funcs,
    poss      = poss,
    funcCount = #funcs,
    posCount  = #poss,
    hard      = hardList,
    zone      = M.zone(mode),
    widgets   = M.widgets(mode),
  }
end

-- 位置限制的有效圈半径：模式包给基线，房主配置只能「更严」
-- （以前是 TUNE.zoneRadius 一刀切 1200m，德比场地和全图捉迷藏都用同一个数，明显不合理）
function M.zoneRadius(mode, tuneRadius)
  local z = M.zone(mode)
  local base = tonumber(z.radius) or 0
  local tune = tonumber(tuneRadius) or 0
  if base <= 0 then return 0 end
  if tune > 0 and tune < base then return tune end
  return base
end

return M
