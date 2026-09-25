








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
local MOD_VERSION = '2.9.4'






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
  pcall(function() veh:setField('protected', 0, '1') end)
  
  
  
  
  
  pcall(function()
    veh:queueLuaCommand("extensions.loadModulesInDirectory('lua/vehicle/extensions/startride')")
  end)
  pcall(function() veh:queueLuaCommand("startrideVE.setVehicleType('R')") end)
  pcall(function() veh:queueLuaCommand('startrideVE.setServerID(' .. string.format('%q', tostring(id)) .. ')') end)
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
  if not id or id == playerName then return end
  
  
  
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




function M.onVEReady(gameVehicleID)
  veReadyCount = veReadyCount + 1
  local found = nil
  for id, rec in pairs(remoteVehicles) do
    if rec.veh then
      local ok, gid = pcall(function() return rec.veh:getID() end)
      if ok and gid == gameVehicleID then found = id break end
    end
  end
  if found then
    remoteVehicles[found].veReady = true
    logMsg('远程车 VE 就绪:', tostring(found))
  else
    logMsg('VE 就绪上报(暂未匹配到车辆):', tostring(gameVehicleID))
  end
end






function M.applyRemoteTransform(gameVehicleID, jsonStr)
  local veh = getObjectByID(gameVehicleID)
  if not veh then return end
  local ok, d = pcall(jsonDecode, jsonStr)
  if not ok or type(d) ~= 'table' then return end
  local p, r, v, rv = d.pos, d.rot, d.vel, d.rvel
  if type(p) ~= 'table' or type(r) ~= 'table' then return end
  if type(rv) ~= 'table' then rv = { 0, 0, 0 } end

  pcall(function()
    local refNode = veh:getRefNodeId()
    local dx, dy, dz = veh:getDirectionVectorXYZ()
    local ux, uy, uz = veh:getDirectionVectorUpXYZ()
    local cur = quat():setFromDir(vec3(-dx, -dy, -dz), vec3(ux, uy, uz))
    local want = quat(r[1], r[2], r[3], r[4])
    local delta = cur:inversed() * want

    
    local localVel = veh:getVelocity()
    veh:setClusterPosRelRot(refNode, p[1], p[2], p[3], delta.x, delta.y, delta.z, delta.w)

    if type(v) == 'table' then
      local rotVel = localVel:rotated(delta)
      veh:applyClusterVelocityScaleAdd(refNode, 1,
        (v[1] or 0) - rotVel.x,
        (v[2] or 0) - rotVel.y,
        (v[3] or 0) - rotVel.z)
    end

    
    veh:queueLuaCommand('startrideVE.setAngularVelocity(0, 0, 0, ' ..
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
    id = playerName,
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
          queuePacket({ type = 'vehcfg', id = playerName, name = playerName, cfg = cfg })
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






local function relayStateText()
  if relayState == 'connected' then return '已连接', C.ok end
  if relayState == 'connecting' then return '连接中', C.warn end
  if relayState == 'retrying' then return '重连中', C.warn end
  if relayState == 'failed' then return '连接失败', C.danger end
  if relayState == 'disconnected' then return '已断开', C.danger end
  return '未知', C.dim
end

local function drawHUD()
  local W, H = 268, 104
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
  for _, rec in pairs(remoteVehicles) do despawnRemote(rec) end
  remoteVehicles = {}
  dropConnection('扩展卸载')
  initialized = false
end

return M
