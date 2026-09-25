





































if v.srVEModule then return v.srVEModule end

local M = {}


v.mpVehicleType = 'L'
v.srServerID = ''

local abs = math.abs
local min = math.min
local max = math.max



local posCorrectMul = 5        
local posForceMul = 5          
local minPosForce = 0.04       
local maxPosForce = 100        
local maxAcc = 100             
local maxAccError = 3          

local rotCorrectMul = 7        
local rotForceMul = 7
local minRotForce = 0.02
local maxRotForce = 50
local maxRacc = 50
local maxRaccError = 3

local tpDelayAdd = 1           
local tpDistAdd = 1            
local tpDistMul1 = 0.1         
local tpDistMul2 = 0.5         
local tpRotAdd = 0.5
local tpRotMul1 = 0.2
local tpRotMul2 = 0.5

local maxPredict = 0.3
local packetTimeout = 0.30     

local remoteVelSmoothRate = 2
local remoteAccSmoothRate = 1
local errSmoothRate = 50


local timer = 0
local lastDT = 0
local framesSinceReset = 0    
local tpTimer = 0
local active = false

local pendingSnap = false     
local lastTpAt = -100         
local minTpGap = 0.25         
local tpCount = 0             
local nanDropCount = 0        
local badDataWarned = false
local lastMailboxVersion = -1
local physicsFPS = 2000
local refNode = nil
local refNodeResolved = false
local staleWarned = false
local readyLogged = false

local remoteData = {
  pos = nil, vel = nil, acc = nil, rot = nil, rvel = nil, racc = nil,
  tim = -1, recvAt = 0,
}

local lastVehVel = nil
local lastVehRvel = nil
local lastAcc = nil

local lastAccV, lastRaccV, hasLastAcc = nil, nil, false
local lastRacc = nil


local dir, dirUp, rotQ, pos, vel, rvel
local vehRot, vehPos, vehVel, vehRvel, vehAcc, vehRacc
local cogTmp, forceVec, zeroVec, tmpVec

local sm = {}  

local function newSmoother(rate)
  return { rate = rate, x = 0, y = 0, z = 0 }
end

local function smGet(s, sx, sy, sz, dt)
  local r = min(s.rate * dt, 1)
  s.x = s.x + (sx - s.x) * r
  s.y = s.y + (sy - s.y) * r
  s.z = s.z + (sz - s.z) * r
  return s.x, s.y, s.z
end

local function smSet(s, sx, sy, sz)
  s.x, s.y, s.z = sx, sy, sz
end

local function smReset(s)
  s.x, s.y, s.z = 0, 0, 0
end

local function isFinite(v)
  return type(v) == 'number' and v == v and v ~= math.huge and v ~= -math.huge
end

local function finite3(a, b, c)
  return isFinite(a) and isFinite(b) and isFinite(c)
end

local function len3(x, y, z)
  return math.sqrt(x * x + y * y + z * z)
end

local function clamp3(x, y, z, maxLen)
  local l = len3(x, y, z)
  if l > maxLen and l > 0 then
    local s = maxLen / l
    return x * s, y * s, z * s
  end
  return x, y, z
end






local function quatOk(x, y, z, w)
  if not (isFinite(x) and isFinite(y) and isFinite(z) and isFinite(w)) then return false end
  local n = x * x + y * y + z * z + w * w
  return n > 0.25 and n < 2.5
end


local function posOk(x, y, z)
  if not finite3(x, y, z) then return false end
  return (x * x + y * y + z * z) < 1e12
end

local function logI(...)
  local parts = {}
  for _, x in ipairs({ ... }) do parts[#parts + 1] = tostring(x) end
  pcall(function() log('I', 'startride', '[StartRide VE] ' .. table.concat(parts, ' ')) end)
end

local function ensureInit()
  if dir then return end
  dir = vec3()
  dirUp = vec3()
  rotQ = quat()
  pos = vec3()
  vel = vec3()
  rvel = vec3()
  vehRot = quat()
  vehPos = vec3()
  vehVel = vec3()
  vehRvel = vec3()
  vehAcc = vec3()
  vehRacc = vec3()
  cogTmp = vec3()
  forceVec = vec3()
  zeroVec = vec3(0, 0, 0)
  tmpVec = vec3()
  lastAccV = vec3()
  lastRaccV = vec3()
  remoteData.pos = vec3(0, 0, 0)
  remoteData.vel = vec3(0, 0, 0)
  remoteData.acc = vec3(0, 0, 0)
  remoteData.rot = quat(0, 0, 0, 1)
  remoteData.rvel = vec3(0, 0, 0)
  remoteData.racc = vec3(0, 0, 0)
  sm.remoteVel = newSmoother(remoteVelSmoothRate)
  sm.remoteRvel = newSmoother(remoteVelSmoothRate)
  sm.remoteAcc = newSmoother(remoteAccSmoothRate)
  sm.remoteRacc = newSmoother(remoteAccSmoothRate)
  sm.accError = newSmoother(errSmoothRate)
  sm.raccError = newSmoother(errSmoothRate)
end


local function getRefNode()
  if refNodeResolved then return refNode end
  pcall(function()
    if v and v.data and v.data.refNodes and v.data.refNodes[0] and v.data.refNodes[0].ref then
      refNode = v.data.refNodes[0].ref
    else
      refNode = obj:getRefNodeId()
    end
  end)
  if refNode then refNodeResolved = true end
  return refNode
end



local function getVehRot()
  local dx, dy, dz = obj:getDirectionVectorXYZ()
  local ux, uy, uz = obj:getDirectionVectorUpXYZ()
  dir:set(-dx, -dy, -dz)
  dirUp:set(ux, uy, uz)
  vehRot:setFromDir(dir, dirUp)
  return vehRot
end


local function setVehicleType(t)
  v.mpVehicleType = t
end

local function setServerID(id)
  v.srServerID = id
  lastMailboxVersion = -1
end



local function applyTargetRot(rt)
  if type(rt) ~= 'table' then return false end
  if not quatOk(rt[1], rt[2], rt[3], rt[4]) then return false end
  remoteData.rot:set(rt[1], rt[2], rt[3], rt[4])
  return true
end


local function setTargetPos(jsonStr)
  ensureInit()
  local ok, pr = pcall(jsonDecode, jsonStr)
  if not ok or type(pr) ~= 'table' then return end

  local p, rt = pr.pos, pr.rot
  
  if type(p) ~= 'table' or not posOk(p[1], p[2], p[3]) then
    nanDropCount = nanDropCount + 1
    if not badDataWarned then
      badDataWarned = true
      logI('丢弃非法位置包（坐标异常），已丢', nanDropCount, '次')
    end
    return
  end

  local tim = tonumber(pr.tim) or 0
  
  local reset = (remoteData.tim < 0) or (tim < remoteData.tim) or (tim - remoteData.tim > 3)

  if reset then
    remoteData.tim = tim
    remoteData.pos:set(p[1], p[2], p[3])
    remoteData.vel:set(0, 0, 0)
    remoteData.rvel:set(0, 0, 0)
    remoteData.acc:set(0, 0, 0)
    remoteData.racc:set(0, 0, 0)
    remoteData.rot:set(0, 0, 0, 1)
    if type(rt) == 'table' and finite3(rt[1], rt[2], rt[3]) and isFinite(rt[4]) then
      remoteData.rot:set(rt[1], rt[2], rt[3], rt[4])
    end
    smReset(sm.remoteVel); smReset(sm.remoteRvel)
    smReset(sm.remoteAcc); smReset(sm.remoteRacc)
    smReset(sm.accError); smReset(sm.raccError)
    lastAcc, lastRacc = nil, nil
    framesSinceReset = 0
    tpTimer = 0
    active = true
    staleWarned = false
    remoteData.recvAt = timer
    return
  end

  local dt = max(tim - remoteData.tim, 0.001)

  local vx, vy, vz = 0, 0, 0
  if type(pr.vel) == 'table' and finite3(pr.vel[1], pr.vel[2], pr.vel[3]) then
    vx, vy, vz = pr.vel[1], pr.vel[2], pr.vel[3]
  end
  local rx, ry, rz = 0, 0, 0
  if type(pr.rvel) == 'table' and finite3(pr.rvel[1], pr.rvel[2], pr.rvel[3]) then
    rx, ry, rz = pr.rvel[1], pr.rvel[2], pr.rvel[3]
  end

  
  remoteData.acc:set(vx - remoteData.vel.x, vy - remoteData.vel.y, vz - remoteData.vel.z)
  remoteData.acc:setScaled(1 / dt)
  local ax, ay, az = clamp3(remoteData.acc.x, remoteData.acc.y, remoteData.acc.z, maxAcc)
  remoteData.acc:set(ax, ay, az)

  remoteData.racc:set(rx - remoteData.rvel.x, ry - remoteData.rvel.y, rz - remoteData.rvel.z)
  remoteData.racc:setScaled(1 / dt)
  local qx, qy, qz = clamp3(remoteData.racc.x, remoteData.racc.y, remoteData.racc.z, maxRacc)
  remoteData.racc:set(qx, qy, qz)

  remoteData.pos:set(p[1], p[2], p[3])
  remoteData.vel:set(vx, vy, vz)
  remoteData.rvel:set(rx, ry, rz)
  applyTargetRot(rt)
  remoteData.tim = tim
  remoteData.recvAt = timer
  active = true
  staleWarned = false
end


local function pullMailbox()
  local sid = v.srServerID
  if not sid or sid == '' then return end
  local box = 'srPos' .. sid
  local ok, ver = pcall(function() return obj:getLastMailboxVersion(box) end)
  if not ok or ver == nil or ver == lastMailboxVersion then return end
  lastMailboxVersion = ver
  local ok2, data = pcall(function() return obj:getLastMailbox(box) end)
  if ok2 and data then setTargetPos(data) end
end


local function onPhysicsStep(dtSim)
  ensureInit()
  if v.mpVehicleType ~= 'R' then
    
    pcall(function()
      local a, b, c = obj:getRollPitchYawAngularVelocity()
      local vx, vy, vz = obj:getVelocityXYZ()
      if finite3(vx, vy, vz) then vehVel:set(vx, vy, vz) end
      if finite3(a, b, c) then vehRvel:set(b, a, c) end
    end)
    return
  end
  pcall(function()
    local vx, vy, vz = obj:getVelocityXYZ()
    if not finite3(vx, vy, vz) then return end
    vehVel:set(vx, vy, vz)
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    if finite3(a, b, c) then vehRvel:set(b, a, c) end
  end)
end




local function applyClusterDelta(dvx, dvy, dvz, dwx, dwy, dwz)
  local rn = getRefNode()
  if not rn then return false end
  
  if not (finite3(dvx, dvy, dvz) and finite3(dwx, dwy, dwz)) then return false end
  return pcall(function()
    forceVec:set(dvx * physicsFPS, dvy * physicsFPS, dvz * physicsFPS)
    
    tmpVec:set(-dwx * physicsFPS, -dwy * physicsFPS, -dwz * physicsFPS)
    obj:applyClusterLinearAngularAccel(rn, forceVec, tmpVec)
  end)
end





local function requestTeleport(px, py, pz, qx, qy, qz, qw, vx, vy, vz, rvx, rvy, rvz, noCounter)
  if not posOk(px, py, pz) or not quatOk(qx, qy, qz, qw) then
    nanDropCount = nanDropCount + 1
    return false
  end
  if not (finite3(vx, vy, vz) and finite3(rvx or 0, rvy or 0, rvz or 0)) then
    nanDropCount = nanDropCount + 1
    return false
  end
  local ok = pcall(function()
    local payload = jsonEncode({
      pos = { px, py, pz },
      rot = { qx, qy, qz, qw },
      vel = { vx, vy, vz },
      rvel = { rvx or 0, rvy or 0, rvz or 0 },
      noCounter = noCounter and 1 or 0,
    })
    obj:queueGameEngineLua('startride.applyRemoteTransform(' .. obj:getID() .. ', ' ..
      string.format('%q', payload) .. ')')
  end)
  return ok
end


local function updateGFX(dt)
  ensureInit()
  if v.mpVehicleType ~= 'R' then return end

  timer = timer + dt
  lastDT = dt
  framesSinceReset = framesSinceReset + 1

  pullMailbox()

  if not active then return end
  
  if (timer - remoteData.recvAt) > packetTimeout then
    active = false
    if not staleWarned then
      staleWarned = true
      logI('同步中断（对方数据超时），车辆保持原位')
    end
    return
  end

  local rn = getRefNode()
  if not rn then return end

  local ok = pcall(function()
    local px, py, pz = obj:getPositionXYZ()
    vehPos:set(px, py, pz)
  end)
  if not ok then return end

  local vr = getVehRot()
  pcall(function()
    local vx, vy, vz = obj:getVelocityXYZ()
    vehVel:set(vx, vy, vz)
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    vehRvel:set(b, a, c)
  end)

  
  if not lastVehVel then lastVehVel = vehVel:copy() end
  vehAcc:setSub(vehVel, lastVehVel)
  lastVehVel:set(vehVel)

  if not lastVehRvel then lastVehRvel = vehRvel:copy() end
  vehRacc:setSub(vehRvel, lastVehRvel)
  lastVehRvel:set(vehRvel)

  
  local predictTime = min(max(timer - remoteData.recvAt, 0), maxPredict)
  local smootherDT = dt / max(abs(predictTime), 0.001)
  local rvx, rvy, rvz = smGet(sm.remoteVel, remoteData.vel.x, remoteData.vel.y, remoteData.vel.z, smootherDT)
  local rrx, rry, rrz = smGet(sm.remoteRvel, remoteData.rvel.x, remoteData.rvel.y, remoteData.rvel.z, smootherDT)
  local rax, ray, raz = smGet(sm.remoteAcc, remoteData.acc.x, remoteData.acc.y, remoteData.acc.z, smootherDT)
  local rqx, rqy, rqz = smGet(sm.remoteRacc, remoteData.racc.x, remoteData.racc.y, remoteData.racc.z, smootherDT)

  local ptAcc = 0.5 * predictTime * predictTime
  local tpx = remoteData.pos.x + rvx * predictTime + rax * ptAcc
  local tpy = remoteData.pos.y + rvy * predictTime + ray * ptAcc
  local tpz = remoteData.pos.z + rvz * predictTime + raz * ptAcc
  local tvx = rvx + rax * predictTime
  local tvy = rvy + ray * predictTime
  local tvz = rvz + raz * predictTime
  if not finite3(tpx, tpy, tpz) or not finite3(tvx, tvy, tvz) then return end

  
  local ex, ey, ez = tpx - vehPos.x, tpy - vehPos.y, tpz - vehPos.z
  local posErrLenSq = ex * ex + ey * ey + ez * ez
  if posErrLenSq ~= posErrLenSq then return end

  
  local rx, ry, rz = 0, 0, 0
  local rotErrLenSq = 0
  pcall(function()
    local qErr = vr:inversed() * remoteData.rot
    local e = qErr:toEulerYXZ()
    rx, ry, rz = e.y, e.z, e.x
    rotErrLenSq = rx * rx + ry * ry + rz * rz
  end)
  if not finite3(rx, ry, rz) then rx, ry, rz = 0, 0, 0; rotErrLenSq = 0 end

  
  local maxVel = max(len3(tvx, tvy, tvz), len3(vehVel.x, vehVel.y, vehVel.z))
  local tpDist1 = tpDistAdd + maxVel * tpDistMul1
  local tpDist2 = tpDistAdd + maxVel * tpDistMul2
  local maxRvel = max(len3(rrx, rry, rrz), len3(vehRvel.x, vehRvel.y, vehRvel.z))
  local tpRot1 = tpRotAdd + maxRvel * tpRotMul1
  local tpRot2 = tpRotAdd + maxRvel * tpRotMul2

  
  if posErrLenSq > tpDist1 * tpDist1 or rotErrLenSq > tpRot1 * tpRot1 then
    tpTimer = tpTimer + dt
  else
    tpTimer = 0
  end

  
  
  
  
  
  
  if framesSinceReset > 5 then
    local wantTp = pendingSnap
      or tpTimer > (tpDelayAdd + abs(predictTime))
      or posErrLenSq > tpDist2 * tpDist2
      or rotErrLenSq > tpRot2 * tpRot2
    if wantTp and (timer - lastTpAt) < minTpGap then wantTp = false end

    if wantTp then
      local sent = requestTeleport(tpx, tpy, tpz,
        remoteData.rot.x, remoteData.rot.y, remoteData.rot.z, remoteData.rot.w,
        tvx, tvy, tvz,
        remoteData.rvel.x, remoteData.rvel.y, remoteData.rvel.z,
        pendingSnap)
      if sent then
        pendingSnap = false
        lastTpAt = timer
        tpCount = tpCount + 1
        smSet(sm.remoteVel, remoteData.vel.x, remoteData.vel.y, remoteData.vel.z)
        smSet(sm.remoteRvel, remoteData.rvel.x, remoteData.rvel.y, remoteData.rvel.z)
        smReset(sm.remoteAcc)
        smReset(sm.remoteRacc)
        smReset(sm.accError)
        smReset(sm.raccError)
        lastAcc, lastRacc = nil, nil
        hasLastAcc = false
        tpTimer = 0
        return
      end
      
      pendingSnap = false
      tpTimer = 0
      return
    end
  end

  
  local velErrX, velErrY, velErrZ = tvx - vehVel.x, tvy - vehVel.y, tvz - vehVel.z
  
  local aex, aey, aez = smGet(sm.accError,
    (hasLastAcc and lastAccV.x or vehAcc.x) - vehAcc.x,
    (hasLastAcc and lastAccV.y or vehAcc.y) - vehAcc.y,
    (hasLastAcc and lastAccV.z or vehAcc.z) - vehAcc.z, dt)
  local rex, rey, rez = smGet(sm.raccError,
    (hasLastAcc and lastRaccV.x or vehRacc.x) - vehRacc.x,
    (hasLastAcc and lastRaccV.y or vehRacc.y) - vehRacc.y,
    (hasLastAcc and lastRaccV.z or vehRacc.z) - vehRacc.z, dt)

  local f = min(posForceMul * dt, 1)
  local fr = min(rotForceMul * dt, 1)

  local tAccX, tAccY, tAccZ = (velErrX + ex * posCorrectMul) * f,
                              (velErrY + ey * posCorrectMul) * f,
                              (velErrZ + ez * posCorrectMul) * f
  tAccX, tAccY, tAccZ = clamp3(tAccX, tAccY, tAccZ, maxPosForce * dt)

  local tRaccX, tRaccY, tRaccZ = (rrx - vehRvel.x + rx * rotCorrectMul) * fr,
                                 (rry - vehRvel.y + ry * rotCorrectMul) * fr,
                                 (rrz - vehRvel.z + rz * rotCorrectMul) * fr
  tRaccX, tRaccY, tRaccZ = clamp3(tRaccX, tRaccY, tRaccZ, maxRotForce * dt)

  
  local accLenSq = tAccX * tAccX + tAccY * tAccY + tAccZ * tAccZ
  local dot = tAccX * aex + tAccY * aey + tAccZ * aez
  local mul = 1 - min(max(dot / (accLenSq + maxAccError * maxAccError * dt), 0), 1)
  tAccX, tAccY, tAccZ = tAccX * mul, tAccY * mul, tAccZ * mul

  local raccLenSq = tRaccX * tRaccX + tRaccY * tRaccY + tRaccZ * tRaccZ
  local rdot = tRaccX * rex + tRaccY * rey + tRaccZ * rez
  local rmul = 1 - min(max(rdot / (raccLenSq + maxRaccError * maxRaccError * dt), 0), 1)
  tRaccX, tRaccY, tRaccZ = tRaccX * rmul, tRaccY * rmul, tRaccZ * rmul

  if framesSinceReset <= 5 then
    lastAccV:set(tAccX, tAccY, tAccZ)
    lastRaccV:set(tRaccX, tRaccY, tRaccZ)
    hasLastAcc = true
    return
  end

  local rotLenSq = tRaccX * tRaccX + tRaccY * tRaccY + tRaccZ * tRaccZ
  local accLenSq2 = tAccX * tAccX + tAccY * tAccY + tAccZ * tAccZ
  
  
  if not (finite3(tAccX, tAccY, tAccZ) and finite3(tRaccX, tRaccY, tRaccZ)) then
    nanDropCount = nanDropCount + 1
    active = false
    return
  end
  if rotLenSq > minRotForce * minRotForce or len3(vehVel.x, vehVel.y, vehVel.z) > 1 then
    applyClusterDelta(tAccX, tAccY, tAccZ, tRaccX, tRaccY, tRaccZ)
  elseif accLenSq2 > minPosForce * minPosForce then
    applyClusterDelta(tAccX, tAccY, tAccZ, 0, 0, 0)
  end

  lastAcc = vec3(tAccX, tAccY, tAccZ)
  lastRacc = vec3(tRaccX, tRaccY, tRaccZ)
end


local function getVehicleRotation()
  ensureInit()
  local out = {}
  local ok = pcall(function()
    local px, py, pz = obj:getPositionXYZ()
    local vx, vy, vz = obj:getVelocityXYZ()
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    local q = getVehRot()
    out.pos = { px, py, pz }
    out.vel = { vx, vy, vz }
    out.rvel = { b, a, c }
    out.rot = { q.x, q.y, q.z, q.w }
    out.tim = timer
  end)
  if not ok then return nil end
  return out
end


local function setAngularVelocity(x, y, z, pitchAV, rollAV, yawAV)
  ensureInit()
  local rn = getRefNode()
  if not rn then return end
  pcall(function()
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    local curX, curY, curZ = b, a, c
    local dx, dy, dz = (pitchAV - curX), (rollAV - curY), (yawAV - curZ)
    tmpVec:set(-dx * physicsFPS, -dy * physicsFPS, -dz * physicsFPS)
    obj:applyClusterLinearAngularAccel(rn, zeroVec, tmpVec)
  end)
end

local function onReset()
  ensureInit()
  active = false
  framesSinceReset = 0
  tpTimer = 0
  remoteData.tim = -1
  pendingSnap = true
  smReset(sm.remoteVel); smReset(sm.remoteRvel)
  smReset(sm.remoteAcc); smReset(sm.remoteRacc)
  smReset(sm.accError); smReset(sm.raccError)
  lastAcc, lastRacc = nil, nil
  hasLastAcc = false
  lastVehVel, lastVehRvel = nil, nil
end

local function onInit()
  ensureInit()
  refNode = nil
  refNodeResolved = false
  active = false
  lastMailboxVersion = -1
  remoteData.tim = -1
  timer = 0
  framesSinceReset = 0
  tpTimer = 0
  staleWarned = false
  pendingSnap = true
  lastTpAt = -100
  badDataWarned = false
  lastAcc, lastRacc = nil, nil
  hasLastAcc = false
  lastVehVel, lastVehRvel = nil, nil
  pcall(function() physicsFPS = obj:getPhysicsFPS() or 2000 end)
  if not isFinite(physicsFPS) or physicsFPS <= 0 then physicsFPS = 2000 end
  pcall(function() enablePhysicsStepHook() end)
  
  
  pcall(function()
    obj:queueGameEngineLua('startride.onVEReady(' .. tostring(obj:getID()) .. ')')
  end)
  if not readyLogged then
    readyLogged = true
    logI('v2.8.1 已加载, physicsFPS=' .. tostring(physicsFPS),
      'refNode=' .. tostring(getRefNode()), 'type=' .. tostring(v.mpVehicleType))
  end
end


local function debugState()
  return {
    loaded = true,
    type = v.mpVehicleType,
    serverID = v.srServerID,
    active = active,
    hasData = remoteData.tim >= 0,
    tim = remoteData.tim,
    framesSinceReset = framesSinceReset,
    refNode = getRefNode(),
    physicsFPS = physicsFPS,
    mailboxVersion = lastMailboxVersion,
    teleports = tpCount,          
    nanDrops = nanDropCount,      
  }
end

M.onInit = onInit
M.onExtensionLoaded = onInit
M.onReset = onReset
M.updateGFX = updateGFX
M.onPhysicsStep = onPhysicsStep

M.setVehicleType = setVehicleType
M.setServerID = setServerID
M.setTargetPos = setTargetPos
M.setVehiclePosRot = setTargetPos
M.setAngularVelocity = setAngularVelocity
M.getVehicleRotation = getVehicleRotation
M.debugState = debugState
M.isRemoteActive = function() return active end

v.srVEModule = M
return M
