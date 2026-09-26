
if v.srHLModule then return v.srHLModule end

-- ═══════════════════════════════════════════════════════════════════════════
-- StartRide 高光时刻捕捉（车辆层 VE）
--
-- 干一件事：盯住**玩家自己开的这辆车**，认出「值得回看的瞬间」，然后通过
-- obj:queueGameEngineLua 把事件交给 GE 侧（startride.onHighlight）去汇总、截图、落盘。
--
-- 认哪五类（阈值都在下面 constants 里，可调）：
--   jump      大跳跃 —— 四轮离地 ≥ JMP_MIN_AIRTIME 秒，附带空中水平位移
--   impact    重击   —— 耗散能量（obj:getDissipatedEnergy）一次跳变超过阈值
--   rollover  翻车   —— 车顶朝下持续 ROLL_MIN 秒后又翻正
--   burnout   烧胎   —— 驱动轮打滑持续 ≥ BURNOUT_MIN 秒
--   topspeed  极速   —— 本次会话最高地速，每提升 TOP_STEP 报一次
--
-- ⚠️ 判定逻辑写成**纯函数** M.step(sample, dt)：sample 是一个普通表，
--    不碰任何引擎全局。引擎相关的取数在 collect() 里。这样这段逻辑可以脱离游戏
--    用 lupa / 真 Lua 跑离线单测（见 _sr_shots/_hl_unit.py），不必每次开车试。
--
-- ⚠️ 腾空判定照抄引擎自带的 lua/vehicle/extensions/gameplayStatisticModules/watchAirtime.lua：
--    每个轮子都 contactMaterialID1 == -1 且 contactMaterialID2 == -1（没接触材质=没着地），
--    且 obj:getGroundSpeed() > 6，且车身 up 向量的 z > 0.707（45° 以内）。
--    直接读轮子的 contactMaterial 是本模块必须待在 VE 而不是 GE 的唯一原因 ——
--    GE 侧拿不到接触材质（只有 getWheelAxisNodes/getNodePosition，推不出触地）。
--
-- ⚠️ 只对**玩家乘坐**的车生效（playerInfo.anyPlayerSeated），且跳过远程车
--    （v.srServerID 非空 = 对方发来的车，那是别人在开）。判定依据与引擎自带的
--    lua/vehicle/extensions/gameplayStatistic.lua 的 updateGFX 守卫一致。
--
-- ⚠️ 顺序铁律：Lua 的 local function 只在其定义之后可见，之前引用会解析成全局 nil
--    （被 pcall 包住就静默失效）。本文件按「常量 → 纯函数 → 取数 → 上报 → 每帧」排序。
-- ═══════════════════════════════════════════════════════════════════════════

local M = {}

-- ── 可调阈值 ────────────────────────────────────────────────────────────────
local JMP_MIN_AIRTIME  = 1.2     -- 秒；引擎自带的 watchAirtime 只要 0.1s 就记（那是统计总时长），
                                 -- 这里要的是"值得回看"，所以抬高一个量级
local JMP_MIN_SPEED    = 6       -- m/s；低速蹭起来的"离地"不算
local JMP_MIN_UPZ      = 0.707   -- 车身 up.z 下限（45°），歪着飞出去不算跳跃
local IMPACT_MIN       = 40000   -- 耗散能量一次跳变阈值（BeamNG 单位，实测校准）
local ROLL_MIN_UPZ     = -0.7    -- 车顶朝下的判据（照抄 watchRollover）
local ROLL_MIN         = 0.6     -- 秒；翻过来又马上翻回去不算
local BURNOUT_MIN      = 1.0     -- 秒
local TOP_MIN_GS       = 25      -- m/s（90 km/h）；低于这个速度的"最高速"没有回看价值
local TOP_STEP         = 8       -- m/s；极速每提升这么多报一次
-- 同一类型的最小上报间隔（秒）。逐类型设而不是一个常量统一用：
-- 跳跃连续两次是常见玩法（连环飞坡），用 6s 会把第二次直接吞掉；
-- 而重击一次碰撞会连着掉好几帧能量，必须给冷却。
local COOLDOWN = { jump = 2.5, impact = 2.5, rollover = 3, burnout = 5 }
local COOLDOWN_DEFAULT = 6

-- ── 纯函数状态 ──────────────────────────────────────────────────────────────
-- 全部塞在一个 table 里，M.reset() 直接换一个新的，没有模块级散落变量
local S = nil

local function newState()
  return {
    t = 0,                 -- 累计仿真时间（本模块自己的时钟，不依赖引擎）
    airFrom = nil,         -- 本次腾空的开始时间
    airPos = nil,          -- 本次腾空的开始水平位置
    lastUpZ = 1,
    roofFrom = nil,        -- 车顶朝下的开始时间
    burnFrom = nil,
    topSpeed = 0,
    topSpeedInit = false,  -- 首帧只建立极速基准，不判定（否则一上车就报一条）
    lastDamage = nil,      -- 上一次的耗散能量；nil = 还没建立基准
    lastReport = {},       -- 类型 → 上次上报时间
  }
end

S = newState()

--- 冷却是否允许上报。jump / impact / rollover / burnout 四类都要用它。
--- 极速不用它："每提升 TOP_STEP 才报一次"这道闸本身就够紧了。
---@param st table
---@param typ string
local function ready(st, typ)
  local last = st.lastReport[typ]
  local cd = COOLDOWN[typ] or COOLDOWN_DEFAULT
  if last and (st.t - last) < cd then return false end
  st.lastReport[typ] = st.t
  return true
end

--- 大跳跃：全部轮子离地 + 有速度 + 车身基本水平。
--- 抽成独立函数方便单测（sample 里的字段都是纯数字/布尔）。
local function detectJump(st, sample)
  -- 还在空中：记起点
  if sample.airborne then
    if not st.airFrom then
      st.airFrom = st.t
      -- ⚠️ 必须**复制**，不能存引用：sample.pos 指向复用的 posCache 表，
      --    存引用的话落地时读到的已经是落点坐标，位移恒为 0（离线单测抓到过）。
      --    起飞每局只有几次，这点分配无所谓。
      st.airPos = sample.pos and { sample.pos[1], sample.pos[2] } or nil
    end
    return nil
  end

  -- 落地了
  if not st.airFrom then return nil end
  local airTime = st.t - st.airFrom
  local startPos = st.airPos
  st.airFrom = nil
  st.airPos = nil

  if airTime < JMP_MIN_AIRTIME then return nil end
  if not ready(st, 'jump') then return nil end

  -- 空中水平位移（起点到落地点的水平距离）—— 这个数比"滞空几秒"更有画面感
  local dist = 0
  if startPos and sample.pos then
    local dx = sample.pos[1] - startPos[1]
    local dy = sample.pos[2] - startPos[2]
    dist = math.sqrt(dx * dx + dy * dy)
  end
  return { type = 'jump', value = airTime, extra = dist, speed = sample.gs }
end

--- 重击：耗散能量一次跳变。返回 nil 表示没到阈值（或还在建立基准）。
local function detectImpact(st, sample)
  if sample.damage == nil then return nil end

  -- 第一次见到这个数只当基准，不判定 —— 否则上车/重生瞬间的大数值会被当成撞车
  if st.lastDamage == nil then
    st.lastDamage = sample.damage
    return nil
  end

  local delta = sample.damage - st.lastDamage
  -- 重生/换车会让耗散能量归零，负增量直接当新基准
  if delta < 0 then
    st.lastDamage = sample.damage
    return nil
  end

  st.lastDamage = sample.damage
  if delta < IMPACT_MIN then return nil end
  if not ready(st, 'impact') then return nil end
  return { type = 'impact', value = delta, extra = 0, speed = sample.gs }
end

--- 翻车：车顶朝下持续一会儿，然后翻回来才报（翻到一半又倒回去不算）。
local function detectRollover(st, sample)
  if sample.upZ < ROLL_MIN_UPZ then
    if not st.roofFrom then st.roofFrom = st.t end
    return nil
  end

  if not st.roofFrom then return nil end
  local dur = st.t - st.roofFrom
  st.roofFrom = nil
  if dur < ROLL_MIN then return nil end
  if not ready(st, 'rollover') then return nil end
  return { type = 'rollover', value = dur, extra = 0, speed = sample.gs }
end

--- 烧胎：驱动轮打滑持续。判定条件照抄 watchBurnout，只留"持续时长"这一条门槛。
local function detectBurnout(st, sample)
  if sample.burnout then
    if not st.burnFrom then st.burnFrom = st.t end
    return nil
  end

  if not st.burnFrom then return nil end
  local dur = st.t - st.burnFrom
  st.burnFrom = nil
  if dur < BURNOUT_MIN then return nil end
  if not ready(st, 'burnout') then return nil end
  return { type = 'burnout', value = dur, extra = 0, speed = sample.gs }
end

--- 极速：单调爬升，每 TOP_STEP 报一次。
--- 三道闸：① 首帧只建基准 ② 低于 TOP_MIN_GS 不算 ③ 没刷新纪录不算。
--- 这三道缺一不可：少了①每次上车都立刻报一条"极速=当前速度"，
--- 少了②在停车场挪车也能刷出"极速 12 km/h"。
local function detectTopSpeed(st, sample)
  if not st.topSpeedInit then
    st.topSpeedInit = true
    st.topSpeed = sample.gs
    return nil
  end
  if sample.gs < TOP_MIN_GS then return nil end
  if sample.gs < st.topSpeed + TOP_STEP then return nil end
  st.topSpeed = sample.gs
  return { type = 'topspeed', value = sample.gs, extra = 0, speed = sample.gs }
end

--- 推进一帧，返回本帧产生的全部高光事件（通常 0 个或 1 个）。
--- ⚠️ 这是纯函数：只读 sample、只写 st，不碰 obj / wheels / beamstate。
---@param sample table {gs, upZ, airborne, burnout, damage, pos}
---@param dt number 本帧仿真时间步长（秒）
---@return table|nil 事件，形如 {type, value, extra, speed}
function M.step(sample, dt)
  if not S then S = newState() end
  local st = S

  st.t = st.t + (tonumber(dt) or 0)
  st.lastUpZ = sample.upZ

  -- 顺序有讲究：跳跃和翻车都依赖"进入/退出某状态"的边沿，先判跳跃再判翻车
  local ev = detectJump(st, sample)
  if not ev then ev = detectImpact(st, sample) end
  if not ev then ev = detectRollover(st, sample) end
  if not ev then ev = detectBurnout(st, sample) end
  if not ev then ev = detectTopSpeed(st, sample) end
  return ev
end

--- 清空状态（换车/重生时调）。注意 lastDamage 也一并清掉，
--- 免得重生后归零的耗散能量被当成一次重击。
function M.reset()
  S = newState()
end

--- 供离线单测与 HUD 调试查看。
function M.debugState()
  return {
    t = S and S.t or 0,
    topSpeed = S and S.topSpeed or 0,
    airborne = (S and S.airFrom) ~= nil or false,
    roofed = (S and S.roofFrom) ~= nil or false,
  }
end


-- ═══════════════ 以下是与引擎打交道的一层 ═══════════════

-- 复用的表：每帧新建 table 会产生垃圾，长局游戏 GC 抖动明显
local sample = { gs = 0, upZ = 1, airborne = false, burnout = false, damage = nil, pos = nil }
local posCache = { 0, 0, 0 }

local burnoutWheels = nil      -- 驱动轮索引表；只建一次
local burnoutWheelsValid = false
local playerVehicle = false    -- 本车是不是玩家在开的
local enabled = true

local function logI(...)
  local parts = {}
  for _, x in ipairs({ ... }) do parts[#parts + 1] = tostring(x) end
  pcall(function() log('I', 'startride', '[StartRide HL] ' .. table.concat(parts, ' ')) end)
end

-- ⚠️ 上报走 queueGameEngineLua 传一段代码字符串，所以每个数字都必须用 %.3f 格式化：
--    直接用 tostring 在极端值下会得到 "1e+18" 这种科学计数法，GE 侧解析当场炸。
local function report(ev)
  pcall(function()
    obj:queueGameEngineLua(string.format(
      "startride.onHighlight('%s', %.3f, %.3f, %.3f)",
      ev.type, ev.value or 0, ev.extra or 0, ev.speed or 0))
  end)
end

--- 把引擎状态摊平成 M.step 认识的 sample。全字段都做了 nil 兜底 ——
--- 出问题时宁可这一帧按"没有高光"处理，也不要抛错把整个 updateGFX 打断。
local function collect()
  local ok, gs = pcall(function() return obj:getGroundSpeed() end)
  sample.gs = (ok and tonumber(gs)) or 0

  local upOk, _, _, uz = pcall(function() return obj:getDirectionVectorUpXYZ() end)
  sample.upZ = (upOk and tonumber(uz)) or 1

  -- 腾空：四轮都没有接触材质（照抄 watchAirtime）
  local airborne = sample.gs > JMP_MIN_SPEED and sample.upZ > JMP_MIN_UPZ
  if airborne and wheels and wheels.wheelCount and wheels.wheelCount > 0 then
    for i = 0, wheels.wheelCount - 1 do
      local wd = wheels.wheels[i]
      if not wd or wd.contactMaterialID1 ~= -1 or wd.contactMaterialID2 ~= -1 then
        airborne = false
        break
      end
    end
  else
    airborne = false
  end
  sample.airborne = airborne

  -- 烧胎：驱动轮打滑（照抄 watchBurnout 的判定式）
  local burnout = false
  if burnoutWheelsValid and wheels and wheels.wheels then
    local limit = sample.gs * 1.5
    for _, wi in ipairs(burnoutWheels) do
      local wd = wheels.wheels[wi]
      if wd and wd.isBroken == false and wd.wheelSpeed > 2 and wd.wheelSpeed > limit
         and (wd.lastSlip * math.min((wd.downForce or 0) * 0.1, 1)) > 4
         and wd.contactMaterialID1 ~= -1 and wd.contactMaterialID2 ~= -1 then
        burnout = true
        break
      end
    end
  end
  sample.burnout = burnout

  -- 耗散能量：BeamNG 里就是 damage 指标（beamstate.lua: M.damage = obj:getDissipatedEnergy() + damageExt）
  local dOk, dmg = pcall(function() return obj:getDissipatedEnergy() end)
  sample.damage = dOk and tonumber(dmg) or nil

  local pOk, px, py = pcall(function() return obj:getPositionXYZ() end)
  if pOk and tonumber(px) and tonumber(py) then
    posCache[1] = tonumber(px)
    posCache[2] = tonumber(py)
    posCache[3] = 0
    sample.pos = posCache
  else
    sample.pos = nil
  end
end

--- 只建一次驱动轮索引表（照抄 watchBurnout 的 onExtensionLoaded 做法）。
local function buildBurnoutWheels()
  burnoutWheels = {}
  if not wheels or not wheels.wheelCount or wheels.wheelCount <= 0 then
    burnoutWheelsValid = false
    return
  end
  for i = 0, wheels.wheelCount - 1 do
    local wd = wheels.wheels[i]
    if wd and wd.isPropulsed then burnoutWheels[#burnoutWheels + 1] = i end
  end
  burnoutWheelsValid = #burnoutWheels > 0
end

--- 本车是不是"玩家在开的那辆"。
--- 判据两条：① 不是远程车（v.srServerID 为空）② 车里坐着玩家（playerInfo.anyPlayerSeated）。
--- 第二条与引擎自带的 gameplayStatistic.updateGFX 守卫写法一致；AI 车 / 未入座的车会被排除。
local function refreshPlayerFlag()
  local isRemote = v.srServerID ~= nil and v.srServerID ~= ''
  if isRemote then
    playerVehicle = false
    return
  end
  local ok, seated = pcall(function() return playerInfo and playerInfo.anyPlayerSeated end)
  playerVehicle = ok and seated == true
end

local function onInit()
  M.reset()
  enabled = true
  buildBurnoutWheels()
  refreshPlayerFlag()
  logI('高光检测已加载 wheelCount=' .. tostring(wheels and wheels.wheelCount or '?')
    .. ' 驱动轮=' .. tostring(#(burnoutWheels or {})))
end

local function updateGFX(dt)
  if not enabled then return end
  if not dt or dt <= 0 then return end

  -- 每帧刷新一次"是不是玩家车"：玩家上车/下车、被 GE 标成远程车都会变
  refreshPlayerFlag()
  if not playerVehicle then
    -- 不在状态里的车必须清干净，否则下次上车会把"上一辆车的滞空开始时间"接上去
    if S and (S.airFrom or S.roofFrom or S.burnFrom) then M.reset() end
    return
  end

  collect()
  local ev = M.step(sample, dt)
  if ev then report(ev) end
end

--- 换车/重生：状态清零，驱动轮表重建（换车后驱动轮索引会变）
local function onReset()
  M.reset()
  buildBurnoutWheels()
end

--- GE 侧下发开关（联机中才开检测可以省一点开销，目前默认常开）
function M.setEnabled(value)
  enabled = value == true
  if not enabled then M.reset() end
end

M.onInit = onInit
M.onExtensionLoaded = onInit
M.onReset = onReset
M.updateGFX = updateGFX

v.srHLModule = M
return M
