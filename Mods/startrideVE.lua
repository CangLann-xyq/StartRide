
if v.srVEModule then return v.srVEModule end

-- ═══════════════════════════════════════════════════════════════════════════
-- StartRide 远程车辆物理接管（车辆层 VE）
--
-- 只做一件事：把对方发来的位置/速度**用物理力**把本机这辆车拽过去，
-- 同时保留碰撞。不能直接 setPos，那样远程车就变成幽灵（穿模、撞不到、不参与解算）。
--
-- 参考实现：BeamMP 的 positionVE.lua + velocityVE.lua（本地副本 D:\code\all\BeamMP-ref\）。
--   * 位置修正/预测/误差/传送阈值 —— 逐条对齐 positionVE.updateGFX
--   * 施力 —— 对齐 velocityVE.addForce / addAngularForce（逐节点 applyForceVector）
--   * 角速度只补角速度、不动线速度 —— 对齐 positionGE 里传的 onlyAngularVelocity=1
--
-- ⚠️ 施力为什么走「逐节点 applyForceVector」而不是 applyClusterLinearAngularAccel：
--   后者在游戏里是 thruster 用的接口（见 lua/vehicle/thrusters.lua 的 applyAccel/
--   applyVelocity 与 update 里的 clusterThrust 段），语义是"给这个 cluster 一个加速度"，
--   由引擎直接在速度空间上叠加；而 applyForceVector 进的是**力累加器**，和接触/碰撞
--   冲量在同一个约束求解器里解 —— 两台车才能互相顶、互相推。
--   BeamMP 的 velocityVE.addForce 就是逐节点施力（它的 cluster 分支只是省开销的
--   "快路径 + 给断开节点反向力"）。这里统一走逐节点，省掉那套 counter-velocity 记账；
--   节点数过多（改装车/超长挂车）时再退回单次 cluster 调用，避免每帧上千次调用。
--
-- ⚠️ Lua 的 `local function` 只在其定义之后可见，前面的函数体里同名引用会解析成
--   **全局变量**（运行期 nil，被 pcall 包住就静默失效）。所以本文件按
--   「工具 → 状态 → 取车体朝向/初始化 → 节点集合 → 施力 → 接收 → 每帧」严格排序，
--   改文件时别打乱。
-- ═══════════════════════════════════════════════════════════════════════════

local M = {}

-- 默认按"本车"起步；GE 下发的 setVehicleType('R') 会改成 'R'。
-- ⚠️ 那条命令是在 spawn 之后立刻排队的，那时本模块可能还没加载完 → 会丢。
--    所以 setTargetPos 里还有一层自愈（收到远程数据就强制 'R'，BeamMP 同样做法）。
v.mpVehicleType = 'L'
v.srServerID = ''

local abs = math.abs
local min = math.min
local max = math.max

-- ── 位置/角度修正参数（数值与 BeamMP positionVE 完全一致；tpRotAdd 0.5→0.8 是主动放宽）──
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
-- BeamMP 用 0.5。阈值过小会让高速行驶的远程车频繁被硬传送，
-- 每次硬传送都会打断一次物理解算 —— 看起来就是"抖 + 穿模"。这里放宽到 0.8。
local tpRotAdd = 0.8
local tpRotMul1 = 0.2
local tpRotMul2 = 0.5

local maxPredict = 0.3
local packetTimeout = 0.30

local remoteVelSmoothRate = 2
local remoteAccSmoothRate = 1
local errSmoothRate = 50

-- ── 逐节点施力 ──
local maxBeamLengthRatio = 2   -- 梁被拉长超过原长这个倍数就当成断了（BeamMP velocityVE）
local PER_NODE_LIMIT = 900     -- 可连接节点数超过它就退回单次 cluster 调用（保帧率）
local NODE_REFRESH_INTERVAL = 5
local connectedBeams = {}
local isConnectedNode = {}
local nodes = {}
local parentNode = nil
local beamsChanged = true
local nodesBuilt = false
local nodeSetFallbacks = 0
local lastNodeRefresh = -100
local beamBrokeWrapped = false

-- ── 运行时状态 ──
local timer = 0
local lastDT = 0
local framesSinceReset = 0
local tpTimer = 0
local active = false
local pendingSnap = false
local lastTpAt = -100
local minTpGap = 0.25
local tpCount = 0
local skipByCollision = 0
local nanDropCount = 0
local badDataWarned = false
local lastMailboxVersion = -1
local physicsFPS = 2000
local refNode = nil
local refNodeResolved = false
local staleWarned = false
local readyLogged = false
local firstPacketLogged = false
local promoteLogged = false
local idRequested = false

local remoteData = {
  pos = nil, vel = nil, acc = nil, rot = nil, rvel = nil, racc = nil,
  tim = -1, recvAt = 0,
}

local lastVehVel = nil
local lastVehRvel = nil
local lastAcc = nil
local lastRacc = nil
local lastAccV, lastRaccV, hasLastAcc = nil, nil, false

-- 这些向量在 ensureInit 里一次性建好，之后全程复用（每帧 vec3() 会产生垃圾）
local dir, dirUp, rotQ, pos, vel, rvel
local vehRot, vehPos, vehVel, vehRvel, vehAcc, vehRacc
local cogTmp, forceVec, zeroVec, tmpVec, cogRel
local clusterLin, clusterAng
local sm = {}


-- ═══════════════ 工具 ═══════════════

local function logI(...)
  local parts = {}
  for _, x in ipairs({ ... }) do parts[#parts + 1] = tostring(x) end
  pcall(function() log('I', 'startride', '[StartRide VE] ' .. table.concat(parts, ' ')) end)
end

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

-- 旋转一个向量（不用 setRotated，避免不同版本 API 差异）
local function rotatedInto(dst, src, q)
  local r = src:rotated(q)
  dst:set(r.x, r.y, r.z)
end


-- ═══════════════ 初始化 / 车体朝向 ═══════════════

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
  clusterLin = vec3()
  clusterAng = vec3()
  cogRel = vec3(0, 0, 0)
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
  if refNodeResolved and refNode then return refNode end
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
  firstPacketLogged = false
  idRequested = false
end

-- 收到远程数据 = 这辆车绝不可能是"我自己的车"。BeamMP 的 positionVE 也是这么自愈的。
-- 少这一层，一旦 GE 的 setVehicleType('R') 没生效（spawn 竞态），远程车就永远停在
-- 生成点 —— 用户看到的就是"看不见对方的车 + 撞不到"。
local function ensureRemoteType(sid)
  if v.mpVehicleType ~= 'R' then
    v.mpVehicleType = 'R'
    if not promoteLogged then
      promoteLogged = true
      logI('收到远程车辆数据 → 本车按远程车接管 (id=' .. tostring(sid) .. ')')
    end
  end
end


-- ═══════════════ 节点集合（velocityVE 移植）═══════════════

-- 质心（车身坐标）。逐节点给同样 Δv 时，只有绕质心才不产生额外自旋。
local function calcCOG()
  local rot = getVehRot()
  local totalMass, cx, cy, cz = 0, 0, 0, 0
  for nid in pairs(isConnectedNode) do
    local nm = 0
    pcall(function() nm = obj:getNodeMass(nid) or 0 end)
    local np = nil
    pcall(function() np = obj:getNodePosition(nid) end)
    if np and nm > 0 then
      cx, cy, cz = cx + np.x * nm, cy + np.y * nm, cz + np.z * nm
      totalMass = totalMass + nm
    end
  end
  if totalMass <= 0 then
    cogRel:set(0, 0, 0)
    return
  end
  local mean = vec3(cx / totalMass, cy / totalMass, cz / totalMass)
  rotatedInto(cogRel, mean, rot:inversed())
end

local function findConnectedNodesRecursive(parentID, depth)
  if isConnectedNode[parentID] then return end
  if depth > 4000 then return end
  isConnectedNode[parentID] = true
  local nm = 0
  pcall(function() nm = obj:getNodeMass(parentID) or 0 end)
  nodes[#nodes + 1] = { parentID, nm * physicsFPS }

  local beams = connectedBeams[parentID] or {}
  for i = 1, #beams do
    local bid = beams[i]
    local broken, ratio = false, 0
    pcall(function() broken = obj:beamIsBroken(bid) end)
    pcall(function() ratio = obj:getBeamCurLengthRefRatio(bid) or 0 end)
    if not broken and ratio < maxBeamLengthRatio then
      local b = v.data.beams[bid]
      if b then
        if parentID == b.id1 then
          findConnectedNodesRecursive(b.id2, depth + 1)
        elseif parentID == b.id2 then
          findConnectedNodesRecursive(b.id1, depth + 1)
        end
      end
    end
  end
end

local function findConnectedNodes()
  isConnectedNode = {}
  nodes = {}
  if parentNode then
    findConnectedNodesRecursive(parentNode, 0)
  end
  if #nodes == 0 then
    -- 兜底：与参考节点不连通（断裂/改装）→ 用全部节点，总比一动不动强
    nodeSetFallbacks = nodeSetFallbacks + 1
    for _, n in pairs(v.data.nodes or {}) do
      if n.cid then
        isConnectedNode[n.cid] = true
        local nm = 0
        pcall(function() nm = obj:getNodeMass(n.cid) or 0 end)
        nodes[#nodes + 1] = { n.cid, nm * physicsFPS }
      end
    end
  end
  calcCOG()
  beamsChanged = false
  nodesBuilt = #nodes > 0
end

local function buildNodeSet()
  connectedBeams = {}
  for _, b in pairs(v.data.beams or {}) do
    -- 排除 BEAM_PRESSURED(3) / BEAM_LBEAM(4) / BEAM_SUPPORT(7)
    if b.beamType ~= 3 and b.beamType ~= 4 and b.beamType ~= 7 then
      if connectedBeams[b.id1] == nil then connectedBeams[b.id1] = {} end
      if connectedBeams[b.id2] == nil then connectedBeams[b.id2] = {} end
      table.insert(connectedBeams[b.id1], b.cid)
      table.insert(connectedBeams[b.id2], b.cid)
    end
  end

  local refNodes = v.data.refNodes and v.data.refNodes[0]
  parentNode = nil
  if refNodes then
    if connectedBeams[refNodes.ref] then parentNode = refNodes.ref
    elseif connectedBeams[refNodes.back] then parentNode = refNodes.back
    elseif connectedBeams[refNodes.left] then parentNode = refNodes.left
    elseif connectedBeams[refNodes.up] then parentNode = refNodes.up end
  end

  findConnectedNodes()

  -- 梁断了要重建节点集合。能挂上 beamBroke 就挂（BeamMP 的做法）；
  -- 挂不上也不致命：onReset + 每 NODE_REFRESH_INTERVAL 秒兜底刷新。
  if not beamBrokeWrapped then
    pcall(function()
      local orig = powertrain.beamBroke
      if type(orig) ~= 'function' then return end
      powertrain.beamBroke = function(id, ...)
        local b = v.data.beams and v.data.beams[id]
        if b and b.beamType ~= 3 and b.beamType ~= 4 and b.beamType ~= 7 then
          beamsChanged = true
        end
        return orig(id, ...)
      end
      beamBrokeWrapped = true
    end)
  end

  logI('节点集合就绪: 节点=' .. tostring(#nodes) .. ' parentNode=' .. tostring(parentNode) ..
    ' 兜底=' .. tostring(nodeSetFallbacks) .. ' physicsFPS=' .. tostring(physicsFPS))
end


-- ═══════════════ 施力 ═══════════════

local function applyNodeForce(cid, x, y, z)
  forceVec:set(x, y, z)
  return pcall(function() obj:applyForceVector(cid, forceVec) end)
end

-- 单次 cluster 调用（节点太多时的省开销路径）。
-- 角速度那一路 BeamNG 的符号与 roll/pitch/yaw 相反 → 取负（BeamMP velocityVE 同）。
local function clusterAccel(x, y, z, pitchAV, rollAV, yawAV)
  local rn = getRefNode()
  if not rn then return false end
  clusterLin:set(x * physicsFPS, y * physicsFPS, z * physicsFPS)
  clusterAng:set(-(pitchAV or 0) * physicsFPS, -(rollAV or 0) * physicsFPS, -(yawAV or 0) * physicsFPS)
  return pcall(function() obj:applyClusterLinearAngularAccel(rn, clusterLin, clusterAng) end)
end

-- 加一个速度增量 Δv(m/s)
local function addVelocity(x, y, z)
  if beamsChanged or not nodesBuilt then findConnectedNodes() end
  if #nodes > PER_NODE_LIMIT then
    clusterAccel(x, y, z, 0, 0, 0)
    return
  end
  for i = 1, #nodes do
    local node = nodes[i]
    applyNodeForce(node[1], x * node[2], y * node[2], z * node[2])
  end
end

-- 加角速度增量 Δω(rad/s)，顺序 pitch/roll/yaw。
-- 按"绕质心的切向速度"给每个节点施力 → 纯转动，不会把车撕开（BeamMP addAngularForce 同式）
local function addAngularVelocity(x, y, z, pitchAV, rollAV, yawAV)
  if beamsChanged or not nodesBuilt then findConnectedNodes() end
  pitchAV, rollAV, yawAV = pitchAV or 0, rollAV or 0, yawAV or 0
  if #nodes > PER_NODE_LIMIT then
    clusterAccel(x, y, z, pitchAV, rollAV, yawAV)
    return
  end

  local rot = getVehRot()
  rotatedInto(cogTmp, cogRel, rot)
  local cx, cy, cz = cogTmp.x, cogTmp.y, cogTmp.z

  for i = 1, #nodes do
    local node = nodes[i]
    local cid, mul = node[1], node[2]
    local np = nil
    pcall(function() np = obj:getNodePosition(cid) end)
    if np then
      local px, py, pz = np.x - cx, np.y - cy, np.z - cz
      applyNodeForce(cid,
        (x + py * yawAV - pz * rollAV) * mul,
        (y + pz * pitchAV - px * yawAV) * mul,
        (z + px * rollAV - py * pitchAV) * mul)
    end
  end
end

-- 只设置角速度，**不动线速度**：对齐 BeamMP positionGE 传的 onlyAngularVelocity=1。
-- GE 那边已经用 applyClusterVelocityScaleAdd 把线速度设好了，这里再动线速度会两边打架
-- （会变成"每次传送都把刚设好的速度抵消掉"→ 远程车像被粘住）。
local function setAngularVelocityOnly(pitchAV, rollAV, yawAV)
  ensureInit()
  local rot = getVehRot()
  rotatedInto(cogTmp, cogRel, rot)

  local rvNow = vec3(0, 0, 0)
  pcall(function()
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    rvNow:set(b, a, c)
    local r = rvNow:rotated(rot)
    rvNow:set(r.x, r.y, r.z)
  end)
  local rdx = (pitchAV or 0) - rvNow.x
  local rdy = (rollAV or 0) - rvNow.y
  local rdz = (yawAV or 0) - rvNow.z
  if not finite3(rdx, rdy, rdz) then return end
  addAngularVelocity(0, 0, 0, rdx, rdy, rdz)
end


-- ═══════════════ 接收远程数据 ═══════════════

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

  ensureRemoteType(v.srServerID)
  if not firstPacketLogged then
    firstPacketLogged = true
    logI('收到首个远程位置包 id=' .. tostring(v.srServerID) ..
      ' pos=(' .. string.format('%.1f,%.1f,%.1f', p[1], p[2], p[3]) .. ')')
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
    pendingSnap = true      -- 新数据流（刚进场/重连）→ 第 6 帧强制硬对齐一次
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

-- GE → VE 的高速通道：邮箱（每辆车按自己的 srPos<id> 取件）
local function pullMailbox()
  local sid = v.srServerID
  if not sid or sid == '' then
    -- ⚠️ GE 的 setServerID 是在 spawn 后立刻排队的，那时本模块可能还没加载完 → 命令丢失。
    -- 主动向 GE 要一次（GE 侧 startride.onVEAskID 会按 gameVehicleID 找到记录并重发）。
    if not idRequested then
      idRequested = true
      pcall(function()
        obj:queueGameEngineLua('startride.onVEAskID(' .. tostring(obj:getID()) .. ')')
      end)
    end
    return
  end

  local box = 'srPos' .. sid
  local ok, ver = pcall(function() return obj:getLastMailboxVersion(box) end)
  if not ok or ver == nil or ver == lastMailboxVersion then return end
  lastMailboxVersion = ver
  local ok2, data = pcall(function() return obj:getLastMailbox(box) end)
  if ok2 and data then setTargetPos(data) end
end


-- ═══════════════ 每帧：算目标 → 施力 ═══════════════

local function onPhysicsStep(dtSim)
  ensureInit()
  -- 只有远程车才需要维护这两个量（本地车交给游戏自己算）
  if v.mpVehicleType ~= 'R' then return end
  pcall(function()
    local vx, vy, vz = obj:getVelocityXYZ()
    if not finite3(vx, vy, vz) then return end
    vehVel:set(vx, vy, vz)
    local a, b, c = obj:getRollPitchYawAngularVelocity()
    if finite3(a, b, c) then vehRvel:set(b, a, c) end
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
      -- 给 GE 做"是不是刚发生碰撞"的判断（BeamMP positionGE 的 localVel vs vehVel 比较）
      vehVel = { vehVel.x, vehVel.y, vehVel.z },
      noCounter = noCounter and 1 or 0,
    })
    obj:queueGameEngineLua('startride.applyRemoteTransform(' .. obj:getID() .. ', ' ..
      string.format('%q', payload) .. ')')
  end)
  return ok
end

local function updateGFX(dt)
  ensureInit()
  timer = timer + dt
  lastDT = dt
  framesSinceReset = framesSinceReset + 1

  -- ⚠️ pullMailbox 必须放在 mpVehicleType 判断【之前】：
  -- GE 的 setVehicleType('R') 是 spawn 后立刻排队的，那时本模块可能还没加载完 → 丢命令。
  -- 一旦丢了本车就永远是 'L'，下面的物理解算整段不跑 —— 用户看到的就是
  -- "看不见对方的车"。收到远程包即自愈（BeamMP positionVE 同样做法）。
  pullMailbox()

  if v.mpVehicleType ~= 'R' then return end

  if not nodesBuilt then
    pcall(buildNodeSet)
    if not nodesBuilt and framesSinceReset == 30 then
      logI('拿不到节点集合，退回 cluster 级施力（碰撞可能异常）')
    end
  elseif beamsChanged or (timer - lastNodeRefresh) > NODE_REFRESH_INTERVAL then
    lastNodeRefresh = timer
    pcall(findConnectedNodes)
  end

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

  -- rotError = vehRot^-1 * rot，再 (x,y,z) → (y,z,x)（BeamMP positionVE 的轴重排）
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

  -- BeamMP positionVE：等 6 帧再无条件硬对齐一次
  -- （smoother 需要几帧收敛；第一帧就传会让"高速状态下进场/重生"反复弹跳）
  if framesSinceReset > 5 then
    local forceSnap = pendingSnap and framesSinceReset >= 6
    local wantTp = forceSnap
      or tpTimer > (tpDelayAdd + abs(predictTime))
      or posErrLenSq > tpDist2 * tpDist2
      or rotErrLenSq > tpRot2 * tpRot2
    -- 强制那一次不看间隔限制（否则 firstTp 会被 minTpGap 挡掉）
    if wantTp and not forceSnap and (timer - lastTpAt) < minTpGap then wantTp = false end

    if wantTp then
      local sent = requestTeleport(tpx, tpy, tpz,
        remoteData.rot.x, remoteData.rot.y, remoteData.rot.z, remoteData.rot.w,
        tvx, tvy, tvz,
        remoteData.rvel.x, remoteData.rvel.y, remoteData.rvel.z,
        forceSnap)
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
      -- 发送失败：强制这一次不再重试（避免死循环），普通传送下帧再说
      if forceSnap then pendingSnap = false end
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

  if not (finite3(tAccX, tAccY, tAccZ) and finite3(tRaccX, tRaccY, tRaccZ)) then
    nanDropCount = nanDropCount + 1
    active = false
    return
  end

  local rotLenSq = tRaccX * tRaccX + tRaccY * tRaccY + tRaccZ * tRaccZ
  local accLenSq2 = tAccX * tAccX + tAccY * tAccY + tAccZ * tAccZ

  -- 逐节点施力：力小到可忽略时就干脆不给，让轮胎/碰撞自己说话
  if rotLenSq > minRotForce * minRotForce or len3(vehVel.x, vehVel.y, vehVel.z) > 1 then
    addAngularVelocity(tAccX, tAccY, tAccZ, tRaccX, tRaccY, tRaccZ)
  elseif accLenSq2 > minPosForce * minPosForce then
    addVelocity(tAccX, tAccY, tAccZ)
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
  beamsChanged = true
  pcall(findConnectedNodes)
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
  firstPacketLogged = false
  lastAcc, lastRacc = nil, nil
  hasLastAcc = false
  lastVehVel, lastVehRvel = nil, nil
  nodesBuilt = false
  beamsChanged = true
  pcall(function() physicsFPS = obj:getPhysicsFPS() or 2000 end)
  if not isFinite(physicsFPS) or physicsFPS <= 0 then physicsFPS = 2000 end
  pcall(function() enablePhysicsStepHook() end)
  pcall(buildNodeSet)

  pcall(function()
    obj:queueGameEngineLua('startride.onVEReady(' .. tostring(obj:getID()) .. ')')
  end)
  if not readyLogged then
    readyLogged = true
    logI('v2.10.1 已加载, physicsFPS=' .. tostring(physicsFPS),
      'refNode=' .. tostring(getRefNode()), 'type=' .. tostring(v.mpVehicleType),
      '施力=' .. (#nodes > PER_NODE_LIMIT and 'cluster' or '逐节点'))
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
    nodes = #nodes,
    parentNode = parentNode,
    perNode = (#nodes > 0 and #nodes <= PER_NODE_LIMIT),
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
M.setAngularVelocityOnly = setAngularVelocityOnly
-- 兼容旧调用：GE 以前传的是 setAngularVelocity(0,0,0,p,r,y) —— 线速度那三个参数本来
-- 就是 0，这里直接按"只设角速度"处理（详见 setAngularVelocityOnly 的注释）。
M.setAngularVelocity = function(_, _, _, pitchAV, rollAV, yawAV)
  setAngularVelocityOnly(pitchAV, rollAV, yawAV)
end
M.getVehicleRotation = getVehicleRotation
M.debugState = debugState
M.isRemoteActive = function() return active end

v.srVEModule = M
return M
