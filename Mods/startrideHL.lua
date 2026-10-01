
if v.srHLModule then return v.srHLModule end

local M = {}

local JMP_MIN_AIRTIME  = 1.2
local JMP_MIN_SPEED    = 6
local JMP_MIN_UPZ      = 0.707
local IMPACT_MIN       = 40000
local ROLL_MIN_UPZ     = -0.7
local ROLL_MIN         = 0.6
local BURNOUT_MIN      = 1.0
local TOP_MIN_GS       = 25
local TOP_STEP         = 8

local COOLDOWN = { jump = 2.5, impact = 2.5, rollover = 3, burnout = 5 }
local COOLDOWN_DEFAULT = 6

local S = nil

local function newState()
  return {
    t = 0,
    airFrom = nil,
    airPos = nil,
    lastUpZ = 1,
    roofFrom = nil,
    burnFrom = nil,
    topSpeed = 0,
    topSpeedInit = false,
    lastDamage = nil,
    lastReport = {},
  }
end

S = newState()

local function ready(st, typ)
  local last = st.lastReport[typ]
  local cd = COOLDOWN[typ] or COOLDOWN_DEFAULT
  if last and (st.t - last) < cd then return false end
  st.lastReport[typ] = st.t
  return true
end

local function detectJump(st, sample)
  if sample.airborne then
    if not st.airFrom then
      st.airFrom = st.t

      st.airPos = sample.pos and { sample.pos[1], sample.pos[2] } or nil
    end
    return nil
  end

  if not st.airFrom then return nil end
  local airTime = st.t - st.airFrom
  local startPos = st.airPos
  st.airFrom = nil
  st.airPos = nil

  if airTime < JMP_MIN_AIRTIME then return nil end
  if not ready(st, 'jump') then return nil end

  local dist = 0
  if startPos and sample.pos then
    local dx = sample.pos[1] - startPos[1]
    local dy = sample.pos[2] - startPos[2]
    dist = math.sqrt(dx * dx + dy * dy)
  end
  return { type = 'jump', value = airTime, extra = dist, speed = sample.gs }
end

local function detectImpact(st, sample)
  if sample.damage == nil then return nil end

  if st.lastDamage == nil then
    st.lastDamage = sample.damage
    return nil
  end

  local delta = sample.damage - st.lastDamage
  if delta < 0 then
    st.lastDamage = sample.damage
    return nil
  end

  st.lastDamage = sample.damage
  if delta < IMPACT_MIN then return nil end
  if not ready(st, 'impact') then return nil end
  return { type = 'impact', value = delta, extra = 0, speed = sample.gs }
end

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

function M.step(sample, dt)
  if not S then S = newState() end
  local st = S

  st.t = st.t + (tonumber(dt) or 0)
  st.lastUpZ = sample.upZ

  local ev = detectJump(st, sample)
  if not ev then ev = detectImpact(st, sample) end
  if not ev then ev = detectRollover(st, sample) end
  if not ev then ev = detectBurnout(st, sample) end
  if not ev then ev = detectTopSpeed(st, sample) end
  return ev
end

function M.reset()
  S = newState()
end

function M.debugState()
  return {
    t = S and S.t or 0,
    topSpeed = S and S.topSpeed or 0,
    airborne = (S and S.airFrom) ~= nil or false,
    roofed = (S and S.roofFrom) ~= nil or false,
  }
end

local sample = { gs = 0, upZ = 1, airborne = false, burnout = false, damage = nil, pos = nil }
local posCache = { 0, 0, 0 }

local burnoutWheels = nil
local burnoutWheelsValid = false
local playerVehicle = false
local enabled = true

local function logI(...)
  local parts = {}
  for _, x in ipairs({ ... }) do parts[#parts + 1] = tostring(x) end
  pcall(function() log('I', 'startride', '[StartRide HL] ' .. table.concat(parts, ' ')) end)
end

local function report(ev)
  pcall(function()
    obj:queueGameEngineLua(string.format(
      "startride.onHighlight('%s', %.3f, %.3f, %.3f)",
      ev.type, ev.value or 0, ev.extra or 0, ev.speed or 0))
  end)
end

local function collect()
  local ok, gs = pcall(function() return obj:getGroundSpeed() end)
  sample.gs = (ok and tonumber(gs)) or 0

  local upOk, _, _, uz = pcall(function() return obj:getDirectionVectorUpXYZ() end)
  sample.upZ = (upOk and tonumber(uz)) or 1

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

  refreshPlayerFlag()
  if not playerVehicle then
    if S and (S.airFrom or S.roofFrom or S.burnFrom) then M.reset() end
    return
  end

  collect()
  local ev = M.step(sample, dt)
  if ev then report(ev) end
end

local function onReset()
  M.reset()
  buildBurnoutWheels()
end

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
