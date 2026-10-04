-- ============================================================================
-- StartRide 出生规划器
-- ============================================================================
-- 两种玩法各有一段：
--   警匪追逐 —— 公路走廊：在一条天然直路上前后拉开，强盗在前、警车在后成列。
--   德比撞车 —— 环形围场：绕一个中心点均匀排一圈，车头朝圆心。
--
-- 走廊算法思路参考 BeamLink 的 chaseSpawn.lua（公开源码），**但按 StartRide
-- 的桥协议重写**，并且绕开了它踩过的坑：
--
--   坑①  map.nodes 的边只挂在其中一个端点上。
--        必须自己建**双向邻接**，否则「往前走两步」会在半边图上断掉。
--   坑②  「两个连接」才算走廊。
--        路口（3+ 连接）上摆车会让警车从岔路直接截住强盗，必须排除。
--        而且要按**过滤后的**可用连接数算，不是原始 link 数。
--   坑③  确定性打乱必须真的混进种子。
--        只把种子写进 cacheKey 是假打乱 —— 同一个房间每回合还是同一条走廊。
--        step 要和候选数互质，否则遍历会撞回起点，走不到后半段候选。
--   坑④  走廊公平性要整体校验，任何一项不过就整条走廊作废。
--        否则会出现「警察背对强盗」「两车相距 3000 米」这种没法玩的局。
--   坑⑤  safeTeleport 之后的位移容差。
--        BeamLink 通用判据是 24 米，警匪放宽到 3 米。规划器已经把高度算好了，
--        所以要传 applyGroundCorrection = false，否则引擎会二次贴地、把车挪走。
--   坑⑥  摆位是「一次性」的，不能被后续快照反复触发。
--        用 round:role:gameId:layoutKey:坐标 当 key 去重；已经完成的摆位
--        不允许被新快照取消，否则玩家会在 ready/unready 之间反复横跳。
--
-- 上值控制：可调常量收进 TUNE。
-- ============================================================================

local M = {}

-- ---------------------------------------------------------------------------
-- 可调常量
-- ---------------------------------------------------------------------------
local TUNE = {
  -- 走廊筛选
  nodeRadiusMin    = 3.25,
  drivabilityMin   = 0.7,
  minSegmentLen    = 5,
  maxGrade         = 0.25,
  walkSteps        = 95,

  -- 角色间距
  robberBase       = 140,   -- 强盗在前 140 米
  robberPerCop     = 10,    -- 每多一个警察 +10 米（上限 +60）
  robberMaxBonus   = 60,
  copBase          = 18,    -- 第 1 辆警车在后 18 米
  copStride        = 18,    -- 后续每辆 +18 米

  -- 公平性
  nearGapMin       = 100,   -- 近端前后间距下限
  farGapMin        = 120,   -- 远端前后间距下限
  minRobberCopDist = 100,   -- 强盗与警车直线距离下限
  maxRobberCopDist = 1200,  -- 上限
  minCarGap        = 10,    -- 任意两车至少隔 10 米
  minDotForward    = 0.5,   -- 强盗车头与警车车头同向度
  minAheadDist     = 20,    -- 强盗必须在警车前方 20 米以上（沿警车车头方向）

  -- 贴地
  surfaceTol       = 2.25,
  rideHeight       = 0.35,

  -- 德比围场
  derbyRadius      = 22,    -- 环形半径（米）
  derbyMinRadius   = 12,
  derbyMaxRadius   = 60,
}

-- 缓存（key = mapId:baseSeed:ordinal:seed）
local layouts = {}
local sentKey = nil
local lastDiag = {}
-- ⚠️ 这三个必须和上面三个一起声明在 **M.reset() 之前**。
--    原来它们声明在 725 行（M.reset 在 677 行之后方），于是 M.reset 里那三行赋值
--    被编译成**全局写入** —— `placed = {}` 写的是全局 placed，真正的 local 防抖表
--    从来没被清过。reset 看起来"执行了"其实什么也没做（静默失效）。
local placed = {}           -- 防抖表：键 = round : mode : 车id : 坐标
local lastPlacedRound = -1  -- 防抖表当前对应的回合
local appliedRound = -1     -- 本机已摆放过的回合

-- ---------------------------------------------------------------------------
-- 工具
-- ---------------------------------------------------------------------------
local function clamp(v, lo, hi)
  if v < lo then return lo end
  if v > hi then return hi end
  return v
end

local function gcd(a, b)
  while b ~= 0 do a, b = b, a % b end
  return a
end

local function num(v)
  return tonumber(v) or 0
end

-- vec3 构造：优先用引擎的 vec3，取不到就用表（只在本文件内部比较时不调方法）
local function V(x, y, z)
  if vec3 ~= nil then
    local ok, v = pcall(vec3, x, y, z)
    if ok and v then return v end
  end
  return { x = x, y = y, z = z, __plain = true }
end

local function distXY(a, b)
  local dx, dy = (a.x or 0) - (b.x or 0), (a.y or 0) - (b.y or 0)
  return math.sqrt(dx * dx + dy * dy)
end

local function dist3(a, b)
  if a.distance ~= nil then
    local ok, d = pcall(function() return a:distance(b) end)
    if ok and d then return d end
  end
  local dx = (a.x or 0) - (b.x or 0)
  local dy = (a.y or 0) - (b.y or 0)
  local dz = (a.z or 0) - (b.z or 0)
  return math.sqrt(dx * dx + dy * dy + dz * dz)
end

local function dot3(a, b)
  if a.dot ~= nil then
    local ok, d = pcall(function() return a:dot(b) end)
    if ok and d then return d end
  end
  return (a.x or 0) * (b.x or 0) + (a.y or 0) * (b.y or 0) + (a.z or 0) * (b.z or 0)
end

-- ---------------------------------------------------------------------------
-- 地图数据准备
-- ---------------------------------------------------------------------------
local function mapNodes()
  if not map or map.getMap == nil then return nil end
  pcall(function() if map.load then map.load() end end)
  local ok, mapData = pcall(function() return map.getMap() end)
  if not ok or type(mapData) ~= 'table' then return nil end
  return mapData.nodes
end

local function usableRoadLink(first, second, link)
  if type(first) ~= 'table' or type(second) ~= 'table'
     or type(first.pos) ~= 'table' or type(second.pos) ~= 'table'
     or type(link) ~= 'table' then
    return false
  end
  local dx = num(second.pos.x) - num(first.pos.x)
  local dy = num(second.pos.y) - num(first.pos.y)
  local horizontal = math.sqrt(dx * dx + dy * dy)
  local grade = horizontal > 0.1
    and math.abs(num(second.pos.z) - num(first.pos.z)) / horizontal or 1

  return num(first.radius) >= TUNE.nodeRadiusMin
     and num(second.radius) >= TUNE.nodeRadiusMin
     and num(link.drivability) >= TUNE.drivabilityMin
     and tostring(link.type or '') ~= 'private'
     and link.oneWay ~= true
     and horizontal >= TUNE.minSegmentLen
     and grade <= TUNE.maxGrade
end

-- 建双向邻接（坑①）
local function buildAdjacency(nodes)
  local adj = {}
  for nodeId in pairs(nodes) do adj[nodeId] = {} end
  for nodeId, node in pairs(nodes) do
    for neighborId, link in pairs(node.links or {}) do
      if usableRoadLink(node, nodes[neighborId], link) then
        adj[nodeId][neighborId] = link
        if adj[neighborId] then adj[neighborId][nodeId] = link end
      end
    end
  end
  return adj
end

-- ---------------------------------------------------------------------------
-- 走廊候选（坑②：只挑正好两个可用连接的节点）
-- ---------------------------------------------------------------------------
local function collectCandidates(nodes, adj)
  local out = {}
  for nodeId, node in pairs(nodes) do
    if type(node.pos) == 'table' then
      local neighbors = {}
      for neighborId in pairs(adj[nodeId] or {}) do
        local nb = nodes[neighborId]
        if type(nb) == 'table' and type(nb.pos) == 'table'
           and usableRoadLink(node, nb, adj[nodeId][neighborId]) then
          neighbors[#neighbors + 1] = neighborId
        end
      end
      if #neighbors == 2 then
        table.sort(neighbors, function(a, b) return tostring(a) < tostring(b) end)
        for _, pick in ipairs(neighbors) do
          local back = (neighbors[1] == pick) and neighbors[2] or neighbors[1]
          out[#out + 1] = {
            sortKey = tostring(nodeId) .. ':' .. tostring(pick),
            n1 = nodeId,
            n2 = pick,
            back = back,
          }
        end
      end
    end
  end
  table.sort(out, function(a, b) return a.sortKey < b.sortKey end)
  return out
end

-- ---------------------------------------------------------------------------
-- 沿路走 distance 米
-- 返回 position, direction, prevId, curId, routeNodes
-- ---------------------------------------------------------------------------
local function makeWalker(nodes, adj, seed)
  return function(startId, nextId, distance, salt)
    local previousId, currentId = startId, nextId
    local previousPos = V(num(nodes[previousId].pos.x), num(nodes[previousId].pos.y),
                          num(nodes[previousId].pos.z))
    local remaining = math.max(0, num(distance))
    local edgeVisited = {}
    local nodeVisited = { [tostring(startId)] = true }
    local routeNodes = { startId }

    for step = 0, TUNE.walkSteps do
      local currentNode = nodes[currentId]
      if type(currentNode) ~= 'table' or type(currentNode.pos) ~= 'table' then return nil end
      if nodeVisited[tostring(currentId)] then return nil end
      nodeVisited[tostring(currentId)] = true
      routeNodes[#routeNodes + 1] = currentId

      local currentPos = V(num(currentNode.pos.x), num(currentNode.pos.y), num(currentNode.pos.z))
      local dx = currentPos.x - previousPos.x
      local dy = currentPos.y - previousPos.y
      local dz = currentPos.z - previousPos.z
      local segLen = math.sqrt(dx * dx + dy * dy + dz * dz)

      if segLen > 0.1 then
        local ux, uy, uz = dx / segLen, dy / segLen, dz / segLen
        if remaining <= segLen then
          local pos = V(previousPos.x + ux * remaining,
                        previousPos.y + uy * remaining,
                        previousPos.z + uz * remaining)
          return pos, V(ux, uy, uz), previousId, currentId, routeNodes
        end
        remaining = remaining - segLen
      end

      edgeVisited[tostring(previousId) .. '>' .. tostring(currentId)] = true

      local options = {}
      for optionId in pairs(adj[currentId] or {}) do
        local optionNode = nodes[optionId]
        if optionId ~= previousId
           and not nodeVisited[tostring(optionId)]
           and not edgeVisited[tostring(currentId) .. '>' .. tostring(optionId)]
           and type(optionNode) == 'table' and type(optionNode.pos) == 'table'
           and usableRoadLink(currentNode, optionNode, adj[currentId][optionId]) then
          options[#options + 1] = optionId
        end
      end
      if #options == 0 then return nil end
      table.sort(options, function(a, b) return tostring(a) < tostring(b) end)
      local pick = ((seed + (salt or 0) + step * 131) % #options) + 1
      previousId, currentId = currentId, options[pick]
      previousPos = currentPos
    end
    return nil
  end
end

local function routesDisjoint(routeA, routeB, sharedNodeId)
  local occupied = {}
  local shared = tostring(sharedNodeId)
  for _, nodeId in ipairs(routeA or {}) do
    local key = tostring(nodeId)
    if key ~= shared then occupied[key] = true end
  end
  for _, nodeId in ipairs(routeB or {}) do
    local key = tostring(nodeId)
    if key ~= shared and occupied[key] then return false end
  end
  return true
end

-- ---------------------------------------------------------------------------
-- 车道偏移（把车放到合法车道的正中间）
-- ---------------------------------------------------------------------------
local function laneXnorm(nodes, position, node1Id, node2Id, roadDirection)
  local first, second = nodes[node1Id], nodes[node2Id]
  if type(first) ~= 'table' or type(second) ~= 'table' then return 0 end

  local firstPos, secondPos = first.pos, second.pos
  local dx = num(secondPos.x) - num(firstPos.x)
  local dy = num(secondPos.y) - num(firstPos.y)
  local len2 = dx * dx + dy * dy
  if len2 < 0.01 then return 0 end

  local t = ((num(position.x) - num(firstPos.x)) * dx
          + (num(position.y) - num(firstPos.y)) * dy) / len2
  t = clamp(t, 0, 1)

  local r1 = num(first.radius)
  local r2 = num(second.radius)
  local radius = (r2 ~= 0) and (r1 + (r2 - r1) * t) or r1
  local roadWidth = radius * 2
  local laneWidth = (roadWidth >= 6.1) and 3.05 or 2.2
  local laneCount = math.max(1, math.floor(roadWidth / laneWidth))
  if laneCount % 2 ~= 0 then laneCount = math.max(1, laneCount - 1) end
  if laneCount == 1 then return 0 end

  -- 靠哪边行驶：引擎的交通规则说了算
  local legalSide = 1
  pcall(function()
    local rules = map and map.getRoadRules and map:getRoadRules()
    if type(rules) == 'table' and rules.rightHandDrive then legalSide = -1 end
  end)

  return ((roadDirection < 0) and -1 or 1) * legalSide / laneCount
end

-- ---------------------------------------------------------------------------
-- 把「路上一个点」精修成可用的出生位（含贴地）
-- ---------------------------------------------------------------------------
local function finalizeSpawnPoint(routePos, routeDir, node1Id, node2Id, roadDirection)
  local pos = routePos

  -- 引擎的交通工具能给到更贴合路面的点；拿不到就用路线上的点
  pcall(function()
    if gameplay_traffic_trafficUtils
       and gameplay_traffic_trafficUtils.finalizeSpawnPoint ~= nil then
      local res = gameplay_traffic_trafficUtils.finalizeSpawnPoint(
        routePos, routeDir, node1Id, node2Id, {
          legalDirection = true,
          roadDir = roadDirection,
          roadLateralXnorm = TUNE._laneX or 0,
        })
      if res then pos = res end
    end
  end)

  pos = V(num(pos.x), num(pos.y), num(pos.z))

  -- 贴地：引擎地表高度和路线 z 差太多就判这条走廊不合法
  local surface = nil
  pcall(function()
    if be and be.getSurfaceHeightBelow then
      surface = be:getSurfaceHeightBelow(V(pos.x, pos.y, pos.z + 2))
    end
  end)
  if surface == nil then return nil, 'no_surface' end
  if math.abs(num(surface) - pos.z) > TUNE.surfaceTol then return nil, 'surface_mismatch' end
  pos.z = num(surface) + TUNE.rideHeight

  -- 方向取路方向（水平化）
  local dir = V(num(routeDir.x), num(routeDir.y), 0)
  local dlen = math.sqrt(dir.x * dir.x + dir.y * dir.y)
  if dlen < 0.01 then return nil, 'no_direction' end
  dir = V(dir.x / dlen, dir.y / dlen, 0)

  return pos, dir
end

-- ---------------------------------------------------------------------------
-- 警匪：解一条走廊
-- ---------------------------------------------------------------------------
local function resolveCorridor(nodes, adj, walk, option, copCount, diag)
  local function resolveRole(role, index)
    local targetDistance, roadDirection, startId, nextId, salt
    if role == 'robber' then
      local bonus = math.min(TUNE.robberMaxBonus, math.max(0, copCount - 1) * TUNE.robberPerCop)
      targetDistance = TUNE.robberBase + bonus
      roadDirection, startId, nextId, salt = 1, option.n1, option.n2, 17
    else
      targetDistance = TUNE.copBase + math.max(0, index) * TUNE.copStride
      roadDirection, startId, nextId, salt = -1, option.n1, option.back, 29
    end

    local routePos, routeDir, n1, n2, routeNodes =
      walk(startId, nextId, targetDistance, salt)
    if not routePos or not routeDir or not n1 or not n2 then
      diag.missing = diag.missing + 1
      return nil
    end

    TUNE._laneX = laneXnorm(nodes, routePos, n1, n2, roadDirection)
    local pos, dir = finalizeSpawnPoint(routePos, routeDir, n1, n2, roadDirection)
    TUNE._laneX = 0
    if not pos then diag.surface = diag.surface + 1; return nil end

    return {
      position = pos,
      direction = dir,
      routeNodes = routeNodes,
      routeDistance = targetDistance,
      roadDirection = roadDirection,
    }
  end

  -- 走廊本身的前后可用长度（先粗筛，省得逐个算角色位置）
  local forwardNear = walk(option.n1, option.n2, TUNE.robberBase, 17)
  local forwardFar = walk(option.n1, option.n2, 200, 17)
  local backwardNear = walk(option.n1, option.back, TUNE.copBase, 29)
  local _, _, _, _, backwardFarRoute = walk(option.n1, option.back, 126, 29)
  local _, _, _, _, forwardFarRoute = walk(option.n1, option.n2, 200, 17)

  if not (forwardNear and forwardFar and backwardNear and backwardFarRoute) then
    diag.corridorFail = (diag.corridorFail or 0) + 1
    return nil
  end
  if distXY(forwardNear, backwardNear) < TUNE.nearGapMin
     or distXY(forwardFar, walk(option.n1, option.back, 126, 29) or forwardFar) < TUNE.farGapMin
     or not routesDisjoint(forwardFarRoute, backwardFarRoute, option.n1) then
    diag.corridorFail = (diag.corridorFail or 0) + 1
    return nil
  end

  diag.corridor = (diag.corridor or 0) + 1

  local occupied = {}
  local placements = {}

  local function accept(key, placement)
    if not placement then return false end
    for _, prev in ipairs(occupied) do
      if dist3(placement.position, prev) < TUNE.minCarGap then
        diag.overlap = (diag.overlap or 0) + 1
        return false
      end
    end
    placements[key] = placement
    occupied[#occupied + 1] = placement.position
    return true
  end

  local robber = resolveRole('robber', 0)
  if not accept('robber:0', robber) then return nil end

  for copIndex = 0, copCount - 1 do
    local cop = resolveRole('cop', copIndex)
    if not cop
       or not routesDisjoint(robber.routeNodes, cop.routeNodes, option.n1)
       or dot3(robber.direction, cop.direction) < TUNE.minDotForward
       -- 强盗必须「在警车前方」：用警车车头方向去点
       or dot3(V(robber.position.x - cop.position.x,
                 robber.position.y - cop.position.y,
                 robber.position.z - cop.position.z), cop.direction) <= TUNE.minAheadDist
       or dist3(robber.position, cop.position) < TUNE.minRobberCopDist
       or dist3(robber.position, cop.position) > TUNE.maxRobberCopDist
       or not accept('cop:' .. tostring(copIndex), cop) then
      diag.geometry = (diag.geometry or 0) + 1
      return nil
    end
  end

  return placements
end

-- ---------------------------------------------------------------------------
-- 警匪：主入口
-- ---------------------------------------------------------------------------
local function buildCorridorLayout(state, mapId)
  local seed = math.max(1, math.floor(num(state.spawnLayoutSeed) or 1))
  local baseSeed = math.max(1, math.floor(num(state.spawnLayoutBaseSeed) or seed))
  local ordinal = math.max(1, math.floor(num(state.spawnLayoutOrdinal) or 1))
  local cacheKey = table.concat({ tostring(mapId), tostring(baseSeed),
                                  tostring(ordinal), tostring(seed) }, ':')
  if layouts[cacheKey] then return layouts[cacheKey] end

  local diag = { candidates = 0, corridor = 0, surface = 0, overlap = 0,
                 geometry = 0, missing = 0, corridorFail = 0 }
  lastDiag = diag

  local nodes = mapNodes()
  if not nodes then return nil end
  local adj = buildAdjacency(nodes)

  local candidates = collectCandidates(nodes, adj)
  if #candidates == 0 then return nil end
  diag.candidates = #candidates

  -- 确定性挑选（坑③）
  local mixed = (baseSeed + seed * 65537 + ordinal * 8191) % 2147483647
  local step = math.max(1, math.floor(mixed / #candidates) % #candidates)
  while gcd(step, #candidates) ~= 1 do
    step = step % #candidates + 1
  end
  local startIndex = (mixed % #candidates) + 1

  local walk = makeWalker(nodes, adj, seed)

  local copCount = 0
  for _, p in ipairs(state.players or {}) do
    if tostring(p.role or '') == 'cop' then copCount = copCount + 1 end
  end
  if copCount < 1 then copCount = 1 end

  for attempt = 0, #candidates - 1 do
    local index = ((startIndex - 1 + attempt * step) % #candidates) + 1
    local option = candidates[index]
    local placements = resolveCorridor(nodes, adj, walk, option, copCount, diag)
    if placements then
      local layout = {
        kind = 'corridor',
        n1 = option.n1, n2 = option.n2, back = option.back,
        placements = placements,
        diagnostic = diag,
      }
      layouts[cacheKey] = layout
      return layout
    end
  end

  return nil
end

-- ---------------------------------------------------------------------------
-- 德比：环形围场
--
-- BeamLink 没有客户端围场规划（它从服务端拿 player.spawnTarget），
-- 这一段是我们自己加的：找一个平坦的中央锚点，绕一圈均匀排车。
-- ---------------------------------------------------------------------------
local function buildDerbyArena(state, mapId)
  local cacheKey = 'derby:' .. tostring(mapId) .. ':' .. tostring(state.spawnLayoutOrdinal or 1)
  if layouts[cacheKey] then return layouts[cacheKey] end

  local diag = { candidates = 0, surface = 0, overlap = 0, geometry = 0 }
  lastDiag = diag

  -- 锚点：优先玩家当前位置（房主站在哪，围场就开在哪）
  local anchor = nil
  pcall(function()
    if be == nil then return end
    -- ⚠️ 别用 type(getter)=='function' 判方法存在：宿主对象的方法可能是 userdata，
    --    那样会把「能用」误判成「不可用」。只要非 nil 就试，异常由 pcall 兜。
    if be.getPlayerVehicle == nil then return end
    local veh = be:getPlayerVehicle(0)
    if veh then
      local p = veh:getPosition()
      if p then anchor = V(num(p.x), num(p.y), num(p.z)) end
    end
  end)
  if not anchor then return nil end

  diag.candidates = 1

  -- 半径：人多就放大一点，避免车挤在一起
  local n = 0
  for _, p in ipairs(state.players or {}) do
    if tostring(p.role or '') ~= '' then n = n + 1 end
  end
  local radius = TUNE.derbyRadius
  if n >= 3 then radius = radius + (n - 3) * 1.6 end
  radius = clamp(radius, TUNE.derbyMinRadius, TUNE.derbyMaxRadius)

  local placements = {}
  local occupied = {}
  local index = 0

  for _, p in ipairs(state.players or {}) do
    local role = tostring(p.role or '')
    if role ~= '' then
      -- 均匀分布；第一个放在正北，顺时针排
      local angle = (index / math.max(1, n)) * math.pi * 2
      index = index + 1

      local x = anchor.x + math.cos(angle) * radius
      local y = anchor.y + math.sin(angle) * radius

      -- 车头朝圆心（撞起来才好看）：方向 = 圆心 - 车
      local dir = V(anchor.x - x, anchor.y - y, 0)
      local dlen = math.sqrt(dir.x * dir.x + dir.y * dir.y)
      if dlen > 0.01 then dir = V(dir.x / dlen, dir.y / dlen, 0) end

      local surface = nil
      pcall(function()
        if be and be.getSurfaceHeightBelow then
          surface = be:getSurfaceHeightBelow(V(x, y, anchor.z + 2))
        end
      end)
      if surface == nil or math.abs(num(surface) - anchor.z) > TUNE.surfaceTol then
        diag.surface = diag.surface + 1
        -- 这个角度地面不平：退到锚点高度（宁可略高也不要埋进地里）
        surface = anchor.z
      end

      local pos = V(x, y, num(surface) + TUNE.rideHeight)
      for _, prev in ipairs(occupied) do
        if dist3(pos, prev) < TUNE.minCarGap then
          diag.overlap = diag.overlap + 1
        end
      end
      occupied[#occupied + 1] = pos

      placements[role .. ':' .. tostring(p.spawnIndex or index)] = {
        position = pos,
        direction = dir,
        routeDistance = radius,
        roadDirection = 0,
      }
    end
  end

  if next(placements) == nil then return nil end

  local layout = {
    kind = 'arena',
    anchor = anchor,
    radius = radius,
    placements = placements,
    diagnostic = diag,
  }
  layouts[cacheKey] = layout
  return layout
end

-- ---------------------------------------------------------------------------
-- 对外：生成出生方案
--
-- 由主模组在「本机是规划者、且本回合还没出方案」时调用。
-- 返回 { round, slots = { {peerId, position={x,y,z}, forward={x,y,z}} } }
-- 或者 { error = '...' }
-- ---------------------------------------------------------------------------
function M.proposal(state, mapId)
  if type(state) ~= 'table' or type(state.players) ~= 'table' then return nil end

  local mode = tostring(state.id or '')
  if mode ~= 'cops_robber' and mode ~= 'derby' then return nil end

  local key = table.concat({
    tostring(mapId), tostring(state.round or 0),
    tostring(state.spawnLayoutAt or ''), tostring(state.spawnLayoutSeed or ''),
    tostring(state.spawnLayoutOrdinal or ''),
  }, ':')
  if sentKey == key then return nil end

  local layout
  if mode == 'cops_robber' then
    layout = buildCorridorLayout(state, mapId)
  else
    layout = buildDerbyArena(state, mapId)
  end

  sentKey = key

  if not layout then
    return {
      round = state.round,
      error = (mode == 'cops_robber') and 'no_road_corridor' or 'no_derby_anchor',
      diagnostic = lastDiag,
    }
  end

  local slots = {}
  for _, player in ipairs(state.players) do
    local role = tostring(player.role or '')
    if role == 'cop' or role == 'robber' then
      local idx = player.spawnIndex or 0
      local placement = layout.placements[role .. ':' .. tostring(idx)]
      if not placement then return nil end
      slots[#slots + 1] = {
        peerId = player.peerId,
        position = { placement.position.x, placement.position.y, placement.position.z },
        forward = { placement.direction.x, placement.direction.y, placement.direction.z },
      }
    end
  end

  return {
    round = state.round,
    kind = layout.kind,
    slots = slots,
    diagnostic = layout.diagnostic,
  }
end

-- ---------------------------------------------------------------------------
-- 对外：状态 / 清理
-- ---------------------------------------------------------------------------
function M.diagnostic() return lastDiag end

function M.reset()
  layouts = {}
  sentKey = nil
  lastDiag = {}
  placed = {}
  lastPlacedRound = -1
  appliedRound = -1
end

function M.tune() return TUNE end


-- ===========================================================================
-- 出生摆放层（把方案里的槽位真正落到车上）
-- ===========================================================================
--
-- 这一层从主模组搬进来：它是「出生」这件事的一部分，而且主模组顶层
-- local 已经顶到 Lua 的 200 个上限（超了直接编译失败），必须搬出来。
--
-- 需要主模组注入的依赖（都是一次性 setDeps）：
--   getVehicle()  -> 本机玩家车
--   getPeerId()   -> 本机在玩法名单里的 id
--   getPlayerName() -> 本机昵称
--   getLevelId()  -> 当前关卡 id
--   logMsg(...)   -> 日志
--
-- 逐条对照设计文档里的「坑」：
--   坑⑥ safeTeleport 参数：centeredPosition=true（pos 是车身中心）、
--        resetVehicle=true（顺带复位悬挂/破损网格）。
--        高度**不再纠正** —— 规划层已用 surfaceHeightBelow 精确贴地，
--        再纠正一次会把人从走廊上顶走。
--   坑⑦ 落位后边界校验，容差收紧：警匪/德比 3m，赛道日 1m（只看水平）。
--        超容差 → 判失败，且**本回合不再重试**（重试只会更糟）。
--   坑⑧ 防抖键 = round : mode : 车 id : 目标坐标。同回合同坐标只摆一次，
--        晚到的快照不能取消已完成的摆放（BeamLink 的 ready/unready 抖动）。
--   坑⑨ 摆完调 extensions.hook('onVehicleResetted', id)，
--        让相机/悬挂/跟随逻辑知道车换位置了。
-- ===========================================================================

-- 摆放容差（米）：超过就认为这次落位不可信
local PLACE_TOLERANCE = {
  cops_robber = 3.0,    -- 坑⑦：走廊玩法收紧
  derby       = 3.0,
  hide_seek   = 3.0,    -- 捉迷藏同样是路面精确点（规划在 startrideHideSeek 里）
  track_day   = 1.0,    -- 只有水平位移要求
  _default    = 24.0,
}

-- placed / lastPlacedRound / appliedRound 的声明已上移到 M.reset() 之前
-- （见文件开头处说明）—— 留在这里会被 M.reset 写成全局变量、静默失效。

-- 注入的依赖
local deps = {
  getVehicle    = nil,
  getPeerId     = nil,
  getPlayerName = nil,
  getLevelId    = nil,
  logMsg        = nil,
}

function M.setDeps(d)
  if type(d) ~= 'table' then return end
  for k, v in pairs(d) do
    if deps[k] ~= nil or k == 'getVehicle' or k == 'getPeerId'
       or k == 'getPlayerName' or k == 'getLevelId' or k == 'logMsg' then
      deps[k] = v
    end
  end
end

local function logPlace(...)
  if deps.logMsg ~= nil then
    pcall(deps.logMsg, ...)
  end
end

local function toleranceFor(mode)
  return PLACE_TOLERANCE[mode] or PLACE_TOLERANCE._default
end

-- 摆放我自己那一台车。
-- slot = { peerId, position = {x,y,z}, forward = {x,y,z} }
-- 返回 true=已摆放, false=失败, nil=还没到时候（等地图 / 等车）
function M.placeSelf(slot, mode, round)
  if type(slot) ~= 'table' or type(slot.position) ~= 'table' then return false end

  -- 本机没车就别动（还在加载 / 还没上车）
  local veh = nil
  if deps.getVehicle ~= nil then
    local ok, v = pcall(deps.getVehicle)
    if ok then veh = v end
  end
  if veh == nil then return nil end

  -- 地图还没切过来时，规划出来的坐标是**另一张图**的，硬传会穿地
  local levelId = ''
  if deps.getLevelId ~= nil then
    local ok, lv = pcall(deps.getLevelId)
    if ok and type(lv) == 'string' then levelId = lv end
  end
  if levelId == '' then return nil end

  local p = slot.position
  local f = slot.forward or { 0, -1, 0 }

  -- ⚠️ 车辆 id：引擎里 getId() 与 getID() **两个都存在**
  --    （spawn.lua 自己就混用：缓存键用 getId()，清理用 getID()）。
  --    不同绑定/版本可能只暴露其中一个，所以两个都试，拿不到就退回 -1。
  local vehId = -1
  pcall(function()
    if veh.getId ~= nil then vehId = veh:getId() end
    if vehId == nil and veh.getID ~= nil then vehId = veh:getID() end
  end)
  if vehId == nil then vehId = -1 end

  -- 防抖键（坑⑧）：round + 模式 + 车 id + 目标坐标
  local key = table.concat({
    tostring(round), tostring(mode), tostring(vehId),
    string.format('%.1f,%.1f,%.1f', tonumber(p[1]) or 0,
                  tonumber(p[2]) or 0, tonumber(p[3]) or 0),
  }, ':')
  if placed[key] then return true end

  -- 目标位置：vec3。⚠️ 用 V() 而不是直接 vec3()：vec3 在无头环境可能缺失，
  --    而规划层本来就有 V() 兜底，这里复用同一个。
  local pos, dir
  pos = V(tonumber(p[1]) or 0, tonumber(p[2]) or 0, tonumber(p[3]) or 0)
  dir = V(tonumber(f[1]) or 0, tonumber(f[2]) or 0, tonumber(f[3]) or 0)
  if pos == nil then return nil end

  -- 朝向：从 forward 向量构造「绕 Z 轴」的四元数。
  --
  -- ⚠️ 不要用 math.atan2 —— Lua 5.4/5.5 已经把它删了（5.3 起弃用），
  --    在 lupa 的 5.5 环境里直接 "attempt to call a nil value (field 'atan2')"。
  --    BeamNG 用 LuaJIT(5.1) 确实还有，但依赖一个已被移除的函数太脆，
  --    这里改成纯四元数代数，任何版本都对。
  --
  --    目标：车头方向 = forward。设 forward 在 XY 平面的单位向量为 (fx, fy)，
  --    则绕 Z 轴旋转 θ（cosθ=fx, sinθ=fy）对应半角四元数：
  --        q = (0, 0, sin(θ/2), cos(θ/2))
  --    用半角公式展开，避免 atan2 / acos：
  --        cos(θ/2) = sqrt((1+cosθ)/2)，sin(θ/2) = sign(sinθ)*sqrt((1-cosθ)/2)
  --    BeamNG 的 quat 分量顺序是 (x, y, z, w)。
  local rot = nil
  pcall(function()
    local fx, fy = dir.x, dir.y

    -- 只关心水平朝向；水平分量为零时退回默认指向 +Y
    local hlen = math.sqrt(fx * fx + fy * fy)
    if hlen < 1e-4 then
      fx, fy = 0.0, 1.0
    else
      fx, fy = fx / hlen, fy / hlen
    end

    local c = fx
    if c > 1 then c = 1 elseif c < -1 then c = -1 end

    local hw = math.sqrt((1 + c) / 2)          -- cos(θ/2)
    local hs = math.sqrt((1 - c) / 2)          -- |sin(θ/2)|
    if fy < 0 then hs = -hs end                -- 用 sinθ 的符号定象限

    if quat ~= nil then
      rot = quat(0, 0, hs, hw)
    else
      rot = { x = 0, y = 0, z = hs, w = hw }
    end
  end)
  if rot == nil then return nil end

  local sp = spawn
  local moved = false
  if sp ~= nil and sp.safeTeleport ~= nil then
    pcall(function()
      -- 坑⑥：centeredPosition=true（pos 是车身中心）、resetVehicle=true、
      --       不再做地面纠正（规划层已贴地）。
      sp.safeTeleport(veh, pos, rot, nil, nil, nil, true, true, nil, nil)
      moved = true
    end)
  end
  if not moved then
    -- safeTeleport 不可用时的兜底：直接摆位姿
    pcall(function()
      veh:setPosRot(pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w)
      moved = true
    end)
  end
  if not moved then
    placed[key] = false
    return false
  end

  -- 坑⑦：落位后的边界校验
  local dx, dy, dz = 0, 0, 0
  pcall(function()
    local np = veh:getPosition()
    dx, dy, dz = np.x - pos.x, np.y - pos.y, np.z - pos.z
  end)
  local horiz = math.sqrt(dx * dx + dy * dy)
  local tol = toleranceFor(mode)
  local off = (mode == 'track_day') and horiz
              or math.sqrt(dx * dx + dy * dy + dz * dz)

  if off > tol then
    placed[key] = true     -- 记下来：这一回合不再重试（重试只会更糟）
    logPlace(string.format(
      '玩法：出生摆放偏移过大 %.2fm > %.2fm（%s），本回合不再重试',
      off, tol, tostring(mode)))
    return false
  end

  placed[key] = true

  -- 坑⑨：通知引擎这辆车换位置了（相机 / 悬挂 / 跟随逻辑靠它复位）
  pcall(function()
    if extensions ~= nil and extensions.hook ~= nil then
      extensions.hook('onVehicleResetted', vehId)
    end
  end)

  return true
end

-- 从一份方案里挑出属于**本机**的槽位。
--
-- ⚠️ 必须**按优先级分两趟**找，不能在一个循环里混判：
--    peerId 是唯一标识，昵称只是历史兼容口径。
--    如果一趟里同时比 peerId 和昵称，那么「我的昵称恰好等于别人的 peerId」时
--    （例如本机 id='alice' 昵称='bob'，而 'bob' 是另一个玩家的 id），
--    会因为先扫到那一条而**认错槽位** → 被摆到别人的出生点。
--    所以：第一趟只认 peerId；找不到才退到第二趟认昵称。
function M.pickMySlot(plan)
  if type(plan) ~= 'table' or type(plan.slots) ~= 'table' then return nil end

  local selfId = 'self'
  if deps.getPeerId ~= nil then
    local ok, id = pcall(deps.getPeerId)
    if ok and id ~= nil and id ~= '' then selfId = tostring(id) end
  end
  local myName = ''
  if deps.getPlayerName ~= nil then
    local ok, nm = pcall(deps.getPlayerName)
    if ok and type(nm) == 'string' then myName = nm end
  end

  local matchedSelf = nil
  local matchedName = nil
  for _, slot in ipairs(plan.slots) do
    if type(slot) == 'table' then
      local pid = tostring(slot.peerId or '')
      if pid ~= '' then
        -- 第一趟：peerId（含 'self' 这个历史别名）
        if matchedSelf == nil and (pid == selfId or pid == 'self') then
          matchedSelf = slot
        end
        -- 第二趟候选：昵称
        if matchedName == nil and myName ~= '' and pid == myName then
          matchedName = slot
        end
      end
    end
  end

  return matchedSelf or matchedName
end

-- 记录「本机已经为本回合摆放过了」，避免反复摆。
-- ===========================================================================
-- 玩法出生的「规划 + 摆放」驱动层
--
-- ⚠️ 这一层原来住在 startride_mod.lua 里，2026-10-04 搬到这里，
--    唯一原因是主模组的顶层 local 撞到了 Lua 5.1 的 200 上限
--    （撞线 = 编译期 `too many local variables` = 整个 GE 扩展静默不加载）。
--    搬运时行为**逐行等价**，只是把「谁持有这些 local」换了个地方。
--
-- 依赖注入：主模组不再调 setDeps —— buildModePlan / buildHideSeekPlan /
-- applyModePlan 会在首帧自己把 setDeps({ startride_mod = ... }) 调一次。
-- 这样主模组侧一个符号都不用留。
-- ===========================================================================

local bridgeReady = false
local bridgeMod = nil   -- 主模组句柄（bindBridge 之后一直有效）

-- 延迟注入。主模组在 onUpdateRaw 里先跑本层的函数，所以首帧这里
-- 一定拿得到 盘上的 startride_mod（已经 onExtensionLoaded 过）。
-- 绑定主模组。**可在任何时刻调用**（主模组 onExtensionLoaded 里会主动调一次），
-- 之后 srDebugPlaceSelf / 诊断面板这些「不经过 onUpdateRaw」的入口也能直接用。
function M.bindBridge(mod)
  if mod == nil then return end
  bridgeMod = mod
  bridgeReady = true

  -- 并入而不是覆盖：下面这几个是 startrideSpawn.lua 自己那套 setDeps 的
  -- 权威字段（placeSelf / pickMySlot 用），必须跟着主模组的环境一起换新；
  -- startride_mod 只是**额外**交出去给「出生驱动层」用的把手。
  -- ⚠️ 不要单独调 setDeps({startride_mod=...}) 去覆盖 —— 那会把
  --    getVehicle 等字段（值为 nil）一起写进去，反而把好值打坏。
  pcall(M.setDeps, {
    getVehicle = function()
      if be == nil or be.getPlayerVehicle == nil then return nil end
      local k, veh = pcall(function() return be:getPlayerVehicle(0) end)
      if k and veh ~= nil then return veh end
      return nil
    end,
    getPeerId = function()
      local id = mod.myId and mod.myId() or nil
      if id == nil or id == '' then return 'self' end
      return tostring(id)
    end,
    getPlayerName = function() return mod.playerName() end,
    getLevelId    = function() return mod.currentLevelId() end,
    logMsg        = function(...) mod.logMsg(...) end,
  })
  deps.startride_mod = mod
end

-- 由 startride_mod.lua 的 onExtensionLoaded 直接调用（自注册）。
M.autoBind = M.bindBridge

local function ensureBridge(mod)
  if bridgeReady then return end
  if mod == nil then return end
  M.bindBridge(mod)
end

-- 本层自己的出生记账（与主模组那边的 srSpawn 表分开：两处都是「一次性」状态，
-- 但主模组那份还要给 M.srDebugSpawnState 读，不能共用）
local memo = {
  modePlannedRound   = -1,
  modeLocalPlan      = nil,
  modeAppliedRound   = -1,
  hidePlannedRound   = -1,
  hideLocalPlan      = nil,
  hideAppliedRound   = -1,
}

local function internalLog(mod, ...)
  pcall(mod.logMsg, ...)
end

-- ---------------------------------------------------------------------------
-- 警匪 / 德比：规划（房主一次性算好并广播）
-- ---------------------------------------------------------------------------
function M.buildModePlan(SRMode, mod)
  if mod ~= nil then ensureBridge(mod) end
  local mod = deps.startride_mod
  if mod == nil then return end
  if SRMode == nil then return end

  local phase = SRMode.phase()
  if phase ~= 'staging' then return end
  if not SRMode.isAuthority() then return end

  local round = SRMode.round()
  if memo.modePlannedRound == round then return end

  local snap = SRMode.snapshot()
  local roster = snap.roles or {}
  local players = {}
  local index = 0
  for _, p in ipairs(SRMode.roster()) do
    players[#players + 1] = {
      peerId = p.id,
      role = roster[p.id] or 'robber',
      spawnIndex = index,
    }
    index = index + 1
  end
  if #players < 2 then return end

  local roomInfo = mod.roomInfo()
  local state = {
    id = snap.mode,
    round = round,
    players = players,
    spawnLayoutAt = tostring(round),
    spawnLayoutSeed = tonumber(roomInfo.spawnSeed) or round,
    spawnLayoutOrdinal = round,
    spawnLayoutBaseSeed = tonumber(roomInfo.spawnSeed) or 1,
  }

  local plan = M.proposal(state, mod.currentLevelId())
  if type(plan) ~= 'table' then return end

  memo.modePlannedRound = round

  if plan.error then
    internalLog(mod, '玩法：出生规划失败 → ' .. tostring(plan.error))
    mod.queuePacket({ type = 'mode-spawn-plan', round = round, error = plan.error })
    mod.flushOut()
    return
  end

  mod.queuePacket({
    type = 'mode-spawn-plan',
    round = round,
    kind = plan.kind,
    slots = plan.slots,
  })
  mod.flushOut()
  internalLog(mod, '玩法：出生方案已广播，共 ' .. tostring(#(plan.slots or {})) .. ' 个位置')

  -- 方案本地也要用（房主自己不会收到自己的广播）
  memo.modeLocalPlan = { round = round, slots = plan.slots }
end

-- ---------------------------------------------------------------------------
-- 捉迷藏：规划（不走走廊/围场，由 SRHide 自己算）
-- ---------------------------------------------------------------------------
function M.buildHideSeekPlan(SRHide, mod)
  if mod ~= nil then ensureBridge(mod) end
  local mod = deps.startride_mod
  if mod == nil then return end
  if SRHide == nil then return end
  if SRHide.phase() ~= 'staging' then return end
  if not SRHide.isAuthority() then return end

  local round = SRHide.round()
  if memo.hidePlannedRound == round then return end

  local snap = SRHide.snapshot()
  local roles = snap.roles or {}
  local players = {}
  for _, p in ipairs(SRHide.roster()) do
    players[#players + 1] = {
      peerId = p.id,
      role = roles[p.id] or 'hider',
    }
  end
  if #players < 2 then return end

  local roomInfo = mod.roomInfo()
  local state = {
    id = 'hide_seek',
    round = round,
    players = players,
    spawnLayoutSeed = tonumber(roomInfo.spawnSeed) or round,
    spawnLayoutOrdinal = round,
  }

  local plan = nil
  local ok = pcall(function() plan = SRHide.buildSpawnPlan(state) end)
  memo.hidePlannedRound = round
  if not ok or type(plan) ~= 'table' then
    internalLog(mod, '捉迷藏：出生规划失败（没找到足够的分散路点）')
    return
  end

  mod.queuePacket({
    type = 'mode-spawn-plan',
    round = round,
    kind = plan.kind,
    slots = plan.slots,
  })
  mod.flushOut()
  internalLog(mod, '捉迷藏：出生方案已广播，共 ' .. tostring(#(plan.slots or {})) .. ' 个位置')

  memo.hideLocalPlan = { round = round, slots = plan.slots }
end

-- ---------------------------------------------------------------------------
-- 摆放：把方案里属于**我自己**的那个槽位落到车上
--
-- 警匪/德比与捉迷藏共用本函数，只在「取方案」「记账键」「mode 标签」上分叉。
-- ---------------------------------------------------------------------------
function M.applyModePlan(SRMode, isHide, mod)
  if mod ~= nil then ensureBridge(mod) end
  local mod = deps.startride_mod
  if mod == nil then return end
  if SRMode == nil then return end

  local hide = (isHide == true)
  if not hide then
    -- 自动判据，兼容主模组那边不传第二参的旧调用
    local nm = SRMode.mode and SRMode.mode() or nil
    if nm ~= 'cops_robber' and nm ~= 'derby' then
      if SRMode.mode and SRMode.mode() == 'hide_seek' then hide = true end
    end
  end

  if SRMode.phase() ~= 'staging' then return end

  local mode
  if hide then
    mode = 'hide_seek'
  else
    mode = SRMode.mode()
    if mode ~= 'cops_robber' and mode ~= 'derby' then return end
  end

  -- 取方案：房主用本地方案，加入者用阶段机缓存里的
  local plan = nil
  if SRMode.isAuthority() then
    plan = hide and memo.hideLocalPlan or memo.modeLocalPlan
  end
  if plan == nil and SRMode.peekSpawnPlan then
    plan = SRMode.peekSpawnPlan()
  end
  if plan == nil then return end

  local slot = M.pickMySlot(plan)
  if slot == nil then return end

  local round = tonumber(plan.round) or SRMode.round()
  local applied = hide and memo.hideAppliedRound or memo.modeAppliedRound
  if applied == round then return end

  local res = M.placeSelf(slot, mode, round)
  if res == nil then return end        -- 还没到时候，下一帧再试

  -- 无论成败都记下 round：失败也不要反复摆（坑⑦）
  if hide then memo.hideAppliedRound = round else memo.modeAppliedRound = round end
  M.noteAppliedRound(round)

  if res then
    if hide then
      internalLog(mod, '捉迷藏：已按出生方案落位（回合 ' .. tostring(round) .. '）')
    else
      internalLog(mod, '玩法：已按出生方案落位（回合 ' .. tostring(round) .. '）')
    end
  end

  -- 就位汇报：把结果告诉阶段机，房主再广播给全场。
  -- ⚠️ 失败/超差也要汇报（notePlacement(pid, true)），否则房主会一直等人到超时。
  local myPid = nil
  if hide then
    myPid = mod.myId()
    if myPid == nil or myPid == '' then myPid = 'self' end
    myPid = tostring(myPid)
  else
    myPid = 'self'
    local ok2, mid = pcall(function()
      local st = M.placeState()
      if type(st) == 'table' then return st.selfId end
      return nil
    end)
    if ok2 and type(mid) == 'string' and mid ~= '' then myPid = mid end
  end

  SRMode.notePlacement(myPid, true)

  if SRMode.isAuthority() then
    mod.queuePacket({ type = 'mode-placement', id = myPid, ready = true, round = round })
    mod.flushOut()
  end
end

function M.noteAppliedRound(round)
  if lastPlacedRound ~= round then
    lastPlacedRound = round
    placed = {}
  end
  appliedRound = round
end

function M.appliedRound() return appliedRound end

-- 诊断用
function M.placeState()
  return {
    appliedRound = appliedRound,
    lastPlacedRound = lastPlacedRound,
    placedCount = (function()
      local n = 0
      for _ in pairs(placed) do n = n + 1 end
      return n
    end)(),
  }
end

return M
