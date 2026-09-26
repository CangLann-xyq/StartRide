








local M = {}

local im = ui_imgui
local ffi = ffi or require('ffi')
local socket = require('socket')


local LAUNCHER_IP = '127.0.0.1'
local LAUNCHER_PORT = 4444
local SEND_RATE = 1 / 30        
local PING_INTERVAL = 5         
local VEHICLE_TIMEOUT = 8       
local MAX_REMOTE = 16
local CHAT_KEEP = 80
local CHAT_COOLDOWN = 0.3
-- ⚠️ 这个串必须等于启动器版本号（与 Mods/startrideVE.lua 的日志串一致），
-- 它是「游戏里跑的到底是哪一版模组」的唯一指纹：游戏日志里搜
--   `[StartRide]  GE 扩展 v` 和 `[StartRide VE] v`
-- 两条都要出现且版本一致，才能确定包是启动器刚装的最新版。
-- 升版本号时由 _sr_shots/_bump_version.py 一起改（已登记）。
local MOD_VERSION = '2.12.0'






local MIN_SPAWN_GAP = 0.5       
local MAX_SPAWN_BACKOFF = 8     

local MIN_CFG_RESPAWN_GAP = 3   

local MAX_CONNECT_BACKOFF = 8


local tcpSock = nil
local connected = false
local connecting = false
local connectStartTime = 0
local connectRetryCount = 0
local connectBackoff = 1        
local nextConnectTry = 0
local recvBuffer = ''
local outBuffer = ''

local timeAccum = 0
local lastSend = 0
local lastPingSend = 0
local frameCount = 0

local remoteVehicles = {}       
local mpPlayers = {}            
local roomInfo = { name = '', count = 0, capacity = 0, host = '', closed = false }
local chat = {}
local playerName = 'Player'
-- 启动器下发的稳定联机 ID（SR-XXXX-XXXX-XXXX）。车辆标识用它，不用昵称：
-- 昵称可以两个人一样，ID 不会。取不到时 myId() 自动回退成昵称（兼容旧启动器）。
local playerId = nil
local function myId()
  if playerId and playerId ~= '' then return playerId end
  return playerName
end
local initialized = false






local relayState = 'unknown'    
local relayDetail = ''
local relayRoomId = ''
local statGameIn, statRelayOut, statRelayIn, statRelayVehicle = 0, 0, 0, 0
local remotePacketCount = 0     
local spawnFailCount = 0        
local spawnOkCount = 0          
local lastSpawnError = ''
local lastSpawnTryAt = 0        
local badPacketCount = 0        
local skipByCollision = 0       -- applyRemoteTransform 因"疑似碰撞"跳过的次数
local pendingCleanup = {}       
local lastStallLog = -100       


local localMap = ''
local peerMap = ''
local mapMismatchWarned = false

local veReadyCount = 0


local hudOn = im.BoolPtr(true)
local chatOpen = im.BoolPtr(true)
local panelOpen = im.BoolPtr(false)
local playerListOpen = im.BoolPtr(false)
local chatInput = im.ArrayChar(256)
local chatFocused = false
local chatWantFocus = false
local chatScrollToBottom = false
local chatOpacity = im.FloatPtr(0.92)
local lastChatSend = 0
local keyHeld = { t = false }


local lastRot = nil
local lastRotTime = 0
local lastJbeam = nil
local lastCfgJson = nil



local lastCfgCheck = -999


local C = {
  accent = im.ImVec4(0.35, 0.72, 1.00, 1),
  ok     = im.ImVec4(0.30, 0.87, 0.50, 1),
  warn   = im.ImVec4(0.98, 0.75, 0.30, 1),
  danger = im.ImVec4(0.95, 0.35, 0.40, 1),
  text   = im.ImVec4(0.92, 0.94, 0.97, 1),
  dim    = im.ImVec4(0.60, 0.65, 0.74, 1),
  sys    = im.ImVec4(0.55, 0.80, 1.00, 1),
}

local function logMsg(...)
  local parts = { '[StartRide] ' }
  for _, v in ipairs({ ... }) do parts[#parts + 1] = tostring(v) end
  pcall(function() log('I', 'startride', table.concat(parts, ' ')) end)
end








local function pickFn(tbl, ...)
  for _, n in ipairs({ ... }) do
    local f = tbl[n]
    if type(f) == 'function' then return f, n end
  end
  return nil, nil
end

local fnBeginChild      = pickFn(im, 'BeginChild1', 'BeginChild', 'BeginChild2')
local fnEndChild        = pickFn(im, 'EndChild')
local fnAddRectFilled   = pickFn(im, 'ImDrawList_AddRectFilled', 'ImDrawList_AddRectFilled1', 'ImDrawList_AddRectFilled2')
local fnAddCircleFilled = pickFn(im, 'ImDrawList_AddCircleFilled', 'ImDrawList_AddCircleFilled1')
local fnGetColorU32     = pickFn(im, 'GetColorU322', 'GetColorU32', 'GetColorU321', 'GetColorU323')
local fnGetDrawList     = pickFn(im, 'GetWindowDrawList')
local fnBeginTabBar     = pickFn(im, 'BeginTabBar', 'BeginTabBar1')
local fnBeginTabItem    = pickFn(im, 'BeginTabItem', 'BeginTabItem1')
local fnEndTabBar       = pickFn(im, 'EndTabBar')
local fnEndTabItem      = pickFn(im, 'EndTabItem')


local canDraw = (fnAddRectFilled and fnGetColorU32 and fnGetDrawList) and true or false
local canTab  = (fnBeginTabBar and fnBeginTabItem and fnEndTabBar and fnEndTabItem) and true or false

local function colorU32(col)
  if not fnGetColorU32 then return nil end
  local ok, v = pcall(fnGetColorU32, col)
  if ok then return v end
  return nil
end


local panelTab = 1
local PANEL_TABS = { '状态', '玩家', '聊天', '设置' }


local function beginChild(id, size, border)
  if not fnBeginChild then return false end
  local ok, r = pcall(fnBeginChild, id, size, border)
  return ok and r and true or false
end
local function endChild()
  if fnEndChild then pcall(fnEndChild) end
end

local function safeCall(name, fn, ...)
  local ok, err = pcall(fn, ...)
  if not ok then logMsg('ERR', name, tostring(err)) end
  return ok
end




local function dropConnection(reason)
  if tcpSock then pcall(function() tcpSock:close() end) end
  tcpSock = nil
  connected = false
  connecting = false
  recvBuffer = ''
  outBuffer = ''
  if reason then logMsg('连接断开:', reason) end
end



local function queuePacket(tbl)
  local js
  if type(tbl) == 'string' then
    js = tbl
  else
    local ok, enc = pcall(jsonEncode, tbl)
    if not ok or not enc then return end
    js = enc
  end
  if #js == 0 or #js > 65536 then return end
  outBuffer = outBuffer .. ffi.string(ffi.new('uint32_t[1]', #js), 4) .. js
  if #outBuffer > 524288 then outBuffer = '' end
end

local function flushOut()
  if not tcpSock or not connected then return end
  if #outBuffer == 0 then return end
  local ok, sent, err = pcall(function() return tcpSock:send(outBuffer) end)
  if not ok then
    dropConnection('send exception')
    return
  end
  if sent then
    outBuffer = outBuffer:sub(sent + 1)
  elseif err == 'timeout' or err == 'wantwrite' then
    
    return
  else
    dropConnection('send failed: ' .. tostring(err))
  end
end

local function connectTCP()
  if connected or connecting then return end
  connecting = true
  connectStartTime = timeAccum
  local ok, sock = pcall(function() return socket.tcp() end)
  if not ok or not sock then
    connecting = false
    return
  end
  tcpSock = sock
  pcall(function() tcpSock:settimeout(0) end)
  pcall(function() tcpSock:setoption('tcp-nodelay', true) end)
  local pok, result, err = pcall(function() return tcpSock:connect(LAUNCHER_IP, LAUNCHER_PORT) end)
  if not pok then
    dropConnection('connect exception')
    connectBackoff = math.min(connectBackoff * 2, MAX_CONNECT_BACKOFF)
  elseif result == 1 then
    connected = true
    connecting = false
    connectRetryCount = 0
    connectBackoff = 1
    queuePacket({ type = 'ready', name = playerName, version = MOD_VERSION })
    logMsg('已连接启动器')
  elseif err == 'timeout' or err == 'Operation already in progress' then
    
  else
    dropConnection('connect failed: ' .. tostring(err))
    connectBackoff = math.min(connectBackoff * 2, MAX_CONNECT_BACKOFF)
  end
end

local function checkConnection()
  if not connecting or not tcpSock then return end
  local ok, r, w = pcall(function() return socket.select(nil, { tcpSock }, 0) end)
  if ok and w and #w > 0 then
    local pok, peer = pcall(function() return tcpSock:getpeername() end)
    if pok and peer then
      connected = true
      connecting = false
      connectRetryCount = 0
      connectBackoff = 1
      queuePacket({ type = 'ready', name = playerName, version = MOD_VERSION })
      logMsg('已连接启动器 (async)')
    else
      dropConnection('async connect failed')
      connectBackoff = math.min(connectBackoff * 2, MAX_CONNECT_BACKOFF)
    end
  elseif timeAccum - connectStartTime > 6 then
    connectRetryCount = connectRetryCount + 1
    dropConnection('connect timeout')
    connectBackoff = math.min(connectBackoff * 2, MAX_CONNECT_BACKOFF)
  end
end




local function getObjectByID(id)
  local ok, v = pcall(function() return be:getObjectByID(id) end)
  if ok and v then return v end
  return nil
end

local function countRemote()
  local n = 0
  for _, rec in pairs(remoteVehicles) do
    if rec.veh then n = n + 1 end
  end
  return n
end

local function despawnRemote(rec)
  if not rec then return end
  local veh = rec.veh
  rec.veh = nil
  rec.veReady = nil
  if not veh then return end
  pcall(function() veh:delete() end)
  
  
  local still = nil
  pcall(function() still = getObjectByID(veh:getID()) end)
  if still then
    pcall(function() be:removeObject(veh:getID()) end)
    pcall(function() still = getObjectByID(veh:getID()) end)
    if still then
      pendingCleanup[#pendingCleanup + 1] = veh
      logMsg('车辆删除失败，加入重试队列')
    end
  end
end


local function processPendingCleanup()
  if #pendingCleanup == 0 then return end
  local keep = {}
  for i = 1, #pendingCleanup do
    local veh = pendingCleanup[i]
    pcall(function() veh:delete() end)
    local still = nil
    pcall(function() still = getObjectByID(veh:getID()) end)
    if still then
      pcall(function() be:removeObject(veh:getID()) end)
      pcall(function() still = getObjectByID(veh:getID()) end)
    end
    if still and #keep < 20 then keep[#keep + 1] = veh end
  end
  if #keep > 0 then logMsg('仍有', #keep, '辆车辆删不掉（幽灵车）') end
  pendingCleanup = keep
end



local function isNum(x)
  return type(x) == 'number' and x == x and x ~= math.huge and x ~= -math.huge
end

local function validPos(p)
  if type(p) ~= 'table' then return false end
  if not (isNum(p[1]) and isNum(p[2]) and isNum(p[3])) then return false end
  return (p[1] * p[1] + p[2] * p[2] + p[3] * p[3]) < 1e12   
end

local function validRot(r)
  if type(r) ~= 'table' then return false end
  if not (isNum(r[1]) and isNum(r[2]) and isNum(r[3]) and isNum(r[4])) then return false end
  local n = r[1] * r[1] + r[2] * r[2] + r[3] * r[3] + r[4] * r[4]
  return n > 0.25 and n < 2.5    
end






-- 把"这辆车是远程车 / 它的联机 ID 是多少"下发到车辆层（VE）。
-- ⚠️ 这是在 spawn 之后立刻排队的，那时 VE 模块（lua/vehicle/extensions/startride）
--    可能还没加载完 —— 命令会丢，于是 VE 里 v.mpVehicleType 一直是 'L'、
--    v.srServerID 一直是 ''，物理解算整段不跑 → 对方看我们的车停在原地（"看不见对方的车"）。
--    所以这一段要发三次：① spawn 后立刻；② VE 上报就绪（onVEReady）；③ VE 主动索要
--    （onVEAskID）。BeamMP 也是靠"VE 就绪后才下发"这一步（MPVehicleGE.onVehicleReady）。
local function queueVETypeAndID(veh, id)
  if not veh then return end
  pcall(function() veh.mpVehicleType = 'R' end)
  pcall(function() veh:queueLuaCommand("startrideVE.setVehicleType('R')") end)
  pcall(function()
    veh:queueLuaCommand('startrideVE.setServerID(' .. string.format('%q', tostring(id)) .. ')')
  end)
end


local function trySpawn(model, cfg, pos, rot, id, exact)
  local opts = {
    autoEnterVehicle = false,
    vehicleName = 'sr_remote_' .. tostring(id):gsub('[^%w_]', '_'),
    cling = true,
    centeredPosition = true,
    removeWhenNoPositionFound = false,
    removeTraffic = false,
  }
  
  if exact then opts.safeSpawn = false end
  local ok, veh = pcall(function()
    return spawn.spawnVehicle(model, cfg or '', pos, rot, opts)
  end)
  if ok and veh then return veh end
  return nil
end






local function spawnRemote(rec, id, data)
  if countRemote() >= MAX_REMOTE then return false end
  local p, r = data.pos, data.rot
  
  if not validPos(p) or not validRot(r) then return false end

  local model = data.model
  if type(model) ~= 'string' or model == '' then
    local ok, d = pcall(function() return core_vehicles.defaultVehicleModel end)
    model = (ok and d) or 'pickup'
  end

  local pos = vec3(p[1], p[2], p[3])
  
  local rot = quat(0, 0, 1, 0) * quat(r[1], r[2], r[3], r[4])

  local veh = trySpawn(model, data.cfg, pos, rot, id)
  if not veh then
    
    logMsg('标准生成失败，改精确放置重试:', tostring(model))
    veh = trySpawn(model, data.cfg, pos, rot, id, true)
  end
  if not veh and model ~= 'pickup' then
    
    logMsg('缺少车型', model, '-> 回退 pickup')
    veh = trySpawn('pickup', nil, pos, rot, id, true)
  end
  if not veh then
    logMsg('生成远程车辆失败:', tostring(model))
    return nil
  end

  
  pcall(function() veh.mpVehicleType = 'R' end)
  -- protected 是"配置保护"（禁止克隆/另存），BeamMP 默认给 '0'。之前写成 '1' 是笔误。
  pcall(function() veh:setField('protected', 0, '0') end)
  
  
  
  
  
  pcall(function()
    veh:queueLuaCommand("extensions.loadModulesInDirectory('lua/vehicle/extensions/startride')")
  end)
  queueVETypeAndID(veh, id)
  pcall(function() veh:queueLuaCommand('hydros.onFFBConfigChanged(nil)') end)

  rec.veh = veh
  rec.name = data.name or rec.name or id
  rec.model = model
  rec.lastSeen = timeAccum
  rec.veReady = nil
  rec.spawnFails = 0
  spawnOkCount = spawnOkCount + 1
  lastSpawnError = ''
  table.insert(chat, { name = 'system', text = rec.name .. ' 的车辆已入场' })
  logMsg('已生成远程车辆', model, 'for', rec.name)
  return true
end


local function onRemoteVehiclePacket(data)
  local id = data.id
  -- ⚠️ 这里以前比的是 playerName。两个都没配昵称的玩家在游戏里都是 'Player'，
  -- 于是双方的包互相被当成"自己的"丢掉 —— 房间里人齐了却一辆车都看不见。
  if not id or id == myId() then return end
  
  
  
  if not validPos(data.pos) or not validRot(data.rot) then
    badPacketCount = badPacketCount + 1
    return
  end
  remotePacketCount = remotePacketCount + 1

  
  
  if data.map and data.map ~= '' then
    peerMap = data.map
    if localMap ~= '' and peerMap ~= localMap and not mapMismatchWarned then
      mapMismatchWarned = true
      local sl = string.match(localMap, '[^/]+$') or localMap
      local sp = string.match(peerMap, '[^/]+$') or peerMap
      table.insert(chat, { name = 'system',
        text = '地图不一致！你=' .. sl .. '，对方=' .. sp .. '（必须同一张地图才能看到对方的车）' })
      chatScrollToBottom = true
      logMsg('地图不一致 本地=' .. localMap .. ' 对方=' .. peerMap)
    end
  end

  local rec = remoteVehicles[id]
  if rec and rec.veh and data.model and rec.model ~= data.model then
    
    despawnRemote(rec)
  end
  if not rec then
    rec = { veh = nil, name = data.name or id, model = data.model,
            lastSeen = timeAccum, spawnFails = 0, nextTryAt = 0, lastRespawn = -100 }
    remoteVehicles[id] = rec
  end

  rec.lastSeen = timeAccum
  if data.name then rec.name = data.name end
  
  if data.model and data.model ~= '' and rec.model ~= data.model then rec.model = data.model end

  
  
  if not rec.veh then
    if timeAccum >= rec.nextTryAt and (timeAccum - lastSpawnTryAt) >= MIN_SPAWN_GAP then
      lastSpawnTryAt = timeAccum
      local okSpawn = spawnRemote(rec, id, data)
      if not okSpawn then
        rec.spawnFails = rec.spawnFails + 1
        local delay = math.min(0.5 * 2 ^ (rec.spawnFails - 1), MAX_SPAWN_BACKOFF)
        rec.nextTryAt = timeAccum + delay
        spawnFailCount = spawnFailCount + 1
        
        if rec.spawnFails == 1 or rec.spawnFails % 10 == 0 then
          logMsg('生成远程车辆失败 x' .. rec.spawnFails .. '（' .. tostring(rec.model) ..
            '），退避 ' .. string.format('%.1f', delay) .. 's 后重试')
        end
      end
    end
    if not rec.veh then return end   
  end

  local ok, js = pcall(jsonEncode, {
    pos = data.pos,
    rot = data.rot,
    vel = data.vel or { 0, 0, 0 },
    rvel = data.rvel or { 0, 0, 0 },
    tim = data.tim or timeAccum,
  })
  if ok and js then
    pcall(function() be:sendToMailbox('srPos' .. id, js) end)
  end
end




local function findRecByVehID(gameVehicleID)
  for id, rec in pairs(remoteVehicles) do
    if rec.veh then
      local ok, gid = pcall(function() return rec.veh:getID() end)
      if ok and gid == gameVehicleID then return id, rec end
    end
  end
  return nil, nil
end


function M.onVEReady(gameVehicleID)
  veReadyCount = veReadyCount + 1
  local found, frec = findRecByVehID(gameVehicleID)
  if found then
    frec.veReady = true
    -- ⚠️ 这才是"设类型/设联机 ID"的正确时机：此刻 VE 模块一定已经加载完，命令不会丢。
    -- （BeamMP 的 MPVehicleGE.onVehicleReady 就是在这里做同样的事）
    queueVETypeAndID(frec.veh, found)
    logMsg('远程车 VE 就绪:', tostring(found))
  else
    logMsg('VE 就绪上报(暂未匹配到车辆):', tostring(gameVehicleID))
  end
end


-- VE 主动来要联机 ID（它发现自己 v.srServerID 是空的）→ 立刻补发。
-- 这是加载竞态的最后一道自愈：spawn 时那次下发丢了也能救回来。
function M.onVEAskID(gameVehicleID)
  local id, rec = findRecByVehID(gameVehicleID)
  if not id then return end
  queueVETypeAndID(rec.veh, id)
  if not rec.askLogged then
    rec.askLogged = true
    logMsg('VE 索要联机 ID，已补发:', tostring(id))
  end
end






function M.applyRemoteTransform(gameVehicleID, jsonStr)
  local veh = getObjectByID(gameVehicleID)
  if not veh then return end
  local ok, d = pcall(jsonDecode, jsonStr)
  if not ok or type(d) ~= 'table' then return end
  local p, r, v, rv = d.pos, d.rot, d.vel, d.rvel
  if type(p) ~= 'table' or type(r) ~= 'table' then return end
  if type(v) ~= 'table' then v = { 0, 0, 0 } end
  if type(rv) ~= 'table' then rv = { 0, 0, 0 } end
  local vv = d.vehVel
  local noCounter = (d.noCounter == 1)

  -- ⚠️ 碰撞保护（BeamMP positionGE.setPositionRotationVelocity 的做法）：
  --    远程车"实际速度"远大于它"自己上报的速度" → 说明刚刚发生了碰撞/爆炸/落地冲击。
  --    这时候再硬传送 + 覆盖速度，等于把这次碰撞的结果直接抹掉 —— 用户看到的就是
  --    "两台车撞不到 / 互相穿模"。这一跳只是"这一帧不修"，下一帧会重新判断。
  if type(vv) == 'table' then
    local lv = veh:getVelocity()
    local cur = math.abs(lv.x) + math.abs(lv.y) + math.abs(lv.z)
    local tgt = math.abs(vv[1] or 0) + math.abs(vv[2] or 0) + math.abs(vv[3] or 0)
    if cur > tgt * 5 then
      skipByCollision = skipByCollision + 1
      return
    end
  end

  pcall(function()
    local refNode = veh:getRefNodeId()
    local dx, dy, dz = veh:getDirectionVectorXYZ()
    local ux, uy, uz = veh:getDirectionVectorUpXYZ()
    local cur = quat():setFromDir(vec3(-dx, -dy, -dz), vec3(ux, uy, uz))
    local want = quat(r[1], r[2], r[3], r[4])
    local delta = cur:inversed() * want

    local localVel = veh:getVelocity()
    veh:setClusterPosRelRot(refNode, p[1], p[2], p[3], delta.x, delta.y, delta.z, delta.w)

    -- setClusterPosRelRot 会把速度一起旋转，所以要把转过的分量扣掉。
    -- 但"刚生成那一跳"（noCounter=1）要跳过：那时车身上的松散件（原木/挂车）本来就快，
    -- 扣一次会让它们朝反方向飞出去（BeamMP 的原话：logs on the T-series would fly backwards）。
    local rx, ry, rz = v[1] or 0, v[2] or 0, v[3] or 0
    if not noCounter then
      local rotVel = localVel:rotated(delta)
      rx, ry, rz = rx - rotVel.x, ry - rotVel.y, rz - rotVel.z
    end
    veh:applyClusterVelocityScaleAdd(refNode, 1, rx, ry, rz)

    -- 角速度只能回 VE 做（GE 没有角速度接口）。
    -- ⚠️ 只动角速度、不动线速度：线速度上面已经设好了，VE 再动一次就是互相打架
    --    （BeamMP positionGE 传的 onlyAngularVelocity=1 就是这个意思）。
    veh:queueLuaCommand('startrideVE.setAngularVelocityOnly(' ..
      tostring(rv[1] or 0) .. ', ' .. tostring(rv[2] or 0) .. ', ' .. tostring(rv[3] or 0) .. ')')
  end)
end

function M.debugRemoteInfo()
  local list = {}
  for id, rec in pairs(remoteVehicles) do
    list[#list + 1] = { id = id, name = rec.name, model = rec.model }
  end
  return list
end




local function getPlayerVeh()
  local ok, veh = pcall(function() return be:getPlayerVehicle(0) end)
  if ok and veh then return veh end
  return nil
end

local function sendLocalVehicle()
  local veh = getPlayerVeh()
  if not veh then return end

  local pos, rot, vel, jbeam = nil, nil, nil, nil
  pcall(function() pos = veh:getPosition() end)
  pcall(function() rot = veh:getRotation() end)
  pcall(function() vel = veh:getVelocity() end)
  pcall(function() jbeam = veh:getJBeamFilename() end)
  if not pos or not rot then return end
  if not (pos.x == pos.x and pos.y == pos.y and pos.z == pos.z) then return end

  
  local q = nil
  pcall(function()
    local dx, dy, dz = veh:getDirectionVectorXYZ()
    local ux, uy, uz = veh:getDirectionVectorUpXYZ()
    q = quat():setFromDir(vec3(-dx, -dy, -dz), vec3(ux, uy, uz))
  end)
  if not q then q = rot end

  
  local rvx, rvy, rvz = 0, 0, 0
  local dt = timeAccum - lastRotTime
  if lastRot and dt > 0.0001 and dt < 1 then
    pcall(function()
      local dq = lastRot:inversed() * q
      local e = dq:toEulerYXZ()
      rvx, rvy, rvz = e.x / dt, e.y / dt, e.z / dt
      if math.abs(rvx) > 30 or math.abs(rvy) > 30 or math.abs(rvz) > 30 then
        rvx, rvy, rvz = 0, 0, 0
      end
    end)
  end
  if not lastRot then lastRot = quat() end
  pcall(function() lastRot:set(q.x, q.y, q.z, q.w) end)
  lastRotTime = timeAccum

  lastJbeam = jbeam or lastJbeam

  queuePacket({
    type = 'vehicle',
    id = myId(),
    name = playerName,
    model = lastJbeam or 'pickup',
    map = localMap,
    pos = { pos.x, pos.y, pos.z },
    rot = { q.x, q.y, q.z, q.w },
    vel = { vel and vel.x or 0, vel and vel.y or 0, vel and vel.z or 0 },
    rvel = { rvx, rvy, rvz },
    tim = timeAccum,
  })

  
  if timeAccum - lastCfgCheck > 2 then
    lastCfgCheck = timeAccum
    pcall(function()
      local vd = extensions.core_vehicle_manager.getVehicleData(veh:getID())
      if vd and vd.config then
        local cfg = serialize(vd.config)
        if cfg and #cfg < 60000 and cfg ~= lastCfgJson then
          lastCfgJson = cfg
          queuePacket({ type = 'vehcfg', id = myId(), name = playerName, cfg = cfg })
        end
      end
    end)
  end
end

local function cleanupRemote()
  for id, rec in pairs(remoteVehicles) do
    if timeAccum - rec.lastSeen > VEHICLE_TIMEOUT then
      despawnRemote(rec)
      remoteVehicles[id] = nil
      table.insert(chat, { name = 'system', text = (rec.name or id) .. ' 离开了' })
      logMsg('移除远程车辆', tostring(rec.name))
    end
  end
  if #chat > CHAT_KEEP then
    local drop = #chat - CHAT_KEEP
    for _ = 1, drop do table.remove(chat, 1) end
  end
end




local function onPacket(data)
  if type(data) ~= 'table' or not data.type then return end

  if data.type == 'vehicle' then
    safeCall('onRemoteVehiclePacket', onRemoteVehiclePacket, data)

  elseif data.type == 'vehcfg' then
    local rec = remoteVehicles[data.id]
    
    
    if rec and rec.veh and type(data.cfg) == 'string' and #data.cfg > 0
       and (timeAccum - (rec.lastRespawn or -100)) >= MIN_CFG_RESPAWN_GAP then
      rec.lastRespawn = timeAccum
      pcall(function() rec.veh:respawn(data.cfg) end)
    end

  elseif data.type == 'chat' then
    if data.name ~= playerName then
      table.insert(chat, { name = data.name or '?', text = data.text or '' })
      chatScrollToBottom = true
    end

  elseif data.type == 'players' then
    if type(data.players) == 'table' then mpPlayers = data.players end
    roomInfo.count = tonumber(data.count) or #mpPlayers
    if tonumber(data.capacity) then roomInfo.capacity = tonumber(data.capacity) end
    if data.host then roomInfo.host = data.host end
    if data.roomName then roomInfo.name = data.roomName end

  elseif data.type == 'system' then
    if data.text then
      table.insert(chat, { name = 'system', text = data.text })
      chatScrollToBottom = true
    end

  elseif data.type == 'room-closed' then
    roomInfo.closed = true
    table.insert(chat, { name = 'system', text = '房主已关闭房间，联机已结束' })
    chatScrollToBottom = true
    for id, rec in pairs(remoteVehicles) do
      despawnRemote(rec)
      remoteVehicles[id] = nil
    end

  elseif data.type == 'config' then
    if data.playerName and data.playerName ~= '' then playerName = data.playerName end

  
  
  elseif data.type == 'relay-state' then
    relayState = data.state or 'unknown'
    relayDetail = data.detail or ''
    if data.roomId then relayRoomId = data.roomId end
    -- 启动器是身份的权威来源：它下发的 ID 与昵称直接采信。
    -- 昵称只在"游戏里没自己配过"（还是默认的 'Player'）时才跟随启动器，
    -- 免得把用户手写进 multiplayer.json 的名字盖掉。
    if data.playerId and data.playerId ~= '' and data.playerId ~= playerId then
      playerId = data.playerId
      logMsg('联机 ID:', playerId)
    end
    if data.playerName and data.playerName ~= '' and (playerName == 'Player' or playerName == '')
       and playerName ~= data.playerName then
      playerName = data.playerName
      logMsg('昵称已按启动器账户同步:', playerName)
    end

  elseif data.type == 'stats' then
    statGameIn = tonumber(data.gameIn) or 0
    statRelayOut = tonumber(data.relayOut) or 0
    statRelayIn = tonumber(data.relayIn) or 0
    statRelayVehicle = tonumber(data.relayVehicle) or 0
    if data.relayConnected ~= nil then
      relayState = (tonumber(data.relayConnected) == 1) and 'connected' or 'disconnected'
    end
  end
end

local function receiveTCP()
  if not tcpSock then return end
  local ok, data, err, partial = pcall(function() return tcpSock:receive(65536) end)
  if not ok then return end
  local chunk = data or partial
  if chunk and #chunk > 0 then recvBuffer = recvBuffer .. chunk end
  if err == 'closed' then
    dropConnection('对方关闭连接')
    return
  end
  if #recvBuffer > 1048576 then recvBuffer = '' end
  while #recvBuffer >= 4 do
    local len = string.byte(recvBuffer, 1) + string.byte(recvBuffer, 2) * 256 +
        string.byte(recvBuffer, 3) * 65536 + string.byte(recvBuffer, 4) * 16777216
    if len <= 0 or len > 1048576 then recvBuffer = '' break end
    if #recvBuffer < 4 + len then break end
    local payload = string.sub(recvBuffer, 5, 4 + len)
    recvBuffer = string.sub(recvBuffer, 5 + len)
    local pok, pdata = pcall(jsonDecode, payload)
    if pok and pdata then safeCall('onPacket', onPacket, pdata) end
  end
end




local function nameColor(name)
  local h = 0
  for i = 1, #name do h = (h * 31 + string.byte(name, i)) % 360 end
  local r = 0.45 + 0.55 * math.abs(math.sin(math.rad(h)))
  local g = 0.45 + 0.55 * math.abs(math.sin(math.rad(h + 120)))
  local b = 0.45 + 0.55 * math.abs(math.sin(math.rad(h + 240)))
  return im.ImVec4(r, g, b, 1)
end

local function drawRect(x, y, w, h, col, rounding)
  if not canDraw then return end
  pcall(function()
    local dl = fnGetDrawList()
    local wp = im.GetWindowPos()
    fnAddRectFilled(dl, im.ImVec2(wp.x + x, wp.y + y), im.ImVec2(wp.x + x + w, wp.y + y + h),
      colorU32(col), rounding or 6, nil)
  end)
end

local function drawDot(x, y, r, col)
  if not (canDraw and fnAddCircleFilled) then return end
  pcall(function()
    local dl = fnGetDrawList()
    local wp = im.GetWindowPos()
    fnAddCircleFilled(dl, im.ImVec2(wp.x + x, wp.y + y), r, colorU32(col), 24)
  end)
end


local function sectionTitle(text, col)
  if canDraw then
    local p = im.GetCursorScreenPos()
    pcall(function()
      fnAddRectFilled(fnGetDrawList(), im.ImVec2(p.x, p.y + 1), im.ImVec2(p.x + 3, p.y + 15),
        colorU32(col or C.accent), 2, nil)
    end)
    im.Dummy(im.ImVec2(8, 0))
    im.SameLine()
  else
    im.TextColored(col or C.accent, '|')
    im.SameLine()
  end
  im.TextColored(col or C.accent, text)
end

local function kvRow(label, value, valCol)
  im.TextColored(C.dim, label)
  im.SameLine()
  local avail = im.GetContentRegionAvail().x
  local w = im.CalcTextSize(value).x
  local x = im.GetCursorPosX() + avail - w - 6
  if x > im.GetCursorPosX() then im.SetCursorPosX(x) end
  im.TextColored(valCol or C.text, value)
end






-- ═══════════════════════════════════════════════════════════════════════════
-- 高光时刻（GE 侧）
--
-- 车辆层（startrideHL.lua）认出"值得回看的瞬间"后，用
--   obj:queueGameEngineLua("startride.onHighlight(type, value, extra, speed)")
-- 把事件扔上来。这里负责：汇总 → 截图 → 落盘 → HUD 提示 → 并入游戏自身统计。
--
-- 顺手还管一件事：**联机期间自动开回放录制**。没有录像，高光就只是一行文字；
-- 有了录像 + 时间码，才能在游戏里跳回去看。录制分段（默认每段 SEGMENT 秒），
-- 这样单个文件不会无限大，且"跳到那一刻"不用等一个几小时的录像加载完。
--
-- ⚠️ 绝不动用户手动开的录制：只有自己调 startRecording() 成功的才会去 stopRecording()
--    （HL.ownRecording 记账）。用户自己按了录制键 / 开着任务自动回放时，我们只借它
--    当前的文件名与时间码来打点。
--
-- ⚠️ 落盘目录 = <userpath>/replays/startride/，与 .rpl 同根。选这里的唯一原因是
--    启动器已经知道怎么解析回放目录（ResolveReplaysDirectory），两边不用再约定别的东西。
--    配置也放同一个目录：<replays>/startride/config.json，由启动器写、模组读。
--
-- ⚠️ 截图走引擎自带的 screenshot.doScreenshot(nil, nil, path, 'jpg')：
--    传的是**不含扩展名**的路径（引擎自己补 .jpg，见 timeslip.lua 的用法）。
--    截图是 GPU 回读 + JPEG 编码，有开销 → 用 HL.shotBusy 串行化，不并发发起。
-- ═══════════════════════════════════════════════════════════════════════════
local HL = {
  active = false,
  sessionId = '',
  startedWall = '',
  duration = 0,
  events = {},
  shots = 0,
  shotBusy = false,
  shotBusyUntil = 0,
  replays = {},          -- { {file=, from=, to=} }
  curReplay = '',
  curReplayFrom = 0,
  ownRecording = false,
  --- 本段录制已进行的秒数。**必须自己计时**：core_replay.getState() 的
  --- positionSeconds 在录制态下恒为 0（那是"回放进度"，录制时没有进度可言），
  --- 依赖它会让时间码全 0、分段录制永不触发。
  recElapsed = 0,
  lastLine = '',
  toastUntil = 0,
  lastTick = 0,
  lastPos = 0,
  -- 配置（启动器写 config.json；读不到就用下面的默认值）
  enabled = true,
  autoRecord = true,
  segmentSeconds = 300,   -- 实测录制约 1~2 MB/s，15 分钟一段会到 1 GB 以上
  maxShots = 40,
  onlyInSession = true,
  dir = '',
  dirReady = false,
  cfgLoaded = false,     -- 配置是否已读（首帧读，不能等开会话时才读）
  sessionMap = '',       -- 本局所在地图，换图即分局
  savedAt = 0,           -- 距上次落盘秒数（定时落盘用）
}

--- 会话进行中每隔这么久落一次盘。联机一局可能几十分钟，
--- 中途崩溃/断电不能把整局高光带走（实测游戏崩过一次，日志还没 flush）。
local AUTOSAVE_INTERVAL = 30

--- <userpath> 归一成斜杠，**并吃掉末尾斜杠**。
--- ⚠️ FS:getUserPath() 实测返回 "…\current\"（带尾斜杠），不处理就会拼出
---    "…/current//replays/startride" —— 实测在这个双斜杠路径下
---    jsonReadFile 读不回、screenshot.doScreenshot 连文件都不生成（返回不报错但静默失败）。
--- 高光目录，**相对 userpath 的路径**。
--- ⚠️ 绝不能拼 FS:getUserPath() 出来的绝对 Windows 路径（"D:/…/current/replays/startride"）：
---    BeamNG 的 FS 是虚拟文件系统，**路径基准就是 userpath**，绝对路径一律找不到。
---    而且失败方式极度迷惑 —— directoryCreate 照样返回 true、jsonWriteFile 不抛错、
---    截图调用也不抛错，但文件一个都没落地（实测白排查两轮，最后靠对照引擎自身写法定位）。
---    引擎代码用的就是这种相对写法：replay.lua 的 FS:directoryCreate("/replays/")、
---    timeslip.lua 的 screenshot.doScreenshot(nil, nil, "screenshots/timeslips/<时间>", 'jpg')。
local function hlDir()
  if HL.dirReady then return HL.dir ~= '' and HL.dir or nil end
  HL.dirReady = true
  local dir = 'replays/startride'
  pcall(function()
    if not FS:directoryExists(dir) then FS:directoryCreate(dir, true) end
  end)
  HL.dir = dir
  return dir
end

--- 读启动器写下来的配置。读不到/坏掉都不能让功能整体挂掉 —— 用默认值继续。
local function hlLoadConfig()
  local dir = hlDir()
  if not dir then return end
  local ok, cfg = pcall(function() return jsonReadFile(dir .. '/config.json') end)
  if not ok or type(cfg) ~= 'table' then return end
  if cfg.enabled ~= nil then HL.enabled = cfg.enabled ~= false end
  if cfg.autoRecord ~= nil then HL.autoRecord = cfg.autoRecord ~= false end
  if tonumber(cfg.segmentSeconds) then
    local s = tonumber(cfg.segmentSeconds)
    if s >= 120 and s <= 7200 then HL.segmentSeconds = s end
  end
  if tonumber(cfg.maxShots) then
    local n = tonumber(cfg.maxShots)
    if n >= 0 and n <= 500 then HL.maxShots = n end
  end
  if cfg.onlyInSession ~= nil then HL.onlyInSession = cfg.onlyInSession ~= false end
  logMsg('高光配置: enabled=' .. tostring(HL.enabled), 'autoRecord=' .. tostring(HL.autoRecord),
    '分段=' .. tostring(HL.segmentSeconds) .. 's')
end

local function hlLabel(typ)
  if typ == 'jump' then return '大跳跃' end
  if typ == 'impact' then return '重击' end
  if typ == 'rollover' then return '翻车' end
  if typ == 'burnout' then return '烧胎' end
  if typ == 'topspeed' then return '极速' end
  return typ
end

--- 数值 + 单位，给 HUD 和列表共用。
local function hlValueText(typ, value, extra)
  if typ == 'jump' then
    if extra and extra >= 1 then
      return string.format('%.1f 秒 · %.0f 米', value, extra)
    end
    return string.format('%.1f 秒', value)
  end
  if typ == 'impact' then return string.format('%.0f 能量', value) end
  if typ == 'rollover' then return string.format('翻滚 %.1f 秒', value) end
  if typ == 'burnout' then return string.format('%.1f 秒', value) end
  if typ == 'topspeed' then
    return string.format('%.0f km/h', value * 3.6)
  end
  return string.format('%.2f', value)
end

local function hlMapName()
  local name = ''
  pcall(function()
    local f = getMissionFilename()
    if f and core_levels and core_levels.getLevelName then
      name = tostring(core_levels.getLevelName(f) or '')
    end
    if (name == '' or name == 'nil') and f then name = tostring(f) end
  end)
  if name == 'nil' then name = '' end
  return name
end

-- ── 录制 ───────────────────────────────────────────────────────────────────
local function hlReplayState()
  local st = nil
  pcall(function() st = core_replay.getState() end)
  if type(st) ~= 'table' then return nil end
  return st
end

--- 开录。返回 true 表示**是我们**开的（会话结束时要负责停）。
local function hlStartRecording()
  local st = hlReplayState()
  if st and st.state == 'recording' then
    -- 已经在录（用户手动 / 任务自动回放）→ 借用，不接管
    HL.curReplay = tostring(st.loadedFile or '')
    HL.ownRecording = false
    logMsg('高光：检测到已有录制，借用不接管 →', HL.curReplay)
    return false
  end
  local ok, res = pcall(function() return core_replay.startRecording() end)
  if ok and type(res) == 'table' and res.success then
    HL.curReplay = tostring(res.filename or '')
    HL.curReplayFrom = 0
    HL.recElapsed = 0
    HL.ownRecording = true
    table.insert(HL.replays, { file = HL.curReplay, from = 0, to = 0 })
    logMsg('高光：已开始录制 →', HL.curReplay)
    return true
  end
  logMsg('高光：开始录制失败', ok and tostring(res and res.message or '?') or tostring(res))
  HL.ownRecording = false
  return false
end

local function hlStopRecording()
  if not HL.ownRecording then return end
  local st = hlReplayState()
  if not st or st.state ~= 'recording' then
    HL.ownRecording = false
    return
  end
  -- 段尾用自己计的时长（positionSeconds 在录制态下恒为 0，见 HL.recElapsed 注释）
  local pos = HL.recElapsed or 0
  local seg = HL.replays[#HL.replays]
  if seg then seg.to = HL.curReplayFrom + pos end
  pcall(function() core_replay.stopRecording() end)
  HL.ownRecording = false
  logMsg('高光：录制已停止并保存', HL.curReplay, '时长', string.format('%.1fs', pos))
end

--- 录制分段：一段录满 segmentSeconds 就停掉再开一段。
--- 不分段的话，两小时的联机会得到一个几百 MB、加载半天的文件。
local function hlRotateIfNeeded()
  if not HL.ownRecording then return end
  local st = hlReplayState()
  if not st or st.state ~= 'recording' then return end
  -- ⚠️ 用 recElapsed 而不是 st.positionSeconds：后者在录制态下恒为 0，
  --    拿它判分段会让 `pos < segmentSeconds` 永远成立 —— 分段从不触发。
  local pos = HL.recElapsed or 0
  HL.lastPos = pos
  if pos < HL.segmentSeconds then return end

  local seg = HL.replays[#HL.replays]
  if seg then seg.to = HL.curReplayFrom + pos end
  HL.curReplayFrom = HL.curReplayFrom + pos
  pcall(function() core_replay.stopRecording() end)
  HL.ownRecording = false
  hlStartRecording()          -- 内部会把 recElapsed 清零
  logMsg('高光：录制已分段，本段', string.format('%.0fs', pos))
end

-- ── 截图 ───────────────────────────────────────────────────────────────────
local function hlTakeShot(typ)
  if HL.shots >= HL.maxShots then return nil end
  if HL.shotBusy and os.clock() < HL.shotBusyUntil then return nil end
  local dir = hlDir()
  if not dir then return nil end

  HL.shots = HL.shots + 1
  local base = string.format('%s/hl-%s-%02d', dir, HL.sessionId, HL.shots)
  HL.shotBusy = true
  HL.shotBusyUntil = os.clock() + 2.0   -- 2 秒内不再发起（截图是异步的，没有可靠的回调时序）
  local ok = pcall(function() screenshot.doScreenshot(nil, nil, base, 'jpg') end)
  if not ok then
    HL.shotBusy = false
    HL.shots = HL.shots - 1
    return nil
  end
  return 'hl-' .. HL.sessionId .. string.format('-%02d', HL.shots) .. '.jpg'
end

-- ── 落盘 ───────────────────────────────────────────────────────────────────
local function hlSave(reason)
  local dir = hlDir()
  if not dir or HL.sessionId == '' then return end

  -- 段尾兜底。正常退出时 hlStopRecording 已经记过 to；但**游戏被强杀**、
  -- 或退出时扩展在卸载阶段拿不到 core_replay 状态（实测这条路径会走提前 return），
  -- to 就会一直是 0 —— 界面上看到的就是"0 秒的录像段"。
  -- 这里用我们自己的计时补上，宁可粗一点也不能是 0。
  local tail = HL.curReplayFrom + (HL.recElapsed or 0)
  for _, seg in ipairs(HL.replays) do
    if (tonumber(seg.to) or 0) <= 0 then seg.to = tail end
  end

  local counts = {}
  for _, e in ipairs(HL.events) do
    counts[e.type] = (counts[e.type] or 0) + 1
  end

  local data = {
    version = 1,
    sessionId = HL.sessionId,
    reason = reason or 'normal',
    player = playerName,
    map = hlMapName(),
    room = { id = relayRoomId or '', name = roomInfo.name or '', players = roomInfo.count or 0 },
    startedAt = HL.startedWall,
    endedAt = os.date('%Y-%m-%d %H:%M:%S'),
    durationSeconds = HL.duration,
    replays = HL.replays,
    highlights = HL.events,
    counts = counts,
  }

  local path = dir .. '/session-' .. HL.sessionId .. '.json'
  local ok = pcall(function() return jsonWriteFile(path, data) end)
  logMsg('高光：已落盘', path, '共', tostring(#HL.events), '条', ok and 'ok' or 'FAILED')
end

-- ── 会话 ───────────────────────────────────────────────────────────────────
local function hlStartSession()
  if HL.active then return end
  HL.active = true
  HL.sessionId = os.date('%Y-%m-%d_%H-%M-%S')
  HL.startedWall = os.date('%Y-%m-%d %H:%M:%S')
  HL.duration = 0
  HL.events = {}
  HL.replays = {}
  HL.shots = 0
  HL.curReplay = ''
  HL.curReplayFrom = 0
  HL.recElapsed = 0
  HL.lastPos = 0
  HL.ownRecording = false
  HL.lastLine = ''
  HL.savedAt = 0
  HL.sessionMap = localMap or ''

  hlLoadConfig()
  if not HL.enabled then
    HL.active = false
    return
  end

  logMsg('高光：联机会话开始', HL.sessionId, '地图', hlMapName())
  if HL.autoRecord then
    pcall(hlStartRecording)
  end
end

local function hlEndSession(reason)
  if not HL.active then return end
  HL.active = false
  pcall(hlStopRecording)
  local n = #HL.events
  pcall(function() hlSave(reason or 'normal') end)
  logMsg('高光：会话结束，共记录', tostring(n), '条高光')
  HL.events = {}
  HL.replays = {}
end

--- 车辆层报上来的高光事件入口。
--- ⚠️ 签名必须与 startrideHL.lua 里 string.format 的那串严格一致。
--- ⚠️ 只有**联机中**（或配置里 onlyInSession=false）才落盘：单人开车也检测，
---    但不往磁盘写 —— 否则随便开一圈就多一个 JSON 文件。
function M.onHighlight(typ, value, extra, speed)
  pcall(function()
    if type(typ) ~= 'string' then return end
    value = tonumber(value) or 0
    extra = tonumber(extra) or 0
    speed = tonumber(speed) or 0

    -- 并入游戏自带的玩法统计（F1 统计面板 / 生涯里程碑都能读到）
    pcall(function()
      if gameplay_statistic and gameplay_statistic.metricAdd then
        gameplay_statistic.metricAdd('startride/highlight/' .. typ, 1)
      end
    end)

    local capturing = HL.active or not HL.onlyInSession
    local line = hlLabel(typ) .. ' ' .. hlValueText(typ, value, extra)

    -- HUD 上的"最近一条"始终更新，哪怕没在联机也让人看得见检测在工作
    HL.lastLine = line
    HL.toastUntil = os.clock() + 5

    if not capturing then
      -- 没在记录也留一行日志（单人开车同样检测，只是不落盘）。
      -- 这一行也是实机验证的唯一判据 —— 见 _sr_shots/_hl_ingame.py。
      logMsg('高光(未记录)：' .. line)
      return
    end

    -- 时间码 = 本段之前各段的累计时长 + 本段已录时长。
    -- 借用的录制没有我们的起点，只能退回 positionSeconds（可能为 0）。
    local offset = HL.curReplayFrom + (HL.recElapsed or 0)
    local st = hlReplayState()
    if st and st.state == 'recording' then
      local p = tonumber(st.positionSeconds)
      if p and p > 0 then offset = HL.curReplayFrom + p end
    end

    local ev = {
      type = typ,
      label = hlLabel(typ),
      value = value,
      extra = extra,
      speed = speed,
      offset = offset,
      replay = HL.curReplay ~= '' and HL.curReplay or nil,
      at = os.date('%H:%M:%S'),
      wall = os.time(),
    }
    local shot = hlTakeShot(typ)
    if shot then ev.shot = shot end
    table.insert(HL.events, ev)

    logMsg('高光：' .. line, '时间码=' .. string.format('%.1fs', offset),
      ev.replay and ('录像=' .. ev.replay) or '无录像', shot or '无截图')
  end)
end

--- 每帧调用。做三件事：会话边界检测（中继连上/断开）、录制分段、截图串行化超时复位。
local function hlTick(dtSim, dtReal)
  if HL.duration then HL.duration = HL.duration + (dtSim or 0) end
  -- 自己给录制计时（见 HL.recElapsed 注释）。只在本模组开的录制期间计，
  -- 借用的录制没有起点，计了反而错。
  if HL.ownRecording then HL.recElapsed = (HL.recElapsed or 0) + (dtSim or 0) end
  if HL.shotBusy and os.clock() > HL.shotBusyUntil then HL.shotBusy = false end

  -- 配置必须首帧就读：onlyInSession 参与下面「要不要开会话」的判断，
  -- 放在 hlStartSession 里读会变成先有鸡还是先有蛋（永远读不到 false）。
  if not HL.cfgLoaded then
    HL.cfgLoaded = true
    pcall(hlLoadConfig)
  end

  -- 会话边界：中继连上（且房间没关）算一局；
  -- 关掉「仅联机」后，只要进了地图也算一局（单人开车同样留档）。
  -- 放在每帧轮询而不是改写 relayState 的赋值点，是因为那个状态有两条写入路径
  -- （relay-state 消息、stats 消息），改两处容易漏一处。
  local live = (relayState == 'connected') and not roomInfo.closed
  local always = (HL.onlyInSession == false) and (localMap ~= '')
  local want = live or always

  if want and HL.active and HL.sessionMap ~= '' and localMap ~= ''
     and localMap ~= HL.sessionMap then
    pcall(hlEndSession, 'map-change')          -- 换图 = 上一局结束
  end
  if want and not HL.active then
    pcall(hlStartSession)
  elseif (not want) and HL.active then
    pcall(hlEndSession, live and 'disconnect' or 'left-level')
  end

  -- 以下不必每帧做，1 秒一次足够（getState 是跨 VM 调用）
  HL.lastTick = HL.lastTick + (dtReal or 0)
  if HL.lastTick < 1.0 then return end
  HL.lastTick = 0
  if not HL.active then return end
  if HL.ownRecording then pcall(hlRotateIfNeeded) end

  -- 定时落盘（同一文件覆盖写）
  HL.savedAt = (HL.savedAt or 0) + 1
  if HL.savedAt >= AUTOSAVE_INTERVAL then
    HL.savedAt = 0
    pcall(function() hlSave('autosave') end)
  end
end

local function relayStateText()
  if relayState == 'connected' then return '已连接', C.ok end
  if relayState == 'connecting' then return '连接中', C.warn end
  if relayState == 'retrying' then return '重连中', C.warn end
  if relayState == 'failed' then return '连接失败', C.danger end
  if relayState == 'disconnected' then return '已断开', C.danger end
  return '未知', C.dim
end

local function drawHUD()
  local W, H = 268, 126
  local opened = false
  local ok, err = pcall(function()
    im.SetNextWindowPos(im.ImVec2(18, 18), im.Cond_FirstUseEver)
    im.SetNextWindowSize(im.ImVec2(W, H), im.Cond_Always)
    im.SetNextWindowBgAlpha(0.86)
    if not im.Begin('##sr_hud', hudOn,
        im.WindowFlags_NoTitleBar + im.WindowFlags_NoResize + im.WindowFlags_NoScrollbar +
        im.WindowFlags_NoMove + im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings) then
      im.End()
      return
    end
    opened = true

    local stateCol = roomInfo.closed and C.danger or (connected and C.ok or (connecting and C.warn or C.danger))
    drawRect(0, 0, 4, H, stateCol, 0)

    im.Dummy(im.ImVec2(10, 0))
    im.SameLine()
    drawDot(6 + im.GetCursorPosX(), 7, 4, stateCol)
    im.Dummy(im.ImVec2(14, 0))
    im.SameLine()
    im.TextColored(C.text, 'StartRide')
    im.SameLine()
    im.TextColored(C.dim, 'v' .. MOD_VERSION)

    local availX = im.GetContentRegionAvail().x
    local txt = roomInfo.closed and '已关闭' or (connected and '在线' or (connecting and '连接中' or '未连接'))
    local tx = im.GetCursorPosX() + availX - im.CalcTextSize(txt).x - 2
    if tx > im.GetCursorPosX() then im.SetCursorPosX(tx) end
    im.TextColored(stateCol, txt)

    im.Dummy(im.ImVec2(10, 4))
    im.SameLine()
    if roomInfo.name ~= '' then
      im.TextColored(C.text, roomInfo.name)
    else
      im.TextColored(C.dim, '未加入房间')
    end

    im.Dummy(im.ImVec2(10, 0))
    im.SameLine()
    im.TextColored(C.dim, '玩家')
    im.SameLine()
    im.TextColored(C.accent, tostring(roomInfo.count) .. '/' .. tostring(roomInfo.capacity > 0 and roomInfo.capacity or '?'))
    im.SameLine()
    im.TextColored(C.dim, '  车辆')
    im.SameLine()
    im.TextColored(C.ok, tostring(countRemote()))

    im.Dummy(im.ImVec2(10, 4))
    im.SameLine()
    im.TextColored(C.dim, 'F8 面板  ·  T 聊天  ·  Tab 玩家')

    
    
    
    
    im.Dummy(im.ImVec2(10, 4))
    im.SameLine()
    im.TextColored(C.dim, '中继')
    im.SameLine()
    local rTxt, rCol = relayStateText()
    im.TextColored(rCol, rTxt)
    im.SameLine()
    im.TextColored(C.dim, '  收包')
    im.SameLine()
    im.TextColored(remotePacketCount > 0 and C.ok or C.dim, tostring(remotePacketCount))

    -- 高光：本场次数 + 最近一条。刚触发 5 秒内用亮色，之后转暗，一眼看得出"刚发生"。
    im.Dummy(im.ImVec2(10, 4))
    im.SameLine()
    im.TextColored(C.dim, '高光')
    im.SameLine()
    im.TextColored(#HL.events > 0 and C.accent or C.dim, tostring(#HL.events))
    im.SameLine()
    if HL.lastLine ~= '' then
      local fresh = os.clock() < HL.toastUntil
      im.TextColored(fresh and C.ok or C.dim, '  ' .. HL.lastLine)
    else
      im.TextColored(C.dim, '  还没有')
    end
  end)
  
  
  
  if opened then pcall(function() im.End() end) end
  if not ok then logMsg('HUD 绘制出错:', tostring(err)) end
end

local function remoteDistance(name)
  local pos = nil
  pcall(function()
    local pv = getPlayerVeh()
    if pv then pos = pv:getPosition() end
  end)
  if not pos then return nil end
  for _, rec in pairs(remoteVehicles) do
    if rec.name == name and rec.veh then
      local ok, rp = pcall(function() return rec.veh:getPosition() end)
      if ok and rp then
        local dx, dy, dz = rp.x - pos.x, rp.y - pos.y, rp.z - pos.z
        return math.sqrt(dx * dx + dy * dy + dz * dz)
      end
    end
  end
  return nil
end

local function drawPlayerList()
  local opened = false
  local ok, err = pcall(function()
    im.SetNextWindowPos(im.ImVec2(18, 132), im.Cond_FirstUseEver)
    im.SetNextWindowSize(im.ImVec2(268, 240), im.Cond_FirstUseEver)
    im.SetNextWindowBgAlpha(0.88)
    if not im.Begin('##sr_players', playerListOpen,
        im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings) then
      im.End()
      return
    end
    opened = true

    sectionTitle('房间成员 (' .. tostring(roomInfo.count) .. ')')
    im.Separator()

    local rowH = 22
    local cpos = im.GetCursorScreenPos()
    drawRect(0, cpos.y - im.GetWindowPos().y, im.GetContentRegionAvail().x + 8, rowH,
      im.ImVec4(0.16, 0.42, 0.72, 0.35), 4)
    drawDot(6 + im.GetCursorPosX(), cpos.y - im.GetWindowPos().y + rowH / 2, 3.5, C.ok)
    im.Dummy(im.ImVec2(14, 0))
    im.SameLine()
    im.TextColored(C.text, playerName)
    im.SameLine()
    im.TextColored(C.dim, '(你)')

    local shown = 0
    for _, pl in ipairs(mpPlayers) do
      local nm = pl.name or pl.id
      if nm and nm ~= playerName then
        shown = shown + 1
        local p2 = im.GetCursorScreenPos()
        drawDot(6 + im.GetCursorPosX(), p2.y - im.GetWindowPos().y + 7, 3.5, nameColor(nm))
        im.Dummy(im.ImVec2(14, 0))
        im.SameLine()
        im.TextColored(C.text, nm)
        local d = remoteDistance(nm)
        im.SameLine()
        if d then
          im.TextColored(C.dim, string.format('%.0f 米', d))
        else
          im.TextColored(C.warn, '同步中...')
        end
      end
    end

    if shown == 0 then
      im.TextColored(C.dim, '  等待其他玩家加入...')
    end
  end)
  
  if opened then pcall(function() im.End() end) end
  if not ok then logMsg('玩家列表绘制出错:', tostring(err)) end
end

local function drawChat()
  local H = 232
  local opened = false
  local ok, err = pcall(function()
    im.SetNextWindowPos(im.ImVec2(18, 300), im.Cond_FirstUseEver)
    im.SetNextWindowSize(im.ImVec2(440, H), im.Cond_FirstUseEver)
    im.SetNextWindowBgAlpha(chatOpacity[0])
    if not im.Begin('##sr_chat', chatOpen,
        im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings) then
      im.End()
      return
    end
    opened = true

    sectionTitle('聊天')
    im.SameLine()
    local sw = 100
    local avail = im.GetContentRegionAvail().x
    im.SetCursorPosX(im.GetCursorPosX() + avail - sw - 4)
    im.PushItemWidth(sw)
    im.SliderFloat('##sr_alpha', chatOpacity, 0.25, 1.0, '透明度')
    im.PopItemWidth()
    im.Separator()

    beginChild('##sr_chat_log', im.ImVec2(0, -32), true)
    for _, m in ipairs(chat) do
      if m.name == 'system' then
        im.TextColored(C.sys, '· ' .. tostring(m.text))
      else
        im.TextColored(nameColor(tostring(m.name)), tostring(m.name))
        im.SameLine()
        im.TextColored(C.dim, ':')
        im.SameLine()
        im.Text(tostring(m.text))
      end
    end
    if chatScrollToBottom then
      pcall(function() im.SetScrollHereY(1.0) end)
      chatScrollToBottom = false
    end
    endChild()

    if connected then
      if chatWantFocus then
        im.SetKeyboardFocusHere()
        chatWantFocus = false
        chatFocused = true
      end
      im.PushItemWidth(-70)
      if im.InputText('##sr_chat_in', chatInput, 256, im.InputTextFlags_EnterReturnsTrue) then
        local text = ffi.string(chatInput)
        if text and text ~= '' and timeAccum - lastChatSend > CHAT_COOLDOWN then
          lastChatSend = timeAccum
          queuePacket({ type = 'chat', name = playerName, text = text })
          table.insert(chat, { name = playerName, text = text })
          chatScrollToBottom = true
        end
        chatInput = im.ArrayChar(256)
        chatWantFocus = true
      end
      im.PopItemWidth()
      im.SameLine()
      if im.Button('发送##sr_chat_send', im.ImVec2(60, 0)) then
        local text = ffi.string(chatInput)
        if text and text ~= '' then
          queuePacket({ type = 'chat', name = playerName, text = text })
          table.insert(chat, { name = playerName, text = text })
          chatInput = im.ArrayChar(256)
          chatScrollToBottom = true
          chatWantFocus = true
        end
      end
    else
      im.TextColored(C.warn, '连接中，聊天暂不可用...')
    end
  end)
  
  if opened then pcall(function() im.End() end) end
  if not ok then logMsg('聊天窗绘制出错:', tostring(err)) end
end


local function panelStatus()
  sectionTitle('连接')
  im.Separator()
  kvRow('桥接端口', LAUNCHER_IP .. ':' .. tostring(LAUNCHER_PORT))
  kvRow('本地桥', connected and '已连接' or (connecting and '连接中' or '未连接'),
    connected and C.ok or (connecting and C.warn or C.danger))
  if not connected then
    kvRow('重试次数', tostring(connectRetryCount),
      connectRetryCount > 0 and C.warn or C.dim)
    if connectRetryCount >= 3 then
      
      
      im.TextColored(C.danger, '· 连不上启动器（' .. LAUNCHER_IP .. ':' .. tostring(LAUNCHER_PORT) .. '）。')
      im.TextColored(C.danger, '  启动器就是联机的本地桥，请确认它仍在运行：')
      im.TextColored(C.danger, '  关掉它的窗口会收进托盘，从托盘菜单选')
      im.TextColored(C.danger, '  「退出启动器」才算真的退出。')
    end
  end
  
  local rTxt, rCol = relayStateText()
  kvRow('中继通道', rTxt, rCol)
  kvRow('我的昵称', playerName)
  if relayDetail ~= '' and relayState ~= 'connected' then
    im.TextColored(C.danger, '· ' .. relayDetail)
  end

  im.Dummy(im.ImVec2(0, 8))
  sectionTitle('房间')
  im.Separator()
  kvRow('房间名', roomInfo.name ~= '' and roomInfo.name or '-')
  kvRow('房间 ID', relayRoomId ~= '' and relayRoomId or '-')
  kvRow('房主', roomInfo.host ~= '' and roomInfo.host or '-')
  kvRow('人数', tostring(roomInfo.count) .. ' / ' .. tostring(roomInfo.capacity > 0 and roomInfo.capacity or '?'))
  kvRow('远程车辆', tostring(countRemote()) .. ' / ' .. tostring(MAX_REMOTE))
  
  local slMap = localMap ~= '' and (string.match(localMap, '[^/]+$') or localMap) or '-'
  local mismatch = (localMap ~= '' and peerMap ~= '' and peerMap ~= localMap)
  kvRow('本机地图', slMap, mismatch and C.danger or C.text)
  if peerMap ~= '' then
    kvRow('对方地图', (string.match(peerMap, '[^/]+$') or peerMap), mismatch and C.danger or C.text)
  end

  
  im.Dummy(im.ImVec2(0, 8))
  sectionTitle('数据流（哪一项为 0 就是哪里断了）')
  im.Separator()
  kvRow('本地上报 /10秒', tostring(statGameIn), statGameIn > 0 and C.ok or C.danger)
  kvRow('转发到中继 /10秒', tostring(statRelayOut), statRelayOut > 0 and C.ok or C.danger)
  kvRow('中继下行 /10秒', tostring(statRelayIn), statRelayIn > 0 and C.ok or C.danger)
  kvRow('远程车辆包 /10秒', tostring(statRelayVehicle), statRelayVehicle > 0 and C.ok or C.danger)
  kvRow('累计收到远程包', tostring(remotePacketCount), remotePacketCount > 0 and C.ok or C.dim)
  kvRow('生成车辆 成功/失败', tostring(spawnOkCount) .. ' / ' .. tostring(spawnFailCount),
    spawnFailCount > 0 and C.warn or C.ok)
  
  kvRow('车辆扩展就绪', tostring(veReadyCount), veReadyCount > 0 and C.ok or C.warn)
  kvRow('碰撞保护跳过修正', tostring(skipByCollision), C.dim)
  
  
  
  kvRow('丢弃脏数据包', tostring(badPacketCount), badPacketCount > 0 and C.warn or C.ok)
  kvRow('幽灵车辆待清理', tostring(#pendingCleanup), #pendingCleanup > 0 and C.danger or C.ok)
  if lastSpawnError ~= '' then
    im.TextColored(C.warn, '· ' .. lastSpawnError)
  end

  im.Dummy(im.ImVec2(0, 8))
  sectionTitle('同步')
  im.Separator()
  kvRow('上报频率', string.format('%.0f Hz', 1 / SEND_RATE))
  kvRow('待发队列', tostring(#outBuffer) .. ' 字节')

  im.Dummy(im.ImVec2(0, 12))
  if im.Button('立即重连##sr_re', im.ImVec2(110, 0)) then
    dropConnection('手动重连')
    connectTCP()
  end
  im.SameLine()
  if im.Button('重建远程车辆##sr_respawn', im.ImVec2(140, 0)) then
    for id, rec in pairs(remoteVehicles) do
      despawnRemote(rec)
      remoteVehicles[id] = nil
    end
  end
end

local function panelPlayers()
  sectionTitle('房间成员 (' .. tostring(roomInfo.count) .. ')')
  im.Separator()
  im.TextColored(C.ok, '●')
  im.SameLine()
  im.TextColored(C.text, playerName .. '  (你)')
  for _, pl in ipairs(mpPlayers) do
    local nm = pl.name or pl.id
    if nm and nm ~= playerName then
      im.TextColored(nameColor(nm), '●')
      im.SameLine()
      im.Text(nm)
      im.SameLine()
      
      
      if pl.version and pl.version ~= '' then
        local sameVer = tostring(pl.version) == MOD_VERSION
        im.TextColored(sameVer and C.dim or C.danger, 'v' .. tostring(pl.version))
        im.SameLine()
      end
      local d = remoteDistance(nm)
      if d then
        im.TextColored(C.dim, string.format('  %.0f 米', d))
      else
        im.TextColored(C.warn, '  未收到车辆数据')
      end
    end
  end
end

local function panelChat()
  beginChild('##sr_panel_chat', im.ImVec2(0, -6), true)
  for _, m in ipairs(chat) do
    if m.name == 'system' then
      im.TextColored(C.sys, '· ' .. tostring(m.text))
    else
      im.TextColored(nameColor(tostring(m.name)), tostring(m.name) .. ':')
      im.SameLine()
      im.Text(tostring(m.text))
    end
  end
  if chatScrollToBottom then
    pcall(function() im.SetScrollHereY(1.0) end)
    chatScrollToBottom = false
  end
  endChild()
end

local function panelSettings()
  sectionTitle('界面')
  im.Separator()
  im.Checkbox('显示状态 HUD', hudOn)
  im.Checkbox('显示聊天窗', chatOpen)
  im.Checkbox('显示玩家列表', playerListOpen)
  im.Dummy(im.ImVec2(0, 6))
  im.PushItemWidth(220)
  im.SliderFloat('聊天窗透明度##sr_op', chatOpacity, 0.25, 1.0)
  im.PopItemWidth()

  im.Dummy(im.ImVec2(0, 10))
  sectionTitle('快捷键')
  im.Separator()
  im.TextColored(C.dim, 'F8       打开 / 关闭本面板')
  im.TextColored(C.dim, 'T        聚焦聊天输入')
  im.TextColored(C.dim, 'Tab      玩家列表')
  im.TextColored(C.dim, 'Esc      退出聊天输入')
end

local PANEL_SECTIONS = { panelStatus, panelPlayers, panelChat, panelSettings }

local function drawPanel()
  local opened = false
  local ok, err = pcall(function()
    im.SetNextWindowSize(im.ImVec2(560, 380), im.Cond_FirstUseEver)
    im.SetNextWindowBgAlpha(0.94)
    if not im.Begin('StartRide 联机面板', panelOpen,
        im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings) then
      im.End()
      return
    end
    opened = true

    if canTab then
      if fnBeginTabBar('##sr_tabs', nil) then
        if fnBeginTabItem('状态##sr_t1', nil, nil) then panelStatus() fnEndTabItem() end
        if fnBeginTabItem('玩家##sr_t2', nil, nil) then panelPlayers() fnEndTabItem() end
        if fnBeginTabItem('聊天##sr_t3', nil, nil) then panelChat() fnEndTabItem() end
        if fnBeginTabItem('设置##sr_t4', nil, nil) then panelSettings() fnEndTabItem() end
        fnEndTabBar()
      end
    else
      
      for i, nm in ipairs(PANEL_TABS) do
        if i > 1 then im.SameLine() end
        local active = (panelTab == i)
        if active then pcall(function() im.PushStyleColor2(im.Col_Button, C.accent) end) end
        if im.Button(nm .. '##sr_seg' .. tostring(i), im.ImVec2(96, 0)) then panelTab = i end
        if active then pcall(function() im.PopStyleColor(1) end) end
      end
      im.Dummy(im.ImVec2(0, 4))
      im.Separator()
      local fn = PANEL_SECTIONS[panelTab] or panelStatus
      fn()
    end
  end)
  
  if opened then pcall(function() im.End() end) end
  if not ok then logMsg('联机面板绘制出错:', tostring(err)) end
end

local function handleKeys()
  local wantText = false
  pcall(function() wantText = im.GetIO().WantTextInput end)

  if not wantText then
    local f8 = false
    pcall(function() f8 = im.IsKeyPressed(im.Key_F8) end)
    if f8 then panelOpen[0] = not panelOpen[0] end

    local tab = false
    pcall(function() tab = im.IsKeyPressed(im.Key_Tab) end)
    if tab then playerListOpen[0] = not playerListOpen[0] end

    local tDown = false
    pcall(function() tDown = im.IsKeyDown(im.Key_T) end)
    if not chatFocused and not keyHeld.t and tDown then
      chatOpen[0] = true
      chatWantFocus = true
    end
    keyHeld.t = tDown
  else
    keyHeld.t = false
    if chatFocused then
      local esc = false
      pcall(function() esc = im.IsKeyPressed(im.Key_Escape) end)
      if esc then
        chatFocused = false
        chatWantFocus = false
      end
    end
  end
end




local function onUpdateRaw(dtReal, dtSim, dtRaw)
  frameCount = frameCount + 1
  timeAccum = timeAccum + (dtReal or 0.016)

  
  
  if (dtReal or 0) > 1.0 and timeAccum - lastStallLog > 5 then
    lastStallLog = timeAccum
    logMsg(string.format('检测到卡顿: 单帧 %.2fs', dtReal),
      '远程车=' .. tostring(countRemote()),
      '生成失败=' .. tostring(spawnFailCount),
      '脏包=' .. tostring(badPacketCount),
      '待清理=' .. tostring(#pendingCleanup))
  end

  
  if frameCount % 120 == 0 then
    pcall(function() localMap = getMissionFilename() or '' end)
  end

  if not connected then
    checkConnection()
    
    
    
    if timeAccum >= nextConnectTry then
      nextConnectTry = timeAccum + connectBackoff
      connectTCP()
    end
  else
    safeCall('receiveTCP', receiveTCP)
    flushOut()

    if timeAccum - lastSend >= SEND_RATE then
      lastSend = timeAccum
      safeCall('sendLocalVehicle', sendLocalVehicle)
      flushOut()
    end
    if timeAccum - lastPingSend > PING_INTERVAL then
      lastPingSend = timeAccum
      queuePacket({ type = 'ping', name = playerName })
      flushOut()
    end
    if frameCount % 120 == 0 then
      safeCall('cleanupRemote', cleanupRemote)
    end
  end

  if not initialized then return end
  safeCall('hlTick', hlTick, dtSim, dtReal)
  handleKeys()
  if panelOpen[0] then drawPanel() end
  if hudOn[0] then drawHUD() end
  if playerListOpen[0] then drawPlayerList() end
  if chatOpen[0] then drawChat() end
end




function M.onExtensionLoaded()
  logMsg('GE 扩展 v' .. MOD_VERSION .. ' 已加载')
  initialized = true
  pcall(function()
    local cfg = jsonReadFile('startride/multiplayer.json')
    if cfg then
      if cfg.playerName and cfg.playerName ~= '' then playerName = cfg.playerName end
      if cfg.roomName and cfg.roomName ~= '' then
        roomInfo.name = cfg.roomName
      elseif cfg.roomId then
        roomInfo.name = cfg.roomId
      end
    end
  end)
  logMsg('玩家昵称:', playerName)
  connectTCP()
end

function M.onInit()
  pcall(function() setExtensionUnloadMode(M, 'manual') end)
end

function M.onUpdate(dtReal, dtSim, dtRaw)
  pcall(onUpdateRaw, dtReal, dtSim, dtRaw)
end

function M.onExtensionUnloaded()
  -- 卸载前把当前高光会话存档，否则这一场就白录了
  pcall(hlEndSession, 'unload')
  for _, rec in pairs(remoteVehicles) do despawnRemote(rec) end
  remoteVehicles = {}
  dropConnection('扩展卸载')
  initialized = false
end

return M
