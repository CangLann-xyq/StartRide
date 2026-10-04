








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

local MOD_VERSION = '1.0.0'






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
-- 断开时置位：下次连上后第一件事是补一条完整 ready，让两端的帧边界重新对齐。
local sendOutDirty = false

local timeAccum = 0
local lastSend = 0
local lastPingSend = 0
local frameCount = 0

local remoteVehicles = {}
local mpPlayers = {}
local roomInfo = { name = '', count = 0, capacity = 0, host = '', closed = false, gameMode = '' }
local chat = {}
local playerName = 'Player'

local playerId = nil
local function myId()
  if playerId and playerId ~= '' then return playerId end
  return playerName
end
local initialized = false


-- ---------------------------------------------------------------------------
-- 玩法模块（正式玩法的输入限制 / 阶段机 / 出生规划 / HUD）
-- 单独拆文件，逐个 require。任何一个加载失败都不能拖垮联机主体，
-- 所以统一走 pcall + nil 兜底。
-- ---------------------------------------------------------------------------
local function loadPeer(name)
  local ok, mod = pcall(require, name)
  if ok and type(mod) == 'table' then return mod end
  pcall(function()
    log('W', 'startride', '[StartRide] 玩法模块 ' .. tostring(name)
      .. ' 加载失败: ' .. tostring(mod))
  end)
  return nil
end

local SRInput = loadPeer('startrideInput')
local SRMode  = loadPeer('startrideMode')
local SRSpawn = loadPeer('startrideSpawn')
local SRHud   = loadPeer('startrideModeHud')
-- 捉迷藏是独立模块：它的规则体量（角色/局内阶段/发现判定/出生规划）
-- 塞进 startrideMode 会把那个文件顶到 Lua 的 local 上限，所以自成一份。
local SRHide  = loadPeer('startrideHideSeek')

-- 本机已经为本回合广播过出生方案的回合号（一次性，不重算）
-- ── 出生的记账（全部塞进一个表）────────────────────────────────────────────
-- ⚠️ 为什么是一个表：和下面 srHide 完全同一个理由 —— 顶层 local 只剩个位数
--    余量，撞 200 就是整个扩展编译失败。这三个值本来就同属「出生记账」，
--    分开写只是历史原因。
local srSpawn = {
  plannedRound = -1,   -- 本机已广播过出生方案的回合
  localPlan    = nil,  -- 房主自己算出来的方案（自己收不到自己的广播）
  appliedRound = -1,   -- 本机已摆放过的回合（失败也不重试，见坑⑦）
}

-- ── 捉迷藏的桥接记账（全部塞进一个表）────────────────────────────────────────
-- ⚠️ 为什么是一个表而不是几个 local：
--    主模组顶层 local 已经顶到 Lua 5.1 的 **200 个上限**，撞线是**编译期**失败
--    （`too many local variables`），整个扩展不加载 —— 不是运行时报错。
--    之前在这里加了 6 个散装 local 就直接把模组编挂了，所以统一收进 srHide。
local srHide = {
  plannedRound = -1,   -- 本机已广播过出生方案的回合
  localPlan    = nil,  -- 房主自己算出来的方案（自己收不到自己的广播）
  appliedRound = -1,   -- 本机已摆放过的回合
  lastPhase    = '',   -- 上一次广播出去的「阶段/局内阶段」去重键
  lastFoundKey = '',   -- 上一次广播出去的发现进度去重键
}
-- 上一次广播给全房的阶段（变了才广播）
-- ⚠️ 收进表而不是加新的顶层 local：顶层 local 只剩个位数余量，
--    撞 200 是编译期失败。derbyDownKey 是「本机淘汰状态」的去重键。
local srCast = {
  phase    = '',
  derbyDown = '',   -- 本机「已被淘汰」是否已广播过（''/'0'/'1'）
}







local relayState = 'unknown'
local relayDetail = ''
local relayRoomId = ''
local statGameIn, statRelayOut, statRelayIn, statRelayVehicle = 0, 0, 0, 0
local remotePacketCount = 0
-- 被当成「自己发的」而丢掉的包数。这个数 >0 且远程车一直是 0，
-- 基本就能判定是两端 ID 撞了（重名玩家），而不是网络问题。
local selfPacketCount = 0
local selfPacketWarned = false
local spawnFailCount = 0
local spawnOkCount = 0
local lastSpawnError = ''
local lastSpawnTryAt = 0
local badPacketCount = 0
local skipByCollision = 0
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

-- ---------------------------------------------------------------------------
-- 屏幕上的一条简短提示（玩法里被拒绝的操作、出生失败原因等）
--
-- 走 BeamNG 自己的 ScenarioFlashMessage（就是场景里那种中上方短提示），
-- 拿不到就退回 ui_message，再不行就只写日志 —— 一层层兜底，绝不因为
-- 提示发不出去就把调用方打断。
-- ---------------------------------------------------------------------------
local srToastUntil = 0
local lastToastText = ''
local function srToast(text, seconds)
  local msg = tostring(text or '')
  if msg == '' then return end
  local secs = tonumber(seconds) or 3.0

  -- 简单去重：同一条消息 3 秒内不重复弹（按住键会连发）
  local clk = os.clock()
  if msg == lastToastText and clk < srToastUntil then return end
  lastToastText = msg
  srToastUntil = clk + 3.0

  local shown = false
  pcall(function()
    if guihooks ~= nil and guihooks.trigger ~= nil then
      guihooks.trigger('ScenarioFlashMessage', { msg = msg, seconds = secs })
      shown = true
    end
  end)
  if not shown then
    pcall(function()
      if ui_message ~= nil then ui_message(msg, secs, 'startride') end
    end)
  end
  logMsg('提示:', msg)
end




local function dropConnection(reason)
  if tcpSock then pcall(function() tcpSock:close() end) end
  tcpSock = nil
  connected = false
  connecting = false
  recvBuffer = ''
  -- ⚠️ 半帧残留必须清干净。如果长度前缀已经发出去、body 只发了一半就断开，
  -- 那半个 body 会留在对端缓冲里，被当成下一帧的长度前缀 → 后续所有帧错位，
  -- 表现就是持续刷 'A'/'Z'/'P'/'B'/'N' is an invalid start of a value。
  -- 清空本地出站缓冲、并让下一次连接从一条完整的 ready 重新开始对齐。
  outBuffer = ''
  sendOutDirty = true
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
  -- 刚重连上：先补一条完整 ready（用独立的一次写，确保它排在所有残帧之前），
  -- 对端一旦收到合法 ready，就知道帧边界从这里重新开始。
  if sendOutDirty then
    sendOutDirty = false
    local okR, encR = pcall(jsonEncode, { type = 'ready', name = playerName, version = MOD_VERSION })
    if okR and encR and #encR > 0 then
      outBuffer = ffi.string(ffi.new('uint32_t[1]', #encR), 4) .. encR .. outBuffer
    end
  end
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
    -- 非阻塞 connect 返回 1 只说明「这一步没报错」，仍要核对 SO_ERROR，
    -- 否则对端未监听时会被当成连上（connectTCP 与 checkConnection 必须同一套判据）。
    local sockErr = nil
    pcall(function()
      local so = tcpSock:getoption('error')
      if so ~= nil then sockErr = so end
    end)
    if sockErr == nil or sockErr == 0 then
      connected = true
      connecting = false
      connectRetryCount = 0
      connectBackoff = 1
      queuePacket({ type = 'ready', name = playerName, version = MOD_VERSION })
      logMsg('已连接启动器')
    else
      dropConnection('connect refused: ' .. tostring(sockErr))
      connectBackoff = math.min(connectBackoff * 2, MAX_CONNECT_BACKOFF)
    end
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
    -- ⚠️ 「可写」不等于「连上了」。非阻塞 connect 完成时，无论成功还是被拒，
    -- socket 都会变成可写；如果不查 SO_ERROR，对端根本没监听（启动器还没起桥）时
    -- 也会被判成连上 → 发 ready → send 失败 → dropConnection → 立刻重连，
    -- 形成高频震荡。内测诊断包里同一毫秒出现两条「游戏内模组已连接到启动器」
    -- 以及成片的 'A'/'Z'/'P'/'B'/'N' 解析失败，源头都在这里。
    local peerOk, peer = pcall(function() return tcpSock:getpeername() end)
    local sockErr = nil
    pcall(function()
      local so = tcpSock:getoption('error')
      if so ~= nil then sockErr = so end
    end)
    if peerOk and peer and (sockErr == nil or sockErr == 0) then
      connected = true
      connecting = false
      connectRetryCount = 0
      connectBackoff = 1
      queuePacket({ type = 'ready', name = playerName, version = MOD_VERSION })
      logMsg('已连接启动器 (async)')
    else
      dropConnection('async connect failed: ' .. tostring(sockErr))
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

local function queueVETypeAndID(veh, id)
  if not veh then return end
  pcall(function() veh.mpVehicleType = 'R' end)
  pcall(function() veh:queueLuaCommand("startrideVE.setVehicleType('R')") end)
  pcall(function()
    veh:queueLuaCommand('startrideVE.setServerID(' .. string.format('%q', tostring(id)) .. ')')
  end)
end


--[[
  生成一台远程玩家的车。

  ⚠️ 这里的错误绝不能再咽掉。旧版把 pcall 的成功/失败和返回值一起判掉后直接
  return nil，于是「spawnVehicle 返回了 nil」和「根本没收到对方的数据包」在日志
  里长得一模一样，线上排查只能靠猜。现在把三个信息都记下来：
    · err  —— pcall 捕获到的 Lua 错误串（真抛异常时才有）
    · rv   —— spawnVehicle 的返回值（被调用但返回 nil 时能确认它被调过）
    · 以及调用时的 model / pos，方便回放现场
  每台车只在第一次尝试时详细记一次（detail=true），重试时只报短句，避免刷屏。
]]
local function trySpawn(model, cfg, pos, rot, id, exact, detail)
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

  -- 失败：把真实原因留下，别让日志骗人
  local why
  if not ok then
    why = 'spawnVehicle 抛错: ' .. tostring(veh)
  elseif veh == nil then
    why = 'spawnVehicle 返回 nil（车型缺失/配置非法/位置不可用）'
  else
    why = 'spawnVehicle 返回非车辆对象: ' .. tostring(veh)
  end
  lastSpawnError = tostring(model) .. ' → ' .. why
  if detail then
    logMsg('远程车生成失败', tostring(model), 'exact=' .. tostring(exact == true),
      'pos=' .. string.format('%.1f,%.1f,%.1f', pos.x, pos.y, pos.z),
      'name=' .. tostring(opts.vehicleName), '|', why)
  end
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

  local veh = trySpawn(model, data.cfg, pos, rot, id, false, true)
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

  if not id then return end

  -- 自己发的包要丢掉，但**不能默默丢**：
  -- 早先这里直接 return，于是「重名导致双方 ID 相同、互相丢弃」这种故障
  -- 在日志和面板里完全看不出来（远程车包一直是 0，像网络断了一样）。
  -- 现在记一笔，并且只在第一次提醒，免得 30Hz 刷屏。
  if id == myId() then
    selfPacketCount = selfPacketCount + 1
    if not selfPacketWarned then
      selfPacketWarned = true
      logMsg('收到自己的车辆包，已丢弃（本机 ID=' .. tostring(id) .. '）')
    end
    return
  end

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

    queueVETypeAndID(frec.veh, found)
    logMsg('远程车 VE 就绪:', tostring(found))
  else
    logMsg('VE 就绪上报(暂未匹配到车辆):', tostring(gameVehicleID))
  end
end

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

    local rx, ry, rz = v[1] or 0, v[2] or 0, v[3] or 0
    if not noCounter then
      local rotVel = localVel:rotated(delta)
      rx, ry, rz = rx - rotVel.x, ry - rotVel.y, rz - rotVel.z
    end
    veh:applyClusterVelocityScaleAdd(refNode, 1, rx, ry, rz)

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

--[[
  联机诊断快照。给玩家/客服一键导出用，也是离线自检的读取口
  （模组内部状态全是 local，外部脚本读不到，必须由模组自己吐出来）。

  selfPacketCount > 0 且 remotePacketCount == 0 是最有价值的判据：
  说明收到了对方的包、但被自己的 ID 过滤掉了 —— 两端 ID 撞了（重名玩家）。
  早先这条路径是完全静默的，日志里只有「远程车包=0」，像网络没通一样。
]]
function M.diagnose()
  return {
    version    = MOD_VERSION,
    myId       = myId(),
    playerName = playerName,
    connected  = connected and true or false,
    roomId     = relayRoomId,
    roomHost   = roomInfo.host,
    roomCount  = roomInfo.count,
    localMap   = localMap,
    peerMap    = peerMap,

    remotePacketCount = remotePacketCount,
    selfPacketCount   = selfPacketCount,
    badPacketCount    = badPacketCount,
    spawnOkCount      = spawnOkCount,
    spawnFailCount    = spawnFailCount,
    lastSpawnError    = lastSpawnError,
    remoteCount       = countRemote(),
    veReadyCount      = veReadyCount,

    -- 明确给出结论，免得上层还要自己推
    idCollision       = (selfPacketCount > 0 and remotePacketCount == 0),
    verdict           = (function()
      if not connected then return '没连上启动器本地桥（127.0.0.1:4444）' end
      if relayRoomId == '' then return '没拿到房间号，中继可能没进房成功' end
      if selfPacketCount > 0 and remotePacketCount == 0 then
        return '两端联机 ID 相同，互相丢弃了对方的包（多半是重名玩家）——改昵称或重启启动器'
      end
      if remotePacketCount == 0 then return '没收到任何对方的车辆包（对方可能没进图/没装模组）' end
      if spawnOkCount == 0 then return '收到了对方的包但车没生成成功，看 lastSpawnError' end
      return 'normal'
    end)(),
  }
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




-- ── 房间配置：地图 + 出生点 ───────────────────────────────────────────────────
-- 地图走启动器命令行（-level <id>），游戏开出来就已经在房主那张图上了；
-- 这里只管出生点：把本机选的那个 scenetree 对象找出来，把本地车放上去。
-- 出生点刻意不走中继：房主落哪里跟别人无关，每个人各选各的（同一张图、不同落点）。
local spawnPlan = {
  map = '',
  spawnPoint = '',
  applied = false,
  warned = false,
  mapWarned = false,
  -- 只在「游戏本来开着、又不在房间地图上」时用一次：请游戏自己切图
  mapSwitchTried = false,
}

-- ── 玩法模式（全房共享）───────────────────────────────────────────────────────
-- 玩法是「这局怎么玩」的宣告，由房主选定、随房间元数据广播，每个人的值都一样。
-- 模组侧只做两件事：
--   ① 把收到的玩法记下来，显示在面板上（让玩家知道自己在玩什么）
--   ② 按玩法调整自己的**既有**行为（目前只有一条：捉迷藏要求玩家分散出生）
-- 规则主体（抓人判定、计分、回合）不在这里实现 —— 那属于各玩法自己的逻辑，
-- 这一版先把「玩法能被宣告、能被看到、能影响出生策略」这条链路打通。
-- 认不出来的 id 一律当自由驾驶，绝不因为对端用了新玩法就报错。
local GAME_MODES = {
  free_drive = { title = '自由驾驶', spawnsApart = false },
  hide_seek  = { title = '捉迷藏',   spawnsApart = true  },
  cops_robber= { title = '警察抓强盗', spawnsApart = true },
  derby      = { title = '德比',     spawnsApart = false },
  track_day  = { title = '赛道日',   spawnsApart = true  },
}

local function gameModeInfo(id)
  local hit = GAME_MODES[tostring(id or '')]
  if hit then return hit end
  return GAME_MODES.free_drive
end

local function setGameMode(id)
  local mode = tostring(id or '')
  if mode == '' then mode = 'free_drive' end
  if mode == roomInfo.gameMode then return end

  local wasKnown = GAME_MODES[roomInfo.gameMode] ~= nil
  roomInfo.gameMode = mode

  if wasKnown or roomInfo.gameMode == 'free_drive' then
    logMsg('房间玩法已设定: ' .. gameModeInfo(mode).title .. '  (' .. tostring(mode) .. ')')
    table.insert(chat, { name = 'system', text = '本房玩法：' .. gameModeInfo(mode).title })
    chatScrollToBottom = true
  end
end

local function setSpawnPlan(map, spawnPoint, source)  spawnPlan.map = tostring(map or '')
  spawnPlan.spawnPoint = tostring(spawnPoint or '')
  spawnPlan.applied = false
  spawnPlan.warned = false
  spawnPlan.mapWarned = false
  spawnPlan.mapSwitchTried = false
  if spawnPlan.spawnPoint ~= '' then
    logMsg('出生点已设定: ' .. spawnPlan.spawnPoint .. '  (' .. tostring(source) .. ')')
  end
end

-- 车顶悬浮名牌的运行状态：enabled / maxDistance。
--
-- ⚠️ 必须声明在 readBootstrapConfig **之前**！
--    Lua 的函数体只能引用「在函数定义之前就已声明」的 local。原来这个表写在
--    1871 行（readBootstrapConfig 定义在 1009 行之后方），于是 1031 行的
--    `nameTag.enabled = ...` 编译成了**全局访问** → 运行时 nameTag 是 nil →
--    每次 onExtensionLoaded 都抛
--      `attempt to index global 'nameTag' (a nil value)`
--    而且启动器写进 multiplayer.json 的名牌开关**从来没生效过**。
--    实测证据：beamng.log 里 `job error: [string ".../startride.lua"]:1026`。
local nameTag = { enabled = true, maxDistance = 300 }

-- 启动器在游戏启动前把本房信息写到 userpath 下的 startride/multiplayer.json，
-- 扩展刚加载、本地桥还没连上时先靠它垫一层，后面的桥消息负责刷新。
local function readBootstrapConfig()
  local cfg = nil
  pcall(function() cfg = jsonReadFile('startride/multiplayer.json') end)
  if type(cfg) ~= 'table' then
    pcall(function() cfg = jsonReadFile('current/startride/multiplayer.json') end)
  end
  if type(cfg) ~= 'table' then return false end

  if cfg.playerName and cfg.playerName ~= '' then playerName = cfg.playerName end
  if cfg.roomName and cfg.roomName ~= '' then
    roomInfo.name = cfg.roomName
  elseif cfg.roomId then
    roomInfo.name = cfg.roomId
  end
  if (cfg.map and cfg.map ~= '') or (cfg.spawnPoint and cfg.spawnPoint ~= '') then
    setSpawnPlan(cfg.map, cfg.spawnPoint, 'multiplayer.json')
  end
  if cfg.gameMode and cfg.gameMode ~= '' then
    setGameMode(cfg.gameMode)
  end
  -- 车顶悬浮名牌（昵称 + 距离）：启动器设置页写进来的开关和最远距离。
  if type(cfg.nameTagEnabled) == 'boolean' then
    nameTag.enabled = cfg.nameTagEnabled
  end
  local nmd = tonumber(cfg.nameTagMaxDistance)
  if nmd and nmd > 20 then
    nameTag.maxDistance = nmd
  end
  return true
end

-- 本机当前加载的关卡 id（getMissionFilename 形如 /levels/west_coast_usa/info.json）
local function currentLevelId()
  local f = ''
  pcall(function() f = getMissionFilename() or '' end)
  local id = tostring(f):match('levels/([^/]+)/')
  if id and id ~= '' then return id end
  return ''
end

-- 游戏已经开着、而且确实停在**别的**图上时，请游戏自己切过去。
-- 用引擎自己的 core_loadMapCmd（lua/ge/extensions/core/loadMapCmd.lua）：
-- 它就是命令行 -level 走的那条路 —— 内部会等 mod manager 就绪。
--
-- ⚠️⚠️ 这里以前只看 currentLevelId() ~= 目标图 就切，成了「进地图闪退」的成因：
--   引擎侧 freeroam_freeroam.startFreeroam 只在 scenetree.MissionGroup 存在、
--   或 core_gamestate 已登记在加载时才会延迟、避免「边加载边拆」，其余情况直接
--   startFreeroamHelper → core_levels.startLevel，同时 endActiveGameMode 拆掉
--   正在加载的那张图 —— 加载中的地图被拆就是死在加载界面。
--   而 -level 这条路**本来就已经**调过一次 core_loadMapCmd.set，模组再调一次 =
--   在同一次加载里叠第二次加载。所以必须同时满足三个条件才切：
--     ① 已经在某个关卡里（currentLevelId 非空，即不在主菜单/加载中）
--     ② 不在加载过程中（core_gamestate 没在加载）
--     ③ 当前图确实不是目标图
--   任何一条不满足就等下一帧，绝不抢先调 core_loadMapCmd。
local function isLoadingNow()
  local b = false
  pcall(function()
    -- getLoadingStatus(tag) 只认「这个 tag 自己有没有申请过」，传陌生 tag 永远 nil；
    -- 真正表示「引擎现在正在加载」的是 core_gamestate.loading()。
    b = core_gamestate ~= nil and core_gamestate.loading ~= nil
        and core_gamestate.loading() == true
  end)
  return b == true
end

-- 是否已经有加载动作在管线里。
-- ⚠️ core_gamestate.getLoadingStatus(tag) 只认「这个 tag 自己有没有申请过」，
--    传一个陌生 tag 永远返回 nil，不能用；core_loadMapCmd 的 args 又是模块 local，
--    外部摸不到。所以只能靠下面这两个公开信号。
local function mapLoadPending()
  local pending = false
  pcall(function()
    -- ① mod manager 还没就绪 = 引擎那条 -level 还卡着等挂载，绝对不能插队
    if core_modmanager and core_modmanager.isReady and not core_modmanager.isReady() then
      pending = true
      return
    end
    -- ② 正在加载中 = 已经在拆/建图，插一脚就是「边加载边拆」
    if isLoadingNow() then
      pending = true
    end
  end)
  return pending
end

local function ensureMapLoaded()
  if spawnPlan.map == '' or spawnPlan.mapSwitchTried then return end

  local cur = currentLevelId()
  if cur == '' then return end            -- 还没进任何图（主菜单 / 还在加载），等下一帧
  if string.lower(cur) == string.lower(spawnPlan.map) then
    spawnPlan.mapSwitchTried = true       -- 已经在对的图上，不用切
    return
  end

  -- 目标图在本机存不存在？不存在就别调 —— core_loadMapCmd 只会打一句
  -- "map not found, you may need to add a mod" 然后什么都不做，白赌一次。
  local exists = false
  pcall(function() exists = FS:directoryExists('levels/' .. spawnPlan.map) end)
  if not exists then
    if not spawnPlan.mapWarned then
      spawnPlan.mapWarned = true
      logMsg('本机没有房间地图 ' .. spawnPlan.map .. '（多半是房主的模组地图），留在当前图 ' .. cur)
      table.insert(chat, { name = 'system', text = '本机缺少房间地图「' .. spawnPlan.map .. '」，已留在当前地图' })
      chatScrollToBottom = true
    end
    spawnPlan.mapSwitchTried = true
    return
  end

  -- ⚠️ 加载管线里已经有活就先别插队（见上面那段注释）
  if mapLoadPending() then return end

  -- 走到这里：已经在别的图上、图也有、也没在加载 —— 这才是真的需要换图
  spawnPlan.mapSwitchTried = true
  spawnPlan.applied = false               -- 换图之后要重新落地

  local ok = false
  pcall(function()
    extensions.load('core_loadMapCmd')
    core_loadMapCmd.set({ level = 'levels/' .. spawnPlan.map .. '/info.json' }, true)
    ok = true
  end)

  if ok then
    logMsg('游戏已在运行且不在这张图上，正在切到 ' .. spawnPlan.map)
    table.insert(chat, { name = 'system', text = '正在进入房间地图「' .. spawnPlan.map .. '」' })
    chatScrollToBottom = true
  else
    logMsg('切换地图失败（core_loadMapCmd 不可用），留在当前地图 ' .. cur)
  end
end

-- 把本地车放到选定的出生点上。
-- 落位姿势照抄游戏自己的「大图快速旅行」(core/levels.lua getSpawnPointPosRot + bigMapMode)：
--   rot = quat(0,0,1,0) * obj:getRotation()，再 spawn.safeTeleport(veh, pos, rot)。
-- safeTeleport 内部还会再乘一次 quat(0,0,1,0)，两次 180° 相互抵消，
-- 所以最终朝向 = 出生点对象自己的朝向，和游戏里快速旅行过去一模一样。
local function spawnTick()
  if spawnPlan.spawnPoint == '' and spawnPlan.map == '' then return end

  safeCall('ensureMapLoaded', ensureMapLoaded)
  if spawnPlan.applied or spawnPlan.spawnPoint == '' then return end

  -- 地图没对上就先别动：这会儿出生点对象还不存在，
  -- 硬找只会误报「本图没有这个出生点」并把机会用掉。
  if spawnPlan.map ~= '' then
    local cur = currentLevelId()
    if cur == '' or string.lower(cur) ~= string.lower(spawnPlan.map) then
      return
    end
  end

  local veh = be:getPlayerVehicle(0)
  if not veh then return end

  local obj = nil
  pcall(function() obj = scenetree.findObject(spawnPlan.spawnPoint) end)
  if not obj then
    spawnPlan.applied = true
    if not spawnPlan.warned then
      spawnPlan.warned = true
      logMsg('出生点 ' .. spawnPlan.spawnPoint .. ' 在本图里不存在，按默认位置落地')
      table.insert(chat, { name = 'system', text = '本图没有出生点「' .. spawnPlan.spawnPoint .. '」，已按默认位置出生' })
      chatScrollToBottom = true
    end
    return
  end

  local pos, rot = nil, nil
  pcall(function()
    pos = obj:getPosition()
    rot = quat(0, 0, 1, 0) * obj:getRotation()
  end)
  if not pos or not rot then return end

  local ok = false
  pcall(function()
    if spawn and spawn.safeTeleport then
      spawn.safeTeleport(veh, pos, rot)
      ok = true
    end
  end)
  if not ok then
    -- safeTeleport 不可用时的兜底：直接摆位姿，至少别停在原地
    pcall(function() veh:setPosRot(pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w) end)
  end

  spawnPlan.applied = true
  logMsg('已落到出生点 ' .. spawnPlan.spawnPoint .. (ok and '' or '（safeTeleport 不可用，用了兜底定位）'))
  table.insert(chat, { name = 'system', text = '已在「' .. spawnPlan.spawnPoint .. '」出生' })
  chatScrollToBottom = true
end

local function onPacket(data)
  if type(data) ~= 'table' or not data.type then return end

  if data.type == 'vehicle' then
    if SRMode then pcall(SRMode.notePositions, data) end
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

  
  
  elseif data.type == 'room-config' then
    setSpawnPlan(data.map, data.spawnPoint, '启动器')
    if data.gameMode and data.gameMode ~= '' then setGameMode(data.gameMode) end

  elseif data.type == 'game-mode' then
    if data.gameMode and data.gameMode ~= '' then setGameMode(data.gameMode) end

  elseif data.type == 'mode-phase' then
    -- 房主推阶段：加入者照跟（自己在权威端就不听）
    if SRMode then
      SRMode.applyRemotePhase(data.phase, data.round, data.winner)
    end
    -- 捉迷藏是独立状态机，阶段也要喂一份（否则它的 HUD / 输入策略永远停在 lobby）
    if SRHide then
      SRHide.applyRemotePhase(data.phase, data.round, data.winner)
    end

  elseif data.type == 'hide-seek-stage' then
    -- 房主推「躲藏期 / 搜索期」（running 内部阶段）
    if SRHide then SRHide.applyRemoteStage(data.stage) end

  elseif data.type == 'hide-seek-found' then
    -- 房主推发现进度 + 已找到名单（加入者本地不做判定）
    if SRHide then
      SRHide.applyRemoteFound(data.target, data.heldMs, data.found)
      SRHide.applyRemoteFoundList(data.found, data.roles)
    end

  elseif data.type == 'derby-down' then
    -- 远端玩家报告「我被撞毁了」：房主替他记账，否则回合永远结算不了
    if SRMode then
      local pid = tostring(data.id or data.player or '')
      -- 没带 id 时退回用昵称匹配（两端 playerName 是同一个来源，够用）
      if pid == '' and data.playerName then pid = tostring(data.playerName) end
      SRMode.noteRemoteEliminated(pid)
    end

  elseif data.type == 'mode-spawn-plan' then
    -- 房主广播的出生方案：加入者收下，交给摆放层应用
    if SRMode then SRMode.applySpawnPlan(data) end

  elseif data.type == 'mode-placement' then
    -- 远端玩家汇报「我已就位」。只有房主需要汇总；加入者不用管。
    if SRMode and SRMode.isAuthority() then
      local round = tonumber(data.round) or -1
      if round == tonumber(SRMode.round()) then
        SRMode.notePlacement(tostring(data.id or ''), data.ready ~= false)
      end
    end

  elseif data.type == 'relay-state' then
    relayState = data.state or 'unknown'
    relayDetail = data.detail or ''
    if data.roomId then relayRoomId = data.roomId end

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

local HL = {
  active = false,
  sessionId = '',
  startedWall = '',
  duration = 0,
  events = {},
  shots = 0,
  shotBusy = false,
  shotBusyUntil = 0,
  replays = {},
  curReplay = '',
  curReplayFrom = 0,
  ownRecording = false,

  recElapsed = 0,
  lastLine = '',
  toastUntil = 0,
  lastTick = 0,
  lastPos = 0,
  enabled = true,
  autoRecord = true,
  segmentSeconds = 300,
  maxShots = 40,
  onlyInSession = true,
  dir = '',
  dirReady = false,
  cfgLoaded = false,
  sessionMap = '',
  savedAt = 0,
}

local AUTOSAVE_INTERVAL = 30

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

local function hlReplayState()
  local st = nil
  pcall(function() st = core_replay.getState() end)
  if type(st) ~= 'table' then return nil end
  return st
end

local function hlStartRecording()
  local st = hlReplayState()
  if st and st.state == 'recording' then
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
  local pos = HL.recElapsed or 0
  local seg = HL.replays[#HL.replays]
  if seg then seg.to = HL.curReplayFrom + pos end
  pcall(function() core_replay.stopRecording() end)
  HL.ownRecording = false
  logMsg('高光：录制已停止并保存', HL.curReplay, '时长', string.format('%.1fs', pos))
end

local function hlRotateIfNeeded()
  if not HL.ownRecording then return end
  local st = hlReplayState()
  if not st or st.state ~= 'recording' then return end

  local pos = HL.recElapsed or 0
  HL.lastPos = pos
  if pos < HL.segmentSeconds then return end

  local seg = HL.replays[#HL.replays]
  if seg then seg.to = HL.curReplayFrom + pos end
  HL.curReplayFrom = HL.curReplayFrom + pos
  pcall(function() core_replay.stopRecording() end)
  HL.ownRecording = false
  hlStartRecording()
  logMsg('高光：录制已分段，本段', string.format('%.0fs', pos))
end

local function hlTakeShot(typ)
  if HL.shots >= HL.maxShots then return nil end
  if HL.shotBusy and os.clock() < HL.shotBusyUntil then return nil end
  local dir = hlDir()
  if not dir then return nil end

  HL.shots = HL.shots + 1
  local base = string.format('%s/hl-%s-%02d', dir, HL.sessionId, HL.shots)
  HL.shotBusy = true
  HL.shotBusyUntil = os.clock() + 2.0
  local ok = pcall(function() screenshot.doScreenshot(nil, nil, base, 'jpg') end)
  if not ok then
    HL.shotBusy = false
    HL.shots = HL.shots - 1
    return nil
  end
  return 'hl-' .. HL.sessionId .. string.format('-%02d', HL.shots) .. '.jpg'
end

local function hlSave(reason)
  local dir = hlDir()
  if not dir or HL.sessionId == '' then return end

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

function M.onHighlight(typ, value, extra, speed)
  pcall(function()
    if type(typ) ~= 'string' then return end
    value = tonumber(value) or 0
    extra = tonumber(extra) or 0
    speed = tonumber(speed) or 0

    pcall(function()
      if gameplay_statistic and gameplay_statistic.metricAdd then
        gameplay_statistic.metricAdd('startride/highlight/' .. typ, 1)
      end
    end)

    local capturing = HL.active or not HL.onlyInSession
    local line = hlLabel(typ) .. ' ' .. hlValueText(typ, value, extra)

    HL.lastLine = line
    HL.toastUntil = os.clock() + 5

    if not capturing then

      logMsg('高光(未记录)：' .. line)
      return
    end

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

local function hlTick(dtSim, dtReal)
  if HL.duration then HL.duration = HL.duration + (dtSim or 0) end

  if HL.ownRecording then HL.recElapsed = (HL.recElapsed or 0) + (dtSim or 0) end
  if HL.shotBusy and os.clock() > HL.shotBusyUntil then HL.shotBusy = false end

  if not HL.cfgLoaded then
    HL.cfgLoaded = true
    pcall(hlLoadConfig)
  end

  local live = (relayState == 'connected') and not roomInfo.closed
  local always = (HL.onlyInSession == false) and (localMap ~= '')
  local want = live or always

  if want and HL.active and HL.sessionMap ~= '' and localMap ~= ''
     and localMap ~= HL.sessionMap then
    pcall(hlEndSession, 'map-change')
  end
  if want and not HL.active then
    pcall(hlStartSession)
  elseif (not want) and HL.active then
    pcall(hlEndSession, live and 'disconnect' or 'left-level')
  end

  HL.lastTick = HL.lastTick + (dtReal or 0)
  if HL.lastTick < 1.0 then return end
  HL.lastTick = 0
  if not HL.active then return end
  if HL.ownRecording then pcall(hlRotateIfNeeded) end

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

local CONFLICT_PATTERNS = { 'beamlink', 'beammp', 'carpool', 'beamng-mp' }
local conflictList = {}
local conflictKey = ''
local nextConflictCheck = 0

local function conflictScan()
  local found, seen = {}, {}

  local function hit(text)
    local lk = string.lower(tostring(text))
    if string.find(lk, 'startride', 1, true) then return nil end
    for _, pat in ipairs(CONFLICT_PATTERNS) do
      if string.find(lk, pat, 1, true) then return pat end
    end
    return nil
  end

  local function push(name)
    local s = tostring(name)
    local key = string.lower(s)
    if s == '' or seen[key] then return end
    seen[key] = true
    found[#found + 1] = s
  end

  pcall(function()
    for key in pairs(extensions) do
      if hit(key) then push('扩展 ' .. tostring(key)) end
    end
  end)

  pcall(function()
    local function walk(t, depth)
      if type(t) ~= 'table' or depth > 2 then return end
      for k, v in pairs(t) do
        if type(v) == 'table' then
          local nm = v.modname or v.modName or v.name or v.title or v.filename or v.modId
          if nm ~= nil then
            if v.active ~= false and v.enabled ~= false and hit(nm) then push(nm) end
          else
            walk(v, depth + 1)
          end
        elseif depth == 0 and type(k) == 'string' then
          if hit(k) then push(k) end
        end
      end
    end
    walk(jsonReadFile('mods/db.json'), 0)
  end)

  return found
end

local function conflictTick()
  if timeAccum < nextConflictCheck then return end
  nextConflictCheck = timeAccum + 10

  local list = conflictScan()
  local key = table.concat(list, '|')
  if key == conflictKey then return end
  conflictKey = key
  conflictList = list

  if #list > 0 then
    local text = table.concat(list, '、')
    logMsg('检测到第三方联机模组:', text,
      '—— 它可能接管或删除我们生成的远程车；联机时 mods 里请只留一套联机模组')
    table.insert(chat, {
      name = 'system',
      text = '检测到第三方联机模组：' .. text
        .. '。请把它从 mods 目录移出后重启游戏，否则远程车辆可能被它接管、抖动或反复消失。',
    })
    chatScrollToBottom = true
  else
    logMsg('第三方联机模组检测：干净')
  end
end

-- ============================================================
--  玩家车顶悬浮名牌（昵称 + 距离）
--
--  世界坐标 → 屏幕坐标投影后，在 ImGui 前景绘制层上画一张圆角深色小卡。
--  只要对方在画面里、且在 nameTag.maxDistance 以内就会画；越远越淡。
--
--  ⚠️ Lua 5.1 每个函数最多 60 个 upvalue，超了是**编译期静默失败**（整个扩展不加载）。
--     所以所有可调参数一律收进 TUNE，函数里只引用 TUNE 这一个上值。
-- ============================================================
local TUNE = {
  liftBase   = 1.6,   -- 名牌基准高度（米）：车顶再往上抬这么多
  padX       = 7,     -- 卡片左右内边距
  padY       = 4,     -- 卡片上下内边距
  lineGap    = 2,     -- 昵称与距离两行之间的间距
  rounding   = 5,     -- 卡片圆角
  barW       = 3,     -- 左侧身份色条宽度
  dotR       = 2.5,   -- 身份色圆点半径
  fadeStart  = 0.55,  -- 从 maxDistance 的这个比例开始淡出
  minAlpha   = 0.10,  -- 最远处的最低不透明度
  maxTags    = 16,    -- 单帧最多画几个（兜底，防极端情况刷屏）
  fontScale  = 0.86,  -- 卡片内字号缩放
}

-- 名牌运行时状态 local 已上移到 readBootstrapConfig 之前（见那里的说明）——
-- 放在这里会被编译成全局访问，导致名牌开关失效并且每次加载抛 job error。

local function nameTagFontScale()
  local ok, scaled = pcall(function()
    -- ImGui 有多套隐式缩放重载，名字各不相同，逐个试。
    im.SetWindowFontScale(TUNE.fontScale)
    return true
  end)
  return ok and scaled or false
end

-- 世界坐标 → 屏幕像素。返回 nil 表示这个点在相机背后或出画面。
-- 参照 BeamLink 的做法：用相机的 pos/right/forward/up + 半视场角 + 宽高比做点积投影。
local function nameTagProject(px, py, pz)
  local cam = nil
  pcall(function() cam = core_camera.getPosition() end)
  if not cam then return nil end

  -- 相机朝向：优先取四元数自己算 right/forward/up，拿不到就退回位置差。
  local fx, fy, fz, rx, ry, rz, ux, uy, uz = nil, nil, nil, nil, nil, nil, nil, nil, nil
  pcall(function()
    local q = core_camera.getQuat()
    if not q then return end
    local qx, qy, qz, qw = q.x, q.y, q.z, q.w
    -- forward = q * (0,1,0)
    fx = 2 * (qx * qy + qw * qz)
    fy = 1 - 2 * (qx * qx + qz * qz)
    fz = 2 * (qy * qz - qw * qx)
    -- up = q * (0,0,1)
    ux = 2 * (qx * qz - qw * qy)
    uy = 2 * (qy * qz + qw * qx)
    uz = 1 - 2 * (qx * qx + qy * qy)
    -- right = q * (1,0,0)
    rx = 1 - 2 * (qy * qy + qz * qz)
    ry = 2 * (qx * qy - qw * qz)
    rz = 2 * (qx * qz + qw * qy)
  end)
  if not fx then return nil end

  local halfTan, aspect = nil, nil
  pcall(function()
    halfTan = math.tan((core_camera.getFovRad() or 1.0) * 0.5)
  end)
  pcall(function()
    local w, h = im.GetWindowClientSizeXY()
    if w and h and h > 0 then aspect = w / h end
  end)
  if not halfTan or halfTan <= 0 then return nil end
  if not aspect or aspect <= 0 then return nil end

  local vx, vy, vz = px - cam.x, py - cam.y, pz - cam.z
  local depth = vx * fx + vy * fy + vz * fz
  if depth <= 0.2 then return nil end

  local ndcX = (vx * rx + vy * ry + vz * rz) / (depth * halfTan * aspect)
  local ndcY = (vx * ux + vy * uy + vz * uz) / (depth * halfTan)
  if ndcX < -1.15 or ndcX > 1.15 or ndcY < -1.15 or ndcY > 1.15 then return nil end

  local vpL, vpT, vpW, vpH = 0, 0, 1920, 1080
  pcall(function()
    local vp = im.GetMainViewport()
    if vp then
      vpL, vpT = vp.Pos.x, vp.Pos.y
      vpW, vpH = vp.Size.x, vp.Size.y
    end
  end)
  if vpW <= 0 or vpH <= 0 then return nil end

  return vpL + (0.5 + 0.5 * ndcX) * vpW, vpT + (0.5 - 0.5 * ndcY) * vpH
end

-- 车顶世界坐标（车体包围盒中心 + 抬升），失败则退回车辆原点。
local function nameTagAnchor(veh)
  local x, y, z = nil, nil, nil
  pcall(function()
    local oobb = veh:getSpawnWorldOOBB()
    if oobb then
      local c = oobb:getCenter()
      if c then x, y, z = c.x, c.y, c.z end
    end
  end)
  if not x then
    pcall(function()
      local p = veh:getPosition()
      if p then x, y, z = p.x, p.y, p.z end
    end)
  end
  if not x then return nil end

  local h = nil
  pcall(function() h = tonumber(veh:getInitialHeight()) end)
  local lift = TUNE.liftBase + (h and h * 0.5 or 0.6)
  return x, y, z + lift
end

local function drawNameTags()
  if not (nameTag.enabled and canDraw and initialized) then return end
  local pv = getPlayerVeh()
  if not pv then return end

  local myPos = nil
  pcall(function() myPos = pv:getPosition() end)
  if not myPos then return end

  local camPos = nil
  pcall(function() camPos = core_camera.getPosition() end)

  local maxD = nameTag.maxDistance
  local fadeFrom = maxD * TUNE.fadeStart

  -- 先收集再排序（远的先画、近的盖在上面），避免大车压住小车的名牌。
  local list = {}
  for _, rec in pairs(remoteVehicles) do
    if rec.veh then
      local ok, ax, ay, az = pcall(nameTagAnchor, rec.veh)
      if ok and ax then
        local dx, dy, dz = ax - myPos.x, ay - myPos.y, az - myPos.z
        local dist = math.sqrt(dx * dx + dy * dy + dz * dz)
        if dist <= maxD then
          local sx, sy = nameTagProject(ax, ay, az)
          if sx then
            list[#list + 1] = { name = rec.name or '?', dist = dist, sx = sx, sy = sy }
          end
        end
      end
    end
  end
  local n = #list
  if n == 0 then return end
  if n > TUNE.maxTags then n = TUNE.maxTags end
  table.sort(list, function(a, b) return a.dist > b.dist end)

  local opened = false
  local ok, err = pcall(function()
    im.SetNextWindowPos(im.ImVec2(0, 0), im.Cond_Always)
    local vw, vh = 1920, 1080
    pcall(function()
      local vp = im.GetMainViewport()
      if vp then vw, vh = vp.Size.x, vp.Size.y end
    end)
    im.SetNextWindowSize(im.ImVec2(vw, vh), im.Cond_Always)
    if not im.Begin('##sr_nametag',
        im.BoolPtr(false),
        im.WindowFlags_NoTitleBar + im.WindowFlags_NoResize + im.WindowFlags_NoScrollbar +
        im.WindowFlags_NoMove + im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings +
        im.WindowFlags_NoInputs + im.WindowFlags_NoFocusOnAppearing + im.WindowFlags_NoBringToFrontOnFocus +
        im.WindowFlags_NoBackground) then
      im.End()
      return
    end
    opened = true

    local dl = fnGetDrawList()
    local scaled = nameTagFontScale()

    for i = 1, n do
      local t = list[i]
      -- 距离渐隐
      local alpha = 1
      if t.dist > fadeFrom then
        alpha = 1 - (t.dist - fadeFrom) / (maxD - fadeFrom)
        if alpha < TUNE.minAlpha then alpha = TUNE.minAlpha end
      end

      local nc = nameColor(t.name)
      local l1 = t.name
      local l2 = string.format('%d m', math.floor(t.dist + 0.5))
      local w1 = im.CalcTextSize(l1).x
      local w2 = im.CalcTextSize(l2).x
      local lineH = im.GetTextLineHeight()
      local innerW = (w1 > w2) and w1 or w2
      local cardW = innerW + TUNE.padX * 2 + TUNE.barW + TUNE.dotR * 2 + 6
      local cardH = lineH * 2 + TUNE.lineGap + TUNE.padY * 2

      local x0 = t.sx - cardW * 0.5
      local y0 = t.sy - cardH
      local x1, y1 = x0 + cardW, t.sy

      local bg = im.ImVec4(0.06, 0.09, 0.13, 0.82 * alpha)
      fnAddRectFilled(dl, im.ImVec2(x0, y0), im.ImVec2(x1, y1), colorU32(bg), TUNE.rounding, nil)
      -- 左侧身份色条（用该玩家昵称哈希出来的颜色，和玩家列表里一致）
      local bar = im.ImVec4(nc.x, nc.y, nc.z, alpha)
      fnAddRectFilled(dl, im.ImVec2(x0, y0), im.ImVec2(x0 + TUNE.barW, y1), colorU32(bar), TUNE.rounding, nil)
      -- 身份色圆点
      if fnAddCircleFilled then
        fnAddCircleFilled(dl, im.ImVec2(x0 + TUNE.barW + 4 + TUNE.dotR, y0 + TUNE.padY + lineH * 0.5),
          TUNE.dotR, colorU32(bar), 16)
      end

      -- 文字用窗口绘制（TextColored 会跟随字体缩放；前景绘制层拿不到缩放后的字体）
      local tx = x0 + TUNE.barW + 4 + TUNE.dotR * 2 + 4
      im.SetCursorScreenPos(im.ImVec2(tx, y0 + TUNE.padY))
      im.TextColored(im.ImVec4(C.text.x, C.text.y, C.text.z, alpha), l1)
      im.SetCursorScreenPos(im.ImVec2(tx, y0 + TUNE.padY + lineH + TUNE.lineGap))
      im.TextColored(im.ImVec4(C.dim.x, C.dim.y, C.dim.z, alpha), l2)
    end

    if scaled then pcall(function() im.SetWindowFontScale(1.0) end) end
  end)

  if opened then pcall(function() im.End() end) end
  if not ok then logMsg('名牌绘制出错:', tostring(err)) end
end

local function drawHUD()
  local conflict = #conflictList > 0
  -- ⚠️ 尺寸必须在这里定义：这两个值原来是裸的全局 W / H（永远是 nil），
  -- 实机每帧报 "bad argument #2 to 'ImVec2' (number expected, got nil)" 刷屏。
  -- 宽度与其它 HUD 窗（聊天 440 / 玩家列表 268）风格一致；
  -- 高度按内容行数给：常规 7 行，出现第三方模组告警时多 2 行。
  local W = 320
  local H = conflict and 196 or 176
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
    im.SameLine()
    im.TextColored(C.dim, '  ' .. gameModeInfo(roomInfo.gameMode).title)

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

    if conflict then
      local names = table.concat(conflictList, '、')
      if #names > 28 then names = string.sub(names, 1, 28) .. '…' end
      im.Dummy(im.ImVec2(10, 4))
      im.SameLine()
      im.TextColored(C.danger, '检测到第三方联机模组')
      im.Dummy(im.ImVec2(10, 4))
      im.SameLine()
      im.TextColored(C.danger, '  ' .. names .. '  请移出 mods 后重启')
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
  kvRow('玩法', gameModeInfo(roomInfo.gameMode).title)
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
  if remotePacketCount > 0 and spawnOkCount == 0 then
    im.TextColored(C.danger, '· 收到了对方的包但一辆车都没生成出来 —— 看下面那行原因')
  end
  
  kvRow('车辆扩展就绪', tostring(veReadyCount), veReadyCount > 0 and C.ok or C.warn)
  kvRow('碰撞保护跳过修正', tostring(skipByCollision), C.dim)
  
  
  
  kvRow('丢弃脏数据包', tostring(badPacketCount), badPacketCount > 0 and C.warn or C.ok)
  kvRow('被当成自己的包丢弃', tostring(selfPacketCount),
    selfPacketCount > 0 and C.danger or C.dim)
  if selfPacketCount > 0 and remotePacketCount == 0 then
    im.TextColored(C.danger, '· 两端联机 ID 撞了（多半是重名）。')
    im.TextColored(C.danger, '  改一个不同的昵称，或重启启动器重新进房。')
  end
  kvRow('幽灵车辆待清理', tostring(#pendingCleanup), #pendingCleanup > 0 and C.danger or C.ok)
  if lastSpawnError ~= '' then
    im.TextColored(C.warn, '· ' .. lastSpawnError)
  end

  if #conflictList > 0 then
    im.Dummy(im.ImVec2(0, 8))
    sectionTitle('第三方联机模组（冲突）')
    im.Separator()
    for _, nm in ipairs(conflictList) do
      im.TextColored(C.danger, '· ' .. tostring(nm))
    end
    im.TextColored(C.danger, '它会接管或删除远程车（抖动 / 反复消失）。')
    im.TextColored(C.danger, '请从 mods 目录移出后重启游戏。')
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

  -- 玩法中的复位键（Ins）：所有模式都过阶段机的预算/冷却闸口。
  -- ⚠️ 这里**不**做「是否允许」的判断 —— 判断只在 SRMode.requestReset 里，
  --    本地再判一遍就会跟阶段机不一致（比如德比的冷却）。
  --    被拒时把原因原样报给玩家，而不是静默失败。
  do
    local ins = false
    pcall(function() ins = im.IsKeyPressed(im.Key_Insert) end)
    if not ins and im.Key_Delete ~= nil then
      pcall(function() ins = im.IsKeyPressed(im.Key_Delete) end)
    end
    if ins and not wantText then
      if SRMode then
        local ok, reason = SRMode.requestReset()
        -- requestReset 返回 (true, '') 表示放行 → 交给游戏自己处理复位；
        -- 返回 (false, 原因) 表示拦下 → 提示原因。
        if ok == false and reason and reason ~= '' then
          srToast(reason)
        elseif type(ok) == 'string' and ok ~= '' then
          srToast(ok)
        end
      end
    end
  end

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




-- ============================================================
-- 玩法出生规划 / 玩法 HUD 的桥接
-- ============================================================

-- 玩法出生规划：本机是规划者 + 本回合还没出方案 → 算一次，广播给全房。
-- 方案带上 round 号，加入者收到后按 round 去重应用。

-- 诊断钩子：供启动器诊断面板 / 自动化测试直接驱动摆放层。
-- 生产中不改变任何行为，只是把内部函数暴露出来方便调用。
-- ===========================================================================
-- 玩法出生（规划 + 摆放）—— 薄桥层
--
-- 真正的实现在 startrideSpawn.lua 里（proposal / pickMySlot / placeSelf /
-- buildModePlan / applyModePlan）。搬过去的原因：
--   ① 它本来就是「出生」这件事的一部分，放那边更合理；
--   ② 主模组顶层 local 必须给它让位（Lua 5.1 每块 200 个活跃 local，
--      撞线是编译期 `too many local variables`，整个扩展静默不加载）。
--
-- ⚠️ 下面这 4 个函数名**不能改**：onUpdateRaw 里用
--    safeCall('srModeSpawnApply', applyModeSpawnPlan) 这样按名传入，
--    名字会写进日志，改了就丢诊断信息。
-- ===========================================================================

local function syncModeSpawnPlan()
  if not (SRMode and SRSpawn) then return end
  SRSpawn.buildModePlan(SRMode)
end

local function syncHideSeekPlan()
  if not SRHide then return end
  SRSpawn.buildHideSeekPlan(SRHide)
end

local function applyModeSpawnPlan()
  if not (SRMode and SRSpawn) then return end
  SRSpawn.applyModePlan(SRMode)
end

local function applyHideSeekPlan()
  if not (SRHide and SRSpawn) then return end
  SRSpawn.applyModePlan(SRHide)
end

function M.srDebugPlaceSelf(slot, mode, round)
  if not SRSpawn then return nil end
  -- 兜底：正常路径下 onExtensionLoaded 已经绑过了；但自动化测试会跳过
  -- onExtensionLoaded 直接调本函数，所以这里再绑一次（幂等）。
  if SRSpawn.bindBridge then pcall(SRSpawn.bindBridge, M) end
  return SRSpawn.placeSelf(slot, mode, round)
end

function M.srDebugApplyPlan()
  applyModeSpawnPlan()
end

function M.srDebugSetName(nm)
  if type(nm) == 'string' and nm ~= '' then playerName = nm end
end

function M.srDebugSetMode(md)
  if type(md) == 'string' and md ~= '' then roomInfo.gameMode = md end
end

function M.srDebugSpawnState()
  local st = (SRSpawn and SRSpawn.placeState) and SRSpawn.placeState() or {}
  return {
    phase = SRMode and SRMode.phase() or '?',
    mode  = SRMode and SRMode.mode() or '?',
    selfId = (function()
      local id = myId()
      if id == nil or id == '' then return 'self' end
      return tostring(id)
    end)(),
    name = playerName,
    level = currentLevelId(),
    appliedRound = st.appliedRound or srSpawn.appliedRound,
    hasLocalPlan = srSpawn.localPlan ~= nil,
  }
end

-- ---------------------------------------------------------------------------
-- 阶段音效 / 提示
--
-- 每个客户端都按**本机看到的阶段**放提示（房主加入者都走这里），
-- 用 (phase, round) 做去重键，避免每帧重复放。
-- 音效走 BeamNG 自己的 event（拿不到就静默跳过，不影响玩法）。
-- ---------------------------------------------------------------------------
local srAnnouncedKey = ''
local function announcePhaseCues()
  -- 捉迷藏用的是独立状态机：它的阶段也要走到这里放提示/音效。
  -- 两边互斥（同一个房间只可能是一种玩法），挑「当前玩法对应的那台状态机」。
  local isHide = (SRHide ~= nil) and (roomInfo.gameMode == 'hide_seek')
  if isHide then
    if not SRHide then return end
  else
    if not SRMode then return end
  end

  local ph, round
  if isHide then
    ph = SRHide.phase()
    round = SRHide.round()
  else
    ph = SRMode.phase()
    round = SRMode.round()
  end
  local key = tostring(round) .. ':' .. tostring(ph)
  if key == srAnnouncedKey then return end

  -- lobby / finished 也要记，否则刚好在 lobby 起局时会把 lobby 又播一遍
  local prevKey = srAnnouncedKey
  srAnnouncedKey = key
  if prevKey == '' then return end        -- 首帧只是记一下，不放

  local function playEvent(ev)
    pcall(function()
      if core_sound ~= nil and core_sound.playEvent ~= nil then
        core_sound.playEvent(ev)
      end
    end)
  end

  if ph == 'staging' then
    srToast('准备就绪，正在分配出生位置…', 4)
    playEvent('event:>UI>Missions>Info_Open')
  elseif ph == 'countdown' then
    playEvent('event:UI_CountdownGo')
  elseif ph == 'running' then
    if SRMode and SRMode.mode() == 'cops_robber' then
      local role = SRMode.selfRole()
      if role == 'robber' then
        srToast('你是强盗 —— 跑！', 3)
      elseif role == 'cop' then
        srToast('你是警察 —— 追！', 3)
      else
        srToast('开始！', 2)
      end
    elseif SRMode and SRMode.mode() == 'derby' then
      srToast('德比开始 —— 撞垮他们！', 3)
    elseif SRHide and SRHide.mode() == 'hide_seek' then
      -- 捉迷藏：说清楚「现在该干什么」——躲藏者先跑，搜索者被冻住
      if SRHide.selfRole() == 'hider' then
        srToast('你是躲藏者 —— 快找地方藏起来！', 4)
      elseif SRHide.selfRole() == 'seeker' then
        srToast('你是搜索者 —— 躲藏期你不能动车，等放行', 4)
      else
        srToast('捉迷藏开始！', 3)
      end
    else
      srToast('开始！', 2)
    end
    playEvent('event:UI_CountdownGo')
  elseif ph == 'finished' then
    local snap = isHide and SRHide.snapshot() or SRMode.snapshot()
    if isHide and snap then
      -- 捉迷藏：把 winner 翻译成人话（seeker/hider 是内部 id）
      local who = (snap.winner == 'seeker' and '搜索方')
               or (snap.winner == 'hider' and '躲藏方') or ''
      if who ~= '' then
        srToast('回合结束 —— ' .. who .. '获胜（找到 '
          .. tostring(snap.foundCount or 0) .. ' 人）', 6)
      else
        srToast('回合结束', 5)
      end
    elseif snap and snap.winner and snap.winner ~= '' then
      srToast('回合结束 —— 胜者：' .. tostring(snap.winner), 6)
    else
      srToast('回合结束', 5)
    end
    playEvent('event:UI_Checkpoint')
  end
end

-- ---------------------------------------------------------------------------
-- 给子模块的窄接口（只读观察 + 发包）
--
-- ⚠️ 为什么要「借」而不是让子模块自己去 require：
--    startrideSpawn.lua 里的出生驱动层需要 roomInfo 的实时内容、以及发包能力；
--    子模块拿不到主模组的 upvalue，所以主模组主动把「读值」和「发包」
--    这两个动作以函数形式交出去。这样才能把整层搬到子模块里。
--
-- ⚠️ 这些访问器必须定义在 onExtensionLoaded 之前（见下方 M.* 导出），
--    因为 onExtensionLoaded 里会调 SRSpawn.bindBridge(M)，
--    而子模块在**调用时**才去读 mod.currentLevelId —— 导出晚于 bindBridge
--    会导致 "attempt to call a nil value (field 'currentLevelId')"。
--
-- ⚠️ 为什么不写成 4 个 `local function xxxRef()`：顶层 local 额度只剩个位数
--    （见下方 srSpawn / srHide 两处说明），所以统一收进一个表：
--    表本身只占 1 个 local。
-- ---------------------------------------------------------------------------
local srRef = {
  roomInfo       = function() return roomInfo end,
  myId           = function() return myId() end,
  playerName     = function() return playerName end,
  currentLevelId = function() return currentLevelId() end,
}

-- 玩法 HUD：把阶段机的快照 + 雷达几何算好后交给 HUD 模块画。
local function drawModeHud()
  if not SRHud then return end

  -- 捉迷藏走 SRHide 的快照，其余玩法走 SRMode 的。
  -- 两边的 mode() 都取自同一个 roomInfo.gameMode，所以不会打架。
  local snap, extra = nil, { winnerName = '' }
  local isHide = (SRHide ~= nil) and (SRHide.mode() == 'hide_seek')
  if isHide then
    snap = SRHide.snapshot()
  elseif SRMode then
    snap = SRMode.snapshot()
  end
  if type(snap) ~= 'table' then return end

  -- 结算停留时长（HUD 用来做淡入淡出）
  if isHide then
    snap.finishAgeMs = SRHide.phaseAgeMs()
  elseif SRMode then
    snap.finishAgeMs = SRMode.phaseAgeMs()
  else
    snap.finishAgeMs = 0
  end

  -- 警察雷达：只算 XY 平面距离与方位角
  if snap.mode == 'cops_robber' and snap.role == 'cop' and snap.phase == 'running' then
    local dist, bearing = SRMode.robberBearing()
    snap.robberDist = dist
    snap.robberBearing = bearing
  end

  -- 强盗端：最近的警察威胁（距离/方位/强度/已被贴住多久）
  if snap.mode == 'cops_robber' and snap.role == 'robber' then
    snap.threat = SRMode.robberThreat()
  end

  SRHud.render(snap, extra)
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
  safeCall('spawnTick', spawnTick)
  safeCall('hlTick', hlTick, dtSim, dtReal)
  safeCall('conflictTick', conflictTick)

  -- 玩法：阶段机先跑（算出本帧的输入限制意图），输入层再按意图下发。
  -- 顺序不能反 —— 反了输入限制永远慢一帧。
  if SRMode then safeCall('srModeTick', SRMode.tick, dtReal, {
    gameMode = roomInfo.gameMode,
    connected = connected,
    relayState = relayState,
    host = roomInfo.host,
    selfId = myId(),
    playerName = playerName,
    players = mpPlayers,
    closed = roomInfo.closed,
  }) end
  if SRInput then
    local pol = nil
    if SRMode then pol = SRMode.inputPolicy() end
    if type(pol) == 'table' then
      safeCall('srInputPolicy', SRInput.setPolicy, pol)
    else
      safeCall('srInputPolicy', SRInput.setPolicy, { roundActive = false, running = false })
    end
    safeCall('srInputTick', SRInput.tick, dtReal)
  end

  -- 捉迷藏：独立状态机先跑（它自己判角色/阶段/发现），输入策略随后覆盖。
  -- ⚠️ 顺序：警匪的 inputPolicy 先下发，捉迷藏再覆盖 —— 两者互斥（mode 不同），
  --    但同一帧里先设后覆盖能保证「不论先后都不会残留上一帧的策略」。
  if SRHide then
    safeCall('srHideTick', SRHide.tick, dtReal, {
      gameMode = roomInfo.gameMode,
      connected = connected,
      relayState = relayState,
      host = roomInfo.host,
      selfId = myId(),
      playerName = playerName,
      players = mpPlayers,
      closed = roomInfo.closed,
    })
    if roomInfo.gameMode == 'hide_seek' and SRInput then
      local hpol = SRHide.inputPolicy()
      if type(hpol) == 'table' then
        safeCall('srInputPolicyHide', SRInput.setPolicy, hpol)
      end
    end
  end

  -- 玩法出生布局：本机是规划者、且本回合还没出方案时，算一次并广播。
  -- 规划是「一次性」的，算完缓存；后续快照不会重算（见 startrideSpawn 坑⑥）。
  if SRMode and SRSpawn then
    safeCall('srModeSpawnPlan', syncModeSpawnPlan)
  end
  if SRHide then
    safeCall('srHideSpawnPlan', syncHideSeekPlan)
  end

  -- 玩法出生摆放：把方案里属于**我自己**的那个槽位落到车上。
  -- 必须在规划之后 —— 同一帧房主先算出方案，紧接着就能用上。
  if SRMode then
    safeCall('srModeSpawnApply', applyModeSpawnPlan)
  end
  if SRHide then
    safeCall('srHideSpawnApply', applyHideSeekPlan)
  end

  -- 房主把阶段推给全房（只在阶段真的变了时发一次）
  if SRMode and SRMode.isAuthority() then
    local ph = SRMode.phase()
    if ph ~= srCast.phase then
      srCast.phase = ph
      safeCall('srModePhaseCast', function()
        queuePacket({ type = 'mode-phase', phase = ph, round = SRMode.round(),
                      winner = SRMode.snapshot().winner })
        flushOut()
      end)
    end
  end

  -- 德比：本机被撞毁 → 广播一次（房主与加入者都要发）
  --
  -- 为什么要发：房主靠「roster 里只剩 1 人」结算，而 S.eliminated 原本只写本机
  -- 那一格 → 房主永远不知道别人被撞了 → **回合永远不结束**。这里补上。
  -- 去重键只在本机状态真的翻转时变，所以一局只发一两个包。
  if SRMode and roomInfo.gameMode == 'derby' then
    safeCall('srDerbyDownCast', function()
      local k = SRMode.eliminatedKey and SRMode.eliminatedKey() or '0'
      if k == '1' and k ~= srCast.derbyDown then
        queuePacket({ type = 'derby-down', id = myId(), playerName = playerName })
        flushOut()
      end
      srCast.derbyDown = k
    end)
  else
    srCast.derbyDown = ''
  end

  -- 捉迷藏：房主推「阶段 + 局内阶段 + 发现进度」
  -- 三个阶段用一个 srHide.lastPhase 记，避免重复包；发现进度用
  -- target:foundCount:秒数 当 key，进度每变 0.1 秒才发一次（不逐帧刷）。
  if SRHide and SRHide.isAuthority() and roomInfo.gameMode == 'hide_seek' then
    safeCall('srHideCast', function()
      local ph = SRHide.phase()
      local st = SRHide.stage()
      local key = ph .. '/' .. tostring(st)
      if key ~= srHide.lastPhase then
        srHide.lastPhase = key
        local snap = SRHide.snapshot()
        queuePacket({ type = 'mode-phase', phase = ph, round = SRHide.round(),
                      winner = snap.winner })
        if ph == 'running' then
          queuePacket({ type = 'hide-seek-stage', stage = st })
        end
        flushOut()
      end

      -- 发现进度：只在搜索期、且真的有进度时发
      if ph == 'running' and st == 'seek' then
        local snap = SRHide.snapshot()
        local foundList = {}
        for pid in pairs(SRHide.found() or {}) do foundList[#foundList + 1] = pid end
        table.sort(foundList)
        local fkey = tostring(snap.findTarget or '') .. ':'
          .. tostring(#foundList) .. ':'
          .. string.format('%.1f', (tonumber(snap.findMs) or 0) / 100)
        if fkey ~= srHide.lastFoundKey then
          srHide.lastFoundKey = fkey
          queuePacket({
            type = 'hide-seek-found',
            target = snap.findTarget,
            heldMs = snap.findMs,
            found = foundList,
            roles = snap.roles,
          })
          flushOut()
        end
      end
    end)
  end

  -- 阶段音效 / 提示：每个客户端各自按「本机看到的阶段」放，房主加入者都走这里。
  -- 用 srAnnounced 去重，避免帧里反复放。
  if SRMode then
    safeCall('srModeAnnounce', announcePhaseCues)
  end

  handleKeys()
  if panelOpen[0] then drawPanel() end
  if hudOn[0] then drawHUD() end
  if playerListOpen[0] then drawPlayerList() end
  if chatOpen[0] then drawChat() end
  safeCall('drawNameTags', drawNameTags)

  -- 玩法 HUD（角色卡 / 抓捕条 / 雷达 / 结算画面）
  if SRMode and SRHud then safeCall('srModeHud', drawModeHud) end
end




-- ===========================================================================
-- 导出给 startrideSpawn.lua 的出生驱动层（bindBridge 之后它按名回调这些）
--
-- ⚠️ 必须写在 onExtensionLoaded **之前**：onExtensionLoaded 里会
--    SRSpawn.bindBridge(M)，子模块随后调 mod.currentLevelId()。
--    顺序写反 → "attempt to call a nil value (field 'currentLevelId')"
--    → 出生摆放被静默跳过。
-- ===========================================================================
M.roomInfo       = srRef.roomInfo
M.myId           = srRef.myId
M.playerName     = srRef.playerName
M.currentLevelId = srRef.currentLevelId
M.queuePacket    = queuePacket
M.flushOut       = flushOut
M.logMsg         = logMsg

function M.onExtensionLoaded()
  logMsg('GE 扩展 v' .. MOD_VERSION .. ' 已加载')
  initialized = true
  readBootstrapConfig()
  logMsg('玩家昵称:', playerName)

  -- 立刻把「出生的驱动层」绑上：loadPeer 可能因为打包问题返回 nil，
  -- 所以这里必须容错（pcall + 判空），不能让它把整个加载流程带崩。
  if SRSpawn and SRSpawn.bindBridge then
    pcall(SRSpawn.bindBridge, M)
  end

  safeCall('conflictTick', conflictTick)
  connectTCP()
end

function M.onInit()
  pcall(function() setExtensionUnloadMode(M, 'manual') end)
end

function M.onUpdate(dtReal, dtSim, dtRaw)
  pcall(onUpdateRaw, dtReal, dtSim, dtRaw)
end

function M.onExtensionUnloaded()
  pcall(hlEndSession, 'unload')
  -- 卸载时一定要放行按键并还原地图传送猴补丁，否则玩家回到自由驾驶后
  -- 传送/复位永久失效（BeamLink 踩过这个坑）。
  if SRInput then pcall(SRInput.disableAll) end
  for _, rec in pairs(remoteVehicles) do despawnRemote(rec) end
  remoteVehicles = {}
  dropConnection('扩展卸载')
  initialized = false
end

return M
