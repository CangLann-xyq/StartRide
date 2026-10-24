-- ============================================================================
-- StartRide 玩法 HUD
-- ============================================================================
-- 警匪追逐 / 捉迷藏 / 德比的游戏内界面。**沿用 StartRide 自己的视觉语言**：
-- 深色半透明卡 + 左侧身份色条 + 蓝色主色（COL.accent），不用 BeamLink 那套。
--
-- 全部画在一个无输入全屏窗口（##sr_modehud）里，位置由代码算，玩家拖不动。
-- 之所以用「一个全屏窗口 + 绝对定位」，是因为要在四个屏幕角各画一块，
-- 分成四个窗口的话 ImGui 的窗口堆叠顺序会让它们互相遮挡。
--
-- ⚠️ 坑：窗口尺寸必须每帧 SetNextWindowSize(Cond_Always)。
--    窗口尺寸只在第一帧设的话，玩家改分辨率后整块 HUD 会错位到屏幕外。
--
-- ⚠️ 坑：色块（进度条 / 血条）一定要用**前景绘制层**画，不要用 ImGui 的
--    ProgressBar —— 后者要推进布局、会影响后面文字的位置，条一长就把文字挤飞。
--
-- ⚠️ 坑：所有绘制 API 一律走 pickFn + pcall 兜底。绑定里同一函数常有
--    `Xxx` / `Xxx1` / `Xxx2` 多个重载名，直接点名会「在某些版本上整块 HUD 消失」。
--
-- ⚠️ 上值：所有常量收进 TUNE，子表收进 TUNE2 / COL，避免 M.render 上值爆 60。
--
-- 视觉分级（三级字号 + 三层深度）：
--     L1 标题  12pt 角色/卡片名（角色色）
--     L2 数值  默认字号，白/语义色，最重要的数字
--     L3 说明  默认字号，dim 灰，补充解释
--   卡片统一：外描边(1px 白 8%) + 左侧 3px 身份色条 + 顶部 1px 高光 + 圆角 10
-- ============================================================================

local M = {}

local im = ui_imgui

-- ---------------------------------------------------------------------------
-- 绘制 API 探测（绑定里同名函数有多个重载后缀，必须挨个试）
-- ---------------------------------------------------------------------------
local function pickFn(tbl, ...)
  -- ⚠️ tbl 可能是 nil：主模组 require 这个模块时 ui_imgui 未必已经就绪，
  --    测试环境（无 UI 的裸 Lua 态）里更是根本没有这个全局。
  --    这里必须容错，否则模块加载期就崩，整个联机跟着挂。
  if type(tbl) ~= 'table' then return nil, nil end
  for _, n in ipairs({ ... }) do
    local f = tbl[n]
    if type(f) == 'function' then return f, n end
  end
  return nil, nil
end

local fnAddRectFilled   = pickFn(im, 'ImDrawList_AddRectFilled', 'ImDrawList_AddRectFilled1',
                                     'ImDrawList_AddRectFilled2')
local fnAddRect         = pickFn(im, 'ImDrawList_AddRect', 'ImDrawList_AddRect1',
                                     'ImDrawList_AddRect2')
-- 渐变填充：参数与 AddRectFilled 不同（四个角的颜色各一个），拿不到就走纯色兜底
local fnAddRectMulti    = pickFn(im, 'ImDrawList_AddRectFilledMultiColor',
                                     'ImDrawList_AddRectFilledMultiColor1')
local fnAddCircleFilled = pickFn(im, 'ImDrawList_AddCircleFilled', 'ImDrawList_AddCircleFilled1')
local fnGetColorU32     = pickFn(im, 'GetColorU322', 'GetColorU32', 'GetColorU321', 'GetColorU323')
local fnGetDrawList     = pickFn(im, 'GetWindowDrawList')

local canDraw = (fnAddRectFilled and fnGetColorU32 and fnGetDrawList) and true or false

-- ---------------------------------------------------------------------------
-- 可调常量
-- ---------------------------------------------------------------------------
local TUNE = {
  margin      = 18,     -- 距屏幕边距
  cardW       = 316,    -- 主卡宽度（加宽，给三级字号留呼吸）
  padX        = 16,     -- 内边距横向（加大）
  padY        = 13,     -- 内边距纵向（加大）
  rounding    = 10,     -- 卡片圆角
  railW       = 3,      -- 左侧身份色条宽度（细化，更精致）
  barH        = 8,      -- 进度条高度
  barGap      = 20,     -- 进度条上方文字到条的间距
  bgAlpha     = 0.82,
  -- 卡片层次
  cardBg      = { 0.055, 0.080, 0.115, 0.82 },
  cardEdge    = 0.08,   -- 外描边透明度（白）
  cardTop     = 0.05,   -- 顶部高光透明度（白）
  trackBg     = 0.10,   -- 进度条底槽透明度（白）
  -- 结算淡入淡出
  fadeInMs    = 220,
  fadeOutMs   = 450,
  fadeHoldMs  = 4500,
  -- 雷达
  radarHalfDeg = 15,    -- 「前方」扇区半角
  radarRange   = 400,   -- 雷达显示最远距离
  radarW       = 316,
  radarH       = 30,    -- 方向条高度（加高）
  gaugeR       = 62,    -- 仪表盘半径
}

-- 字号分级（用 SetWindowFontScale；ImGui 默认 ~13px）
local FS = {
  L1 = 1.18,   -- 标题
  L2 = 1.00,   -- 数值
  L3 = 0.88,   -- 说明
}

-- 行高（跟 FS 对应，避免文字叠在一起）
local LH = {
  L1 = 24,
  L2 = 21,
  L3 = 18,
}

-- 配色（与主模组 C 表同源，但这里独立定义，避免跨文件传引用）
local COL = {
  accent = { 0.35, 0.72, 1.00, 1 },
  ok     = { 0.30, 0.87, 0.50, 1 },
  warn   = { 0.98, 0.75, 0.30, 1 },
  danger = { 0.95, 0.35, 0.40, 1 },
  text   = { 0.92, 0.94, 0.97, 1 },
  dim    = { 0.60, 0.65, 0.74, 1 },
  -- 角色色（StartRide 主题里警察用蓝、强盗用橙红，避免纯红）
  cop    = { 0.40, 0.70, 1.00, 1 },
  robber = { 1.00, 0.58, 0.30, 1 },
  -- 捉迷藏：搜索者用主题蓝（追人方），躲藏者用青绿（藏匿方，避免红）
  seeker = { 0.40, 0.70, 1.00, 1 },
  hider  = { 0.36, 0.85, 0.78, 1 },
}

-- 卡片底色（在 TUNE 里，这里只是给个短名方便读）
local CARD_BG = TUNE.cardBg

-- ---------------------------------------------------------------------------
-- 模式组件（每个非自由模式一个独立文件）
-- ---------------------------------------------------------------------------
-- 这些组件只负责「本模式专属的那一块面板」（右下角），主 HUD 的左侧身份卡 /
-- 左下模式卡 / 右上规则卡仍由本文件画 —— 两边职责不重叠：
--   主 HUD  → 所有模式共有的骨架
--   组件    → 这个模式才有的东西（追捕要素 / 场况 / 搜寻 / 赛道）
-- ⚠️ 组件全部走 pcall：任何一个文件缺失或抛错，都不能影响其它模式。
local PACK_MODS = {}
do
  local want = {
    { 'cops_robber', 'startridePackChase' },
    { 'derby',       'startridePackDerby' },
    { 'hide_seek',   'startridePackSeek'  },
    { 'track_day',   'startridePackTrack' },
  }
  local i
  for i = 1, #want do
    local ok, mod = pcall(require, want[i][2])
    if ok and type(mod) == 'table' and type(mod.panel) == 'function' then
      PACK_MODS[want[i][1]] = mod
    end
  end
end

-- ---------------------------------------------------------------------------
-- 小工具
-- ---------------------------------------------------------------------------
local function v4(c, a)
  local alpha = a or c[4] or 1
  return im.ImVec4(c[1], c[2], c[3], alpha)
end

local function clamp01(v)
  if v < 0 then return 0 end
  if v > 1 then return 1 end
  return v
end

local function smoothstep(t)
  t = clamp01(t)
  return t * t * (3 - 2 * t)
end

-- 把颜色往白/黑推，做同色系的层次（k>0 提亮，k<0 压暗）
local function shade(c, k)
  local r, g, b = c[1], c[2], c[3]
  if k >= 0 then
    return { r + (1 - r) * k, g + (1 - g) * k, b + (1 - b) * k, c[4] or 1 }
  end
  local m = 1 + k
  return { r * m, g * m, b * m, c[4] or 1 }
end

local function colorU32(col)
  if not fnGetColorU32 then return nil end
  local ok, v = pcall(fnGetColorU32, col)
  if ok then return v end
  return nil
end

-- 画一个不依赖布局的实心色块（相对窗口左上角）
local function blk(dl, x, y, w, h, col, rounding)
  if not (dl and fnAddRectFilled) then return end
  local cu = colorU32(col)
  if not cu then return end
  pcall(function()
    local wp = im.GetWindowPos()
    fnAddRectFilled(dl,
      im.ImVec2(wp.x + x, wp.y + y),
      im.ImVec2(wp.x + x + w, wp.y + y + h),
      cu, rounding or 4, nil)
  end)
end

-- 描边（不填充）
local function stroke(dl, x, y, w, h, col, rounding, thick)
  if not (dl and fnAddRect) then return end
  local cu = colorU32(col)
  if not cu then return end
  pcall(function()
    local wp = im.GetWindowPos()
    fnAddRect(dl,
      im.ImVec2(wp.x + x, wp.y + y),
      im.ImVec2(wp.x + x + w, wp.y + y + h),
      cu, rounding or 4, nil, thick or 1)
  end)
end

-- 横向渐变填充（拿不到 MultiColor 就退回纯色）
local function grad(dl, x, y, w, h, cL, cR, rounding)
  if not dl then return end
  local cuL, cuR = colorU32(cL), colorU32(cR)
  if fnAddRectMulti and cuL and cuR then
    local ok = pcall(function()
      local wp = im.GetWindowPos()
      fnAddRectMulti(dl,
        im.ImVec2(wp.x + x, wp.y + y),
        im.ImVec2(wp.x + x + w, wp.y + y + h),
        cuL, cuR, cuR, cuL)
    end)
    if ok then return end
  end
  blk(dl, x, y, w, h, cL, rounding)
end

-- 卡片：底 + 右侧/底部柔和外发光 + 1px 外描边 + 顶部高光 + 左侧身份色条
--
-- 发光用「三层逐渐透明的同色圆角块」堆出来，比单层描边有厚度感，
-- 又不需要 AddShadowRect（那个在部分绑定里不存在）。
local function card(dl, x, y, w, h, rail, alpha)
  local a = alpha or 1
  if dl then
    -- 外发光（三层，向外扩 1/2/3 px）
    local glowCol = v4(shade(rail, -0.45), 0.10 * a)
    blk(dl, x - 3, y - 3, w + 6, h + 6, glowCol, TUNE.rounding + 3)
    blk(dl, x - 2, y - 2, w + 4, h + 4, v4(shade(rail, -0.45), 0.14 * a), TUNE.rounding + 2)
    blk(dl, x - 1, y - 1, w + 2, h + 2, v4(shade(rail, -0.30), 0.16 * a), TUNE.rounding + 1)
  end
  blk(dl, x, y, w, h, im.ImVec4(CARD_BG[1], CARD_BG[2], CARD_BG[3], CARD_BG[4] * a),
      TUNE.rounding)
  if dl then
    -- 顶部 1px 高光（做出「玻璃被上方光源照亮」的感觉）
    blk(dl, x + TUNE.rounding, y, w - TUNE.rounding * 2, 1,
        im.ImVec4(1, 1, 1, TUNE.cardTop * a), 0)
    -- 1px 外描边
    stroke(dl, x, y, w, h, im.ImVec4(1, 1, 1, TUNE.cardEdge * a), TUNE.rounding, 1)
    -- 左侧身份色条（带一点提亮，避免纯色显脏）
    blk(dl, x, y + 2, TUNE.railW, h - 4, v4(shade(rail, 0.12), a), TUNE.railW)
  end
end

-- 进度条：底槽 + 渐变填充 + 顶部高光 + 末端亮帽
local function barFill(dl, x, y, w, h, ratio, col, alpha)
  local a = alpha or 1
  blk(dl, x, y, w, h, im.ImVec4(1, 1, 1, TUNE.trackBg * a), h * 0.5)
  local r = clamp01(ratio)
  if r <= 0 then return end
  local fw = w * r
  -- 渐变：左深右亮，像有方向感
  grad(dl, x, y, fw, h, v4(shade(col, -0.28), a), v4(shade(col, 0.18), a), h * 0.5)
  -- 顶部高光条
  blk(dl, x + 1, y + 1, math.max(0, fw - 2), math.max(1, h * 0.28),
      im.ImVec4(1, 1, 1, 0.22 * a), h * 0.25)
  -- 末端亮帽
  if fw >= 4 then
    blk(dl, x + fw - 2, y, 2, h, v4(shade(col, 0.45), a), 1)
  end
end

-- 仪表盘式方位指示：半圆刻度 + 中央「正前」标记 + 目标指针
--
-- 替代原来那条直条。刻度用短块拼，不依赖 PathArcTo。
local function gauge(dl, cx, cy, radius, deg, col, alpha)
  local a = alpha or 1
  local R = radius
  -- 底盘（半透明圆）
  if dl and fnAddCircleFilled then
    local cu = colorU32(im.ImVec4(1, 1, 1, 0.05 * a))
    if cu then
      pcall(function()
        local wp = im.GetWindowPos()
        fnAddCircleFilled(dl, im.ImVec2(wp.x + cx, wp.y + cy), R, cu, 32)
      end)
    end
  end
  -- 刻度：-90°..+90°（左右各 90°），每 15° 一根，越靠中间越长
  local i
  for i = -6, 6 do
    local ang = math.rad(i * 15)
    local inner = R * ((i % 3 == 0) and 0.66 or 0.78)
    local outer = R * 0.92
    local sx, sy = cx + math.sin(ang) * inner, cy - math.cos(ang) * inner
    local ex, ey = cx + math.sin(ang) * outer, cy - math.cos(ang) * outer
    local wgt = (i % 3 == 0) and 2 or 1
    local tickCol = im.ImVec4(1, 1, 1, ((i % 3 == 0) and 0.26 or 0.15) * a)
    -- 用小块拼短线（避免 AddLine 的绑定差异）
    local steps = 5
    local k
    for k = 0, steps - 1 do
      local t0 = k / steps
      local t1 = (k + 1) / steps
      local mx = sx + (ex - sx) * t0
      local my = sy + (ey - sy) * t0
      local segLen = math.sqrt((ex - sx) ^ 2 + (ey - sy) ^ 2) / steps
      blk(dl, mx - wgt * 0.5, my - segLen * 0.5, wgt, segLen + 1, tickCol, 0)
    end
  end
  -- 中央「正前」向上标记
  blk(dl, cx - 1, cy - R - 6, 2, 6, v4(COL.accent, 0.55 * a), 0)
  -- 目标指针（红点 + 从中心射出的短杆）
  if deg then
    local rad = math.rad(deg)
    local px, py = cx + math.sin(rad) * (R * 0.70), cy - math.cos(rad) * (R * 0.70)
    if dl and fnAddCircleFilled then
      local cu = colorU32(v4(col, a))
      if cu then
        pcall(function()
          local wp = im.GetWindowPos()
          fnAddCircleFilled(dl, im.ImVec2(wp.x + px, wp.y + py), 4.5, cu, 16)
        end)
      end
    end
    -- 中心到指针的连线
    local steps = 10
    local k
    for k = 0, steps - 1 do
      local t = (k + 0.5) / steps
      local mx = cx + (px - cx) * t
      local my = cy + (py - cy) * t
      blk(dl, mx - 1, my - 1, 2, 2, v4(col, 0.30 * a), 0)
    end
  end
end

-- 三级字号文字：fs 为字号倍率
local function textAt(x, y, s, col, fs, alpha)
  local a = alpha or 1
  pcall(function() im.SetWindowFontScale(fs or 1.0) end)
  im.SetCursorPos(im.ImVec2(x, y))
  im.TextColored(v4(col, a), s)
  pcall(function() im.SetWindowFontScale(1.0) end)
end

-- 同行追加文字（SameLine 后写）
local function textSame(s, col, fs, alpha)
  local a = alpha or 1
  pcall(function() im.SetWindowFontScale(fs or 1.0) end)
  im.SameLine()
  im.TextColored(v4(col, a), s)
  pcall(function() im.SetWindowFontScale(1.0) end)
end

-- 画条目：左说明 + 右数值（说明用 L3 灰、数值用 L2 语义色）
local function kv(x, y, label, value, col)
  textAt(x, y, label, COL.dim, FS.L3)
  textAt(x + 92, y, tostring(value), col or COL.text, FS.L2)
end

-- ---------------------------------------------------------------------------
-- 角色 / 阶段的展示文案
-- ---------------------------------------------------------------------------
local function roleLabel(role)
  if role == 'driver' then return '车手' end
  if role == 'cop' then return '警察' end
  if role == 'robber' then return '强盗' end
  if role == 'seeker' then return '搜索者' end
  if role == 'hider' then return '躲藏者' end
  return '观战'
end

local function roleColor(role)
  if role == 'driver' then return COL.accent end
  if role == 'cop' then return COL.cop end
  if role == 'robber' then return COL.robber end
  if role == 'seeker' then return COL.seeker end
  if role == 'hider' then return COL.hider end
  return COL.dim
end

local function phaseLabel(phase)
  if phase == 'lobby' then return '等待开始' end
  if phase == 'staging' then return '就位中' end
  if phase == 'countdown' then return '准备' end
  if phase == 'running' then return '进行中' end
  if phase == 'finished' then return '已结束' end
  return phase
end

-- 捉迷藏的局内阶段文案（藏在 running 内部）
local function stageLabel(stage)
  if stage == 'hiding' then return '躲藏期' end
  if stage == 'seek' then return '搜索期' end
  if stage == 'escape' then return '逃脱期' end
  return nil
end

local function modeLabel(mode)
  if mode == 'cops_robber' then return '警察抓强盗' end
  if mode == 'derby' then return '德比' end
  if mode == 'hide_seek' then return '捉迷藏' end
  if mode == 'track_day' then return '赛道日' end
  return '自由驾驶'
end

-- 秒 → mm:ss
local function clockText(sec)
  local s = math.max(0, math.floor(tonumber(sec) or 0))
  return string.format('%d:%02d', math.floor(s / 60), s % 60)
end

-- 昵称太长（联机 id 有 37 字符）会把结算行撑出卡片 → 按 UTF-8 边界截断
local function shortName(s)
  s = tostring(s or '')
  if #s <= 8 then return s end
  local cut = 8
  while cut > 0 do
    local b = s:byte(cut + 1)
    if not b or b < 0x80 or b >= 0xC0 then break end
    cut = cut - 1
  end
  if cut <= 0 then cut = 8 end
  return s:sub(1, cut) .. '…'
end

-- 这个玩法是不是「按角色区分显示」的两种（警匪 / 捉迷藏）
local function isRoleMode(mode)
  return mode == 'cops_robber' or mode == 'hide_seek'
end

-- ---------------------------------------------------------------------------
-- 本局规则卡
--
-- 「限制」这件事最怕的是玩家不知道自己被限了什么，然后以为游戏坏了。
-- 所以这里把本局生效的规则**逐条列出来**：左对齐 + 蓝竖线 + 统一行距，
-- 跟其它卡片同一套视觉语言（深色卡 / 左侧身份色条 / 三级字号）。
--
-- 起跑相关的两条（空档锁定 / 车型要求）单独提亮：它们在开局阶段最需要被看见，
-- running 一开就自动降级成普通条目，不喧宾夺主。
--
-- ⚠️ blk() 要的是 ImVec4，COL 里的颜色是普通表 —— 一律用 v4() 包一层。
-- ---------------------------------------------------------------------------
local RULE_W = 260          -- 规则卡宽度（右上角右对齐）

-- 规则分类顺序（与 startrideMode 的 CAT_TEXT 对应）
local RULE_CATS = {
  { 'zone',    '位置' },
  { 'time',    '时间' },
  { 'vehicle', '车辆' },
  { 'view',    '视野' },
  { 'func',    '功能' },
}
-- 行预算：5 类 × 每条规则全铺开会顶出屏幕，超过就折成「另 N 项」
local RULE_MAX_ROWS = 18
local RULE_MAX_PER  = 3

local function rulesCard(dl, x, y, w, snap, maxH)
  -- 分五类：位置 / 时间 / 车辆 / 视野 / 功能。
  -- 正在发生的问题（出圈倒计时、车型不符）置顶并换警示色 —— 规则列表是「静态说明」，
  -- 而玩家真正需要一眼看到的是「我现在正在违规，还剩几秒」。
  local items = {}
  local function line(t, c) items[#items + 1] = { t = 'l', s = t, c = c } end

  -- ① 正在发生的问题
  if snap.zoneOut == true then
    local left = tonumber(snap.zoneGraceLeft)
    line(left and ('偏离活动区 · ' .. string.format('%.0f', left) .. ' 秒后处罚')
                 or '偏离活动区', COL.danger)
  end
  if snap.vehicleOk == false then
    line('车型不符 · ' .. tostring(snap.vehicleName or '未知'), COL.warn)
  end

  -- ② 按分类整理规则（snap.ruleItems 是 startrideMode 按 RULE_DEFS 派生的结构化清单）
  local byCat, total = {}, 0
  local function pushRule(cat, text)
    local c = tostring(cat or 'func')
    local arr = byCat[c]
    if not arr then arr = {} byCat[c] = arr end
    arr[#arr + 1] = { text = text }
    total = total + 1
  end
  local src = snap.ruleItems
  if type(src) == 'table' and #src > 0 then
    for i = 1, #src do
      local it = src[i]
      if type(it) == 'table' then pushRule(it.cat, it.text) end
    end
  else
    -- 兼容只有纯文本 rules 的旧快照（分不出类别，统一落到「功能」）
    local rl = snap.rules
    if type(rl) == 'table' then
      for i = 1, #rl do pushRule('func', rl[i]) end
    end
  end

  -- ③ 排成扁平行列表（分类标题 + 实时数据行 + 规则行）
  local plan = {}
  for ci = 1, #RULE_CATS do
    local key, label = RULE_CATS[ci][1], RULE_CATS[ci][2]
    local arr = byCat[key]

    -- 实时数据行：规则是「本局限什么」，这一行是「此刻什么情况」，放分类最前面
    local live = nil
    local liveCol = COL.dim
    if key == 'zone' then
      local radius = tonumber(snap.zoneRadius) or 0
      if radius > 0 then
        local s = string.format('活动圈 %.0f m', radius)
        local d = tonumber(snap.zoneDist)
        if d then s = s .. string.format(' · 距圆心 %.0f m', d) end
        -- 出圈后果直接写在活动圈这一行：玩家最想知道「跑出去会怎样」
        if (tonumber(snap.zoneEliminate) or 0) > 0 then
          s = s .. ' · 出圈淘汰'
        else
          s = s .. ' · 出圈拉回'
        end
        live = s
      end
    elseif key == 'time' then
      local tot = tonumber(snap.roundSeconds) or 0
      if tot > 0 then
        local left = tonumber(snap.roundLeft)
        live = left and ('剩余 ' .. clockText(left) .. ' / ' .. clockText(tot))
                    or ('上限 ' .. clockText(tot))
        if left and left <= 30 then liveCol = COL.warn end
      end
    elseif key == 'vehicle' then
      if snap.vehicleRequired == true then
        local s = '要求 ' .. tostring(snap.vehicleTypes or '')
        local vn = tostring(snap.vehicleName or '')
        if vn ~= '' then s = s .. ' · 当前 ' .. vn end
        live = s
        if snap.vehicleOk == false then liveCol = COL.warn end
      end
    end

    local n = (arr and #arr or 0) + (live and 1 or 0)
    if n > 0 then
      local head = label
      if arr and #arr > 0 then head = label .. ' · ' .. tostring(#arr) end
      plan[#plan + 1] = { t = 'h', s = head }
      if live then plan[#plan + 1] = { t = 'l', s = live, c = liveCol } end

      if arr then
        local shown = 0
        for i = 1, #arr do
          if shown >= RULE_MAX_PER then
            plan[#plan + 1] = { t = 'l', s = '另 ' .. tostring(#arr - shown) .. ' 项', c = COL.dim }
            break
          end
          plan[#plan + 1] = { t = 'l', s = tostring(arr[i].text or ''), c = COL.dim }
          shown = shown + 1
        end
      end
    end
  end

  -- 起跑阶段的空档锁：running 之后自动消失，所以放最后
  if snap.gearLocked == true then
    plan[#plan + 1] = { t = 'l', s = '空档锁定 · 起跑自动挂一档', c = COL.warn }
  end

  -- ④ 行预算裁剪（保留前 N-1 行，最后一行换成「另 N 项」）
  if #plan > RULE_MAX_ROWS then
    local cut = #plan - (RULE_MAX_ROWS - 1)
    for i = #plan, RULE_MAX_ROWS + 1, -1 do plan[i] = nil end
    plan[RULE_MAX_ROWS] = { t = 'l', s = '另 ' .. tostring(cut) .. ' 项', c = COL.dim }
  end
  -- ⑤ 高度预算：右下角有模式组件面板时，规则卡必须让出空间。
  --    1080p 不缺高度，1440×900 以下的窗口会明显撞上 —— 这里按「每行都当分组标题」
  --    保守估算，超了就和行预算一样折成「另 N 项」。
  local budget = tonumber(maxH)
  if budget and budget > 0 then
    local rowWorst = LH.L3 + 6
    local room = math.floor((budget - (TUNE.padY * 2 + LH.L1 + 8)) / rowWorst)
    if room < 4 then room = 4 end
    if #plan > room then
      local cut = #plan - (room - 1)
      for i = #plan, room + 1, -1 do plan[i] = nil end
      plan[room] = { t = 'l', s = '另 ' .. tostring(cut) .. ' 项', c = COL.dim }
    end
  end
  for i = 1, #plan do items[#items + 1] = plan[i] end

  if #items == 0 then return 0 end

  local lineH, headH = LH.L3 + 3, LH.L3 + 6
  local h = TUNE.padY * 2 + LH.L1 + 8
  for _, it in ipairs(items) do
    h = h + ((it.t == 'h') and headH or lineH)
  end

  card(dl, x, y, w, h, COL.accent)

  local tx = x + TUNE.padX
  local ty = y + TUNE.padY
  -- 标题带模式名：同一个「本局规则」栏在五个模式里的内容完全不同，
  -- 不写模式名玩家会以为规则没变（切模式后数字变了但看不出为什么）。
  local title = tostring(snap.packLabel or '')
  if title == '' then title = modeLabel(snap.mode) end
  textAt(tx, ty, title .. ' · 本局规则 ' .. tostring(total) .. ' 项', COL.accent, FS.L1)
  -- 右上角补「功能 N / 位置 M」：玩家说的「功能限制 + 位置限制」两类分开计数
  local nf = tonumber(snap.packFuncCount) or 0
  local np = tonumber(snap.packPosCount) or 0
  if nf + np > 0 then
    textAt(tx + w - TUNE.padX - 96, ty + 3,
           string.format('功能 %d · 位置 %d', nf, np), COL.dim, FS.L3)
  end

  local ly = ty + LH.L1 + 8
  for _, it in ipairs(items) do
    if it.t == 'h' then
      -- 分组标题：上下各留一点呼吸，靠字色区分层级（不做加粗，HUD 字重不可控）
      textAt(tx, ly + 2, it.s, COL.accent, FS.L3)
      ly = ly + headH
    else
      local col = it.c or COL.dim
      -- 2px 竖线：杂志编辑式版式，同时把警示行从同色文字里拎出来
      blk(dl, tx, ly + 1, 2, lineH - 4, v4(col), 1)
      textAt(tx + 8, ly, it.s, col, FS.L3)
      ly = ly + lineH
    end
  end
  return h
end

-- ---------------------------------------------------------------------------
-- 主渲染
--
-- snap  = StartRideMode.snapshot()
-- extra = { finishAgeMs, winnerName, ... }
-- ---------------------------------------------------------------------------
-- 模式组件面板：右下角常驻。
-- 几何口径：宽 300，行高与主卡一致；高度由组件自己算（都是 5 行骨架），
-- 这里先按同一公式把 y 锚到底边，保证四个组件的面板下沿对齐。
local PACK_W, PACK_ROWS = 300, 5

-- 右下组件面板占掉的高度（含与规则卡之间的间隙）。
-- 没有组件（自由驾驶）/ 大厅阶段返回 0 —— 这两种情况规则卡可以铺到底。
local function packReserve(snap)
  if not PACK_MODS[snap.mode] then return 0 end
  if tostring(snap.phase or '') == 'lobby' then return 0 end
  return TUNE.padY * 2 + LH.L1 + (LH.L3 + 6) * PACK_ROWS + 12
end
local function packPanel(dl, snap, vw, vh)
  local mod = PACK_MODS[snap.mode]
  if not mod then return end
  if tostring(snap.phase or '') == 'lobby' then return end
  local rowH = LH.L3 + 6
  local h = TUNE.padY * 2 + LH.L1 + rowH * PACK_ROWS
  local ctx = {
    dl = dl, im = im,
    COL = COL, FS = FS, LH = LH, TUNE = TUNE,
    card = card, barFill = barFill, textAt = textAt, textSame = textSame,
    x = vw - TUNE.margin - PACK_W,
    y = vh - TUNE.margin - h,
    w = PACK_W,
  }
  pcall(mod.panel, ctx, snap)
end

function M.render(snap, extra)
  if type(snap) ~= 'table' then return end
  -- ⚠️ ui_imgui 没就绪时直接跳过：不能画就不画，绝不能让 HUD 反过来把联机拖崩。
  --    （模块被 require 的时机早于图形层初始化，这个判定是必要的兜底。）
  if type(im) ~= 'table' then return end
  -- 除自由驾驶外，每个模式都有自己的 HUD（赛道日以前被这里挡在门外）
  if snap.mode == nil or snap.mode == '' or snap.mode == 'free_drive' then return end
  if snap.phase == 'lobby' then return end

  extra = extra or {}

  local opened = false
  local ok, err = pcall(function()
    local vw, vh = 1920, 1080
    pcall(function()
      local vp = im.GetMainViewport()
      if vp then vw, vh = vp.Size.x, vp.Size.y end
    end)

    im.SetNextWindowPos(im.ImVec2(0, 0), im.Cond_Always)
    im.SetNextWindowSize(im.ImVec2(vw, vh), im.Cond_Always)   -- 见文件头「坑」
    im.SetNextWindowBgAlpha(0)

    local flags = im.WindowFlags_NoTitleBar + im.WindowFlags_NoResize
      + im.WindowFlags_NoScrollbar + im.WindowFlags_NoMove
      + im.WindowFlags_NoCollapse + im.WindowFlags_NoSavedSettings
      + im.WindowFlags_NoInputs + im.WindowFlags_NoFocusOnAppearing
      + im.WindowFlags_NoBringToFrontOnFocus + im.WindowFlags_NoBackground

    if not im.Begin('##sr_modehud', im.BoolPtr(false), flags) then
      im.End()
      return
    end
    opened = true

    local dl = nil
    pcall(function() dl = fnGetDrawList() end)

    local rcol = roleColor(snap.role)
    local M0 = TUNE.margin
    local CARD_H = 132

    -- ============ 左上：身份卡 ============
    --
    -- 三级字号：L1 角色名（角色色）· 模式名（灰）
    --           L2 阶段/倒计时（白/黄）
    --           L3 目标说明（灰）
    local cx, cy = M0, M0
    card(dl, cx, cy, TUNE.cardW, CARD_H, rcol)

    -- 第一行：L1 标题
    local tx = cx + TUNE.padX
    local ty = cy + TUNE.padY
    textAt(tx, ty, roleLabel(snap.role or (snap.mode == 'track_day' and 'driver' or nil)), rcol, FS.L1)
    textSame('· ' .. modeLabel(snap.mode), COL.dim, FS.L3)

    -- 右上角：回合数（L3）
    textAt(cx + TUNE.cardW - 76, ty + 3, '第 ' .. tostring(snap.round or 0) .. ' 回合',
           COL.dim, FS.L3)

    -- 第二行：L2 阶段
    local line2 = ty + LH.L1 + 4
    if snap.mode == 'hide_seek' and snap.phase == 'running' then
      -- 捉迷藏：running 里再分「躲藏期 / 搜索期」，这是玩家最需要知道的状态
      local sl = stageLabel(snap.stage) or '进行中'
      local scol = (snap.stage == 'escape') and COL.warn
        or (snap.stage == 'seek') and COL.danger or COL.hider
      textAt(tx, line2, sl, scol, FS.L2)
      if snap.outsideSeconds then
        textSame('返回区域 ' .. tostring(snap.outsideSeconds), COL.danger, FS.L2)
      elseif snap.stageSecondsLeft then
        textSame(clockText(snap.stageSecondsLeft), COL.text, FS.L2)
      end
    elseif snap.phase == 'countdown' and snap.secondsLeft then
      textAt(tx, line2, string.format('%.0f 秒后开始', snap.secondsLeft), COL.warn, FS.L2)
    elseif snap.phase == 'running' and snap.roundLeft then
      local left = math.max(0, tonumber(snap.roundLeft) or 0)
      textAt(tx, line2, '剩余 ' .. clockText(left),
             (left <= 30) and COL.danger or COL.text, FS.L2)
    else
      textAt(tx, line2, phaseLabel(snap.phase), COL.text, FS.L2)
    end

    -- 第三行：L3 目标说明
    local line3 = line2 + LH.L2 + 3
    if snap.mode == 'cops_robber' then
      local goal = (snap.role == 'robber')
        and '甩掉警察，撑到回合结束'
        or ('贴住强盗 ' .. string.format('%.0f', (snap.captureHoldMs or 5000) / 1000) .. ' 秒')
      textAt(tx, line3, '目标 · ' .. goal, COL.dim, FS.L3)
    elseif snap.mode == 'hide_seek' then
      local goal
      if snap.stage == 'escape' then
        goal = (snap.role == 'seeker') and '守住逃脱点，别放跑躲藏者' or '跑到逃脱点即获胜'
      else
        goal = (snap.role == 'seeker') and '找出所有躲藏者' or '别被找到'
      end
      textAt(tx, line3, '目标 · ' .. goal, COL.dim, FS.L3)
    elseif snap.mode == 'derby' then
      -- 淘汰后「目标 · 活到最后」已经不成立，而且车被冻住了。
      -- 不明说的话玩家会以为游戏卡死 —— 直接改说状态（只改这一句，位置不动）。
      if snap.eliminated and snap.phase == 'running' then
        textAt(tx, line3, '已淘汰 · 观战中，等待本局结束', COL.danger, FS.L3)
      else
        textAt(tx, line3, '目标 · 活到最后', COL.dim, FS.L3)
      end
    end

    -- 模式准入提示（警匪/德比）：开局前给活动半径的预期，车型不符给警告。
    -- 只做「同行追加」：不占新行、不动卡片高度，避免把下面进度条顶下去。
    if snap.mode == 'cops_robber' or snap.mode == 'derby' then
      if (tonumber(snap.zoneRadius) or 0) > 0 and snap.phase ~= 'running' then
        textSame(' · 活动圈 ' .. string.format('%.0f', snap.zoneRadius) .. 'm', COL.dim, FS.L3)
      end
      if snap.vehicleOk == false then
        textSame(' · 车型不符', COL.warn, FS.L3)
      end
    end

    -- ============ 顶部居中：准入警告横幅 ============
    -- 出圈（危险）优先、车型不符（警告）其次；两条不同时抢同一行。
    -- 放顶部居中而不是卡片里：这是「你正在违规」类提示，必须在视野正中。
    local banY = vh * 0.15
    local zoneWarn = (snap.mode == 'cops_robber' or snap.mode == 'derby')
      and snap.phase == 'running' and snap.zoneOut == true
    if zoneWarn then
      local msg = '偏离活动区'
      local w1 = im.CalcTextSize(msg).x
      textAt(vw * 0.5 - w1 * 0.5, banY, msg, COL.danger, FS.L1)
      if snap.zoneGraceLeft then
        local sub = string.format('%.0f', math.ceil(snap.zoneGraceLeft)) .. ' 秒后'
          .. ((tonumber(snap.zoneEliminate) or 0) > 0 and '淘汰' or '拉回边界')
        local w2 = im.CalcTextSize(sub).x
        textAt(vw * 0.5 - w2 * 0.5, banY + LH.L1 + 2, sub, COL.warn, FS.L2)
      end
      banY = banY + LH.L1 + LH.L2 + 8
    end
    if (snap.mode == 'cops_robber' or snap.mode == 'derby') and snap.vehicleOk == false then
      local msg = '车型不符 · 请更换为 ' .. tostring(snap.vehicleTypes or '')
      local w1 = im.CalcTextSize(msg).x
      textAt(vw * 0.5 - w1 * 0.5, banY, msg, COL.warn, FS.L2)
    end

    -- ============ 抓捕 / 搜索 / 车损 进度条 ============
    local barY = cy + CARD_H - TUNE.padY - TUNE.barH
    local innerW = TUNE.cardW - TUNE.padX * 2
    if snap.mode == 'cops_robber' then
      local hold = (snap.captureHoldMs or 5000)
      local cur = (snap.captureMs or 0)
      local ratio = clamp01(cur / math.max(1, hold))
      barFill(dl, cx + TUNE.padX, barY, innerW, TUNE.barH, ratio, COL.ok)
      local label = (snap.role == 'robber') and '正在被捕' or '抓捕中'
      if ratio > 0 then
        textAt(cx + TUNE.padX, barY - TUNE.barGap, label, COL.ok, FS.L3)
        textAt(cx + TUNE.padX + 64, barY - TUNE.barGap,
               string.format('%.1f', cur / 1000) .. ' 秒', COL.text, FS.L2)
      else
        textAt(cx + TUNE.padX, barY - TUNE.barGap,
               (snap.role == 'robber') and '未被贴身' or '尚未贴身', COL.dim, FS.L3)
      end
    elseif snap.mode == 'hide_seek' then
      local ratio = clamp01(tonumber(snap.detection) or 0)
      barFill(dl, cx + TUNE.padX, barY, innerW, TUNE.barH, ratio, COL.seeker)
      if ratio > 0.01 then
        local who = (snap.role == 'hider') and '你被锁定' or '发现中'
        textAt(cx + TUNE.padX, barY - TUNE.barGap, who, COL.seeker, FS.L3)
        local band = snap.proximity
        local note = (band == 'near' and '极近') or (band == 'middle' and '接近')
          or (band == 'far' and '远处') or ''
        textAt(cx + TUNE.padX + 64, barY - TUNE.barGap,
               string.format('%.0f%%', ratio * 100) .. (note ~= '' and ('  ' .. note) or ''),
               COL.text, FS.L2)
      else
        textAt(cx + TUNE.padX, barY - TUNE.barGap,
               (snap.role == 'seeker') and '尚未发现目标' or '暂时安全', COL.dim, FS.L3)
      end
    elseif snap.mode == 'derby' then
      -- 车损条：越满越危险（黄 → 红）
      local limit = math.max(1, snap.derbyLimit or 8000)
      local dmg = snap.derbyDamage or 0
      local ratio = clamp01(dmg / limit)
      local col = (ratio < 0.5) and COL.warn or COL.danger
      barFill(dl, cx + TUNE.padX, barY, innerW, TUNE.barH, ratio, col)
      textAt(cx + TUNE.padX, barY - TUNE.barGap, '车损', COL.dim, FS.L3)
      textAt(cx + TUNE.padX + 64, barY - TUNE.barGap,
             string.format('%.0f / %.0f', dmg, limit), col, FS.L2)
      -- 右上角：存活人数（L2，跟着同一行）
      textAt(cx + TUNE.cardW - 92, barY - TUNE.barGap,
             '存活 ' .. tostring(snap.derbyAlive or 0) .. ' 人', COL.text, FS.L3)
    end

    -- 捉迷藏：卡片右下角补「剩余躲藏者」
    if snap.mode == 'hide_seek' then
      textAt(cx + TUNE.cardW - 108, barY - TUNE.barGap,
             '剩余 ' .. tostring(snap.hidersLeft or 0) .. '/' .. tostring(snap.hiderTotal or 0),
             COL.hider, FS.L3)
    end

    -- ============ 左下：通用卡片高（够放仪表盘 / 强度条） ============
    local LOW_H = 154
    local ly = vh - M0 - LOW_H

    -- ---- 左下：警察雷达（仅警察 + running）----
    if snap.mode == 'cops_robber' and snap.role == 'cop' and snap.phase == 'running' then
      card(dl, M0, ly, TUNE.radarW, LOW_H, COL.cop)

      local rx, ry = M0 + TUNE.padX, ly + TUNE.padY
      textAt(rx, ry, '目标雷达', COL.cop, FS.L1)
      if snap.robberDist then
        textSame(string.format('%.0f m', snap.robberDist), COL.text, FS.L2)
      else
        textSame('无信号', COL.dim, FS.L3)
      end

      -- 仪表盘：半径按卡宽自适应，水平居中
      local gcx = M0 + TUNE.radarW * 0.5
      local gcy = ly + LOW_H - 62
      local bearing = snap.robberBearing
      local deg = bearing and math.deg(bearing) or nil
      gauge(dl, gcx, gcy, TUNE.gaugeR, deg, COL.robber)

      -- 底部方位文字（居中）
      local dirTxt
      if deg then
        local absDeg = math.abs(deg)
        if absDeg <= TUNE.radarHalfDeg then
          dirTxt = '正前方'
        elseif deg < 0 then
          dirTxt = '左 ' .. string.format('%.0f', absDeg) .. '°'
        else
          dirTxt = '右 ' .. string.format('%.0f', absDeg) .. '°'
        end
      else
        dirTxt = '目标不在范围内'
      end
      local dw = im.CalcTextSize(dirTxt).x
      textAt(gcx - dw * 0.5, ly + LOW_H - 20, dirTxt,
             deg and COL.text or COL.dim, FS.L3)
    end

    -- ---- 左下：强盗威胁感知（仅强盗）----
    -- 和警察雷达镜像对称：强盗不知道自己「该往哪追」，但必须知道自己「被谁盯上」。
    if snap.mode == 'cops_robber' and snap.role == 'robber' then
      local th = snap.threat
      -- 左侧色条随威胁等级变色（绿 → 黄 → 红）—— 这里「红」是危险语义，
      -- 不是涨跌语义，属于全局蓝色主题下的告警色，允许使用。
      local lvl = th and th.level or 0
      local barCol = { 0.35 + lvl * 0.55, 0.72 - lvl * 0.52, 0.45 - lvl * 0.30, 1 }
      card(dl, M0, ly, TUNE.radarW, LOW_H, barCol)

      local rx, ry = M0 + TUNE.padX, ly + TUNE.padY
      textAt(rx, ry, '被追捕', COL.robber, FS.L1)
      if th and th.signal and th.dist then
        textSame(string.format('%.0f m', th.dist), COL.text, FS.L2)
        textSame(th.label or '', barCol, FS.L3)
      else
        textSame('暂无信号', COL.dim, FS.L3)
      end

      -- 威胁强度条（高光进度条）
      local trackX = M0 + TUNE.padX
      local trackY = ry + LH.L1 + 12
      local trackW = TUNE.radarW - TUNE.padX * 2
      barFill(dl, trackX, trackY, trackW, TUNE.barH, lvl, barCol)

      -- 说明行
      local note, noteCol
      if th and th.signal then
        local held = tonumber(th.captureHeldMs) or 0
        if held > 0 then
          local need = tonumber(th.captureHoldMs) or 5000
          note = '正在被捕 ' .. string.format('%.1f', held / 1000)
            .. ' / ' .. string.format('%.1f', need / 1000) .. ' 秒'
          noteCol = barCol
        else
          note = '警察在' .. (th.direction or '附近')
          noteCol = COL.dim
        end
      else
        note = '暂无警察信号'
        noteCol = COL.dim
      end
      textAt(trackX, trackY + TUNE.barH + 8, note, noteCol, FS.L3)

      -- 底部：威胁等级条（三段式小刻度，做出仪表感）
      local segW = (trackW - 8) / 3
      local si
      for si = 0, 2 do
        local on = (lvl > (si + 0.5) / 3)
        blk(dl, trackX + si * (segW + 4), trackY + TUNE.barH + 32, segW, 4,
            on and v4(barCol, 0.95) or im.ImVec4(1, 1, 1, 0.10), 2)
      end
      textAt(trackX + trackW - 56, trackY + TUNE.barH + 24, '威胁等级', COL.dim, FS.L3)
    end

    -- ---- 左下：捉迷藏 —— 搜寻/隐蔽卡 ----
    --
    -- 和警匪的雷达/威胁卡对称：
    --   搜索者需要知道「最近的躲藏者还有多远」（不然只能瞎转）
    --   躲藏者需要知道「最近的搜索者还有多远」（跑还是蹲）
    if snap.mode == 'hide_seek' and snap.phase == 'running' then
      local isSeeker = (snap.role == 'seeker')
      local barCol = isSeeker and COL.seeker or COL.hider
      card(dl, M0, ly, TUNE.radarW, LOW_H, barCol)

      local rx, ry = M0 + TUNE.padX, ly + TUNE.padY
      textAt(rx, ry, isSeeker and '搜寻雷达' or '隐蔽状态', barCol, FS.L1)
      if snap.targetDist then
        textSame(string.format('%.0f m', snap.targetDist), COL.text, FS.L2)
      else
        textSame(isSeeker and '附近没有目标' or '无人看见你', COL.dim, FS.L3)
      end

      -- 距离强度条：越近越满
      local trackX = M0 + TUNE.padX
      local trackY = ry + LH.L1 + 12
      local trackW = TUNE.radarW - TUNE.padX * 2
      local near = 1 - clamp01((snap.targetDist or 9999) / 400)
      barFill(dl, trackX, trackY, trackW, TUNE.barH, near, barCol)

      -- 阶段说明
      local note, noteCol
      if snap.stage == 'hiding' then
        if isSeeker then
          note = '躲藏期 · 你被冻结，等待放行'
        else
          note = '躲藏期 · 快找地方藏起来'
        end
        noteCol = COL.warn
      elseif snap.stage == 'seek' then
        if isSeeker then
          note = '搜索期 · 越靠近锁定越快'
        else
          note = '搜索期 · 别让搜索者贴住你'
        end
        noteCol = COL.dim
      elseif snap.stage == 'escape' then
        if isSeeker then
          note = '逃脱期 · 守住逃脱点'
        else
          note = '逃脱期 · 跑到逃脱点即获胜'
        end
        noteCol = COL.warn
      else
        note = ''
        noteCol = COL.dim
      end
      textAt(trackX, trackY + TUNE.barH + 8, note, noteCol, FS.L3)

      local sk = snap.skill
      if type(sk) == 'table' and sk.skill ~= '' and snap.stage ~= 'hiding' then
        local nm = (sk.skill == 'break') and '脱离' or '标记'
        local tip = sk.ready and ('Alt+H ' .. nm) or (tostring(sk.cooldownSeconds) .. 's ' .. nm)
        textAt(trackX + trackW - 74, trackY + TUNE.barH + 8, tip,
               sk.ready and barCol or COL.dim, FS.L3)
      end

      -- 距离标尺（做出仪表感：0 / 200 / 400 m）
      local marks = { '0', '200', '400' }
      local mi
      for mi = 1, 3 do
        local px = trackX + trackW * ((mi - 1) / 2) - 6
        textAt(px, trackY + TUNE.barH + 32, marks[mi], COL.dim, FS.L3)
      end
      textAt(trackX + TUNE.padX, trackY + TUNE.barH + 32,
             '近', COL.dim, FS.L3)
      textAt(trackX + trackW - 60, trackY + TUNE.barH + 32,
             '远', COL.dim, FS.L3)
    end

    -- ============ 右上：玩法积分（常驻，跨局累计） ============
    do
      local sc = extra.score or snap.score
      if type(sc) == 'table' and sc.enabled then
        local sx = vw - M0 - 200
        local sy = M0
        card(dl, sx, sy, 200, 52, COL.accent)
        textAt(sx + TUNE.padX, sy + TUNE.padY, '积分', COL.dim, FS.L3)
        textAt(sx + TUNE.padX + 62, sy + TUNE.padY - 3,
               tostring(sc.total or 0), COL.text, FS.L2)
        local gain = tonumber(sc.gain) or 0
        textAt(sx + TUNE.padX + 62, sy + TUNE.padY + LH.L2 - 1,
               '上局 +' .. tostring(gain), gain > 0 and COL.accent or COL.dim, FS.L3)
      end
    end

    -- ============ 右上：捉迷藏 —— 找到计数 ============
    if snap.mode == 'hide_seek' and snap.phase == 'running' then
      local wx = vw - M0 - 200
      local wy = M0 + 60
      card(dl, wx, wy, 200, 52, COL.hider)
      textAt(wx + TUNE.padX, wy + TUNE.padY, '已找到', COL.dim, FS.L3)
      local left = tonumber(snap.hidersLeft) or 0
      local fcol = left > 0 and COL.text or COL.ok
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY - 3,
             tostring(snap.foundCount or 0) .. ' 人', fcol, FS.L2)
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY + LH.L2 - 1,
             '剩 ' .. tostring(left) .. ' 人', COL.dim, FS.L3)
    end

    -- ============ 右上：复位预算提示（德比） ============
    if snap.mode == 'derby' and snap.phase == 'running' and snap.resetLimit then
      local wx = vw - M0 - 200
      local wy = M0 + 60
      card(dl, wx, wy, 200, 52, COL.warn)
      textAt(wx + TUNE.padX, wy + TUNE.padY, '原地复位', COL.dim, FS.L3)
      local left = math.max(0, (snap.resetLimit or 0) - (snap.resetUsed or 0))
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY - 3,
             tostring(left) .. ' 次', left > 0 and COL.text or COL.danger, FS.L2)
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY + LH.L2 - 1,
             '上限 ' .. tostring(snap.resetLimit or 0) .. ' 次', COL.dim, FS.L3)
    end

    -- ============ 右上：本局规则（常驻，开局阶段最需要被看见） ============
    --
    -- ⚠️ 位置贴着右上角往下排：积分卡占 y=M0（h=52），模式计数卡占 y=M0+60（h=52），
    --    这里从 y=M0+120 开始、宽 260 右对齐，不会跟它们叠在一起。
    do
      local rx = vw - TUNE.margin - RULE_W
      local top = TUNE.margin + 120
      -- 可用高度 = 窗口底 - 模式面板（含间隙）- 规则卡上沿
      local avail = vh - TUNE.margin - packReserve(snap) - top
      rulesCard(dl, rx, top, RULE_W, snap, avail)
    end

    -- ============ 右下：本模式专属组件 ============
    -- 每个模式一个独立 Lua 文件（startridePack*），绘制风格沿用本文件的语言。
    packPanel(dl, snap, vw, vh)

    -- ============ 中央：结算画面 ============
    if snap.phase == 'finished' then
      local age = extra.finishAgeMs or 0
      local enter = clamp01(age / TUNE.fadeInMs)
      local leave = clamp01((TUNE.fadeHoldMs - age) / TUNE.fadeOutMs)
      local alpha = smoothstep(enter) * leave

      if alpha > 0.01 then
        local cw, ch = 460, 178
        local bx = (vw - cw) * 0.5
        local by = (vh - ch) * 0.5

        local title
        local tcol = COL.accent
        if snap.mode == 'cops_robber' then
          if snap.winner == 'cop' then title = '警察胜利'; tcol = COL.cop
          elseif snap.winner == 'robber' then title = '强盗逃脱'; tcol = COL.robber
          else title = '回合结束' end
        elseif snap.mode == 'hide_seek' then
          if snap.winner == 'seeker' then title = '搜索方胜利'; tcol = COL.seeker
          elseif snap.winner == 'hider' then title = '躲藏方胜利'; tcol = COL.hider
          else title = '回合结束' end
        else
          if extra.winnerName and extra.winnerName ~= '' then
            title = extra.winnerName .. ' 获胜'
          else
            title = '德比结束'
          end
          tcol = COL.ok
        end

        -- 结算卡：底部柔光 + 深底 + 顶部渐变条（用胜负色）
        if dl then
          local glowCol = v4(shade(tcol, -0.4), 0.12 * alpha)
          blk(dl, bx - 6, by - 6, cw + 12, ch + 12, glowCol, 18)
          blk(dl, bx - 3, by - 3, cw + 6, ch + 6, v4(shade(tcol, -0.3), 0.16 * alpha), 15)
        end
        blk(dl, bx, by, cw, ch, im.ImVec4(0.045, 0.062, 0.090, 0.92 * alpha), 14)
        if dl then
          -- 顶部 3px 胜负色渐变条
          grad(dl, bx, by, cw, 3, v4(shade(tcol, -0.2), alpha),
               v4(shade(tcol, 0.30), alpha), 2)
          stroke(dl, bx, by, cw, ch, im.ImVec4(1, 1, 1, 0.10 * alpha), 14, 1)
        end

        -- L1 标题
        textAt(bx + 28, by + 26, title, tcol, FS.L1 + 0.16, alpha)

        -- 右上角：本局得分 + 累计（积分卡结算期仍在右上，这里再点一次「涨了多少」）
        local sc = extra.score or snap.score
        if type(sc) == 'table' and sc.enabled then
          local gain = tonumber(sc.gain) or 0
          local gtxt = (gain > 0 and '+' or '') .. tostring(gain) .. ' 分'
          local gw = 0
          pcall(function()
            im.SetWindowFontScale(FS.L1 + 0.30)
            gw = im.CalcTextSize(gtxt).x
            im.SetWindowFontScale(1.0)
          end)
          textAt(bx + cw - 28 - gw, by + 26, gtxt,
                 gain > 0 and COL.accent or COL.dim, FS.L1 + 0.30, alpha)
          local ttxt = '累计 ' .. tostring(sc.total or 0)
          local tw = 0
          pcall(function()
            im.SetWindowFontScale(FS.L3)
            tw = im.CalcTextSize(ttxt).x
            im.SetWindowFontScale(1.0)
          end)
          textAt(bx + cw - 28 - tw, by + 64, ttxt, COL.dim, FS.L3, alpha)
        end

        -- 自己的结果
        local mine
        if snap.mode == 'cops_robber' then
          if snap.role == 'robber' then
            mine = (snap.winner == 'robber') and '你成功逃脱' or '你被抓住了'
          else
            mine = (snap.winner == 'cop') and '你抓住了强盗' or '强盗逃掉了'
          end
        elseif snap.mode == 'hide_seek' then
          local iWon = (snap.role == snap.winner)
          if snap.role == 'seeker' then
            mine = iWon and '你找出了所有人' or '没能在时间内找齐'
          else
            mine = iWon and '你成功躲到了最后' or '你被找到了'
          end
        else
          mine = snap.eliminated and '你被淘汰' or '你存活到了最后'
        end
        textAt(bx + 28, by + 62, mine, COL.text, FS.L2, alpha)

        -- 分隔线
        blk(dl, bx + 28, by + 92, cw - 56, 1, im.ImVec4(1, 1, 1, 0.10 * alpha), 0)

        -- 副行（L3 说明）
        local sub
        if snap.mode == 'hide_seek' then
          local why = (snap.reason == 'escaped') and '有人逃脱'
            or (snap.reason == 'time_up') and '时间耗尽'
            or (snap.reason == 'outside') and '有人离开区域' or '全部被找到'
          sub = why .. ' · 找到 ' .. tostring(snap.foundCount or 0) .. ' 人'
            .. ' · 剩余躲藏者 ' .. tostring(snap.hidersLeft or 0)
            .. ' · 即将返回等待'
        else
          local why = (snap.finishReason == 'time_up' and '撑满全场')
            or (snap.finishReason == 'last_standing' and '最后幸存')
            or (snap.finishReason == 'robber_outside' and '强盗离开活动区域') or ''
          sub = (why ~= '' and (why .. ' · ') or '') .. '即将返回等待…'
        end

        if snap.mode == 'derby' and type(extra.derbyOrder) == 'table' and #extra.derbyOrder > 0 then
          local parts = {}
          for i = 1, math.min(#extra.derbyOrder, 4) do
            local e = extra.derbyOrder[i]
            parts[#parts + 1] = tostring(i) .. '.' .. tostring(e.name or e.id)
              .. (e.eliminated and '已淘汰' or '')
          end
          textAt(bx + 28, by + 122, table.concat(parts, '  '), COL.dim, FS.L3, alpha)
        end
        textAt(bx + 28, by + 104, sub, COL.dim, FS.L3, alpha)

        -- 积分榜（前 4 名，带本局增量）
        if type(sc) == 'table' and sc.enabled
           and type(sc.board) == 'table' and #sc.board > 0 then
          local parts = {}
          for i = 1, math.min(#sc.board, 4) do
            local e = sc.board[i]
            local d = tonumber(e.delta) or 0
            parts[#parts + 1] = tostring(i) .. '.' .. shortName(e.name or e.id)
              .. ' ' .. tostring(e.score or 0)
              .. (d > 0 and ('(+' .. tostring(d) .. ')') or '')
          end
          textAt(bx + 28, by + 150, '积分榜  ' .. table.concat(parts, '    '),
                 COL.dim, FS.L3, alpha)
        end
      end
    end

    -- ============ 中央上：倒数大字 ============
    if snap.phase == 'countdown' and snap.secondsLeft then
      local sec = math.max(0, math.ceil(snap.secondsLeft))
      local txt = tostring(sec)
      local baseW = im.CalcTextSize(txt).x
      pcall(function() im.SetWindowFontScale(3.4) end)
      local w2 = im.CalcTextSize(txt).x
      local px = (vw - w2) * 0.5
      local py = vh * 0.22
      -- 柔和辉光（叠三层同字）
      local gi
      for gi = 1, 3 do
        local sp = gi * 2
        im.SetCursorPos(im.ImVec2(px + sp, py))
        im.TextColored(v4(COL.warn, 0.06), txt)
        im.SetCursorPos(im.ImVec2(px - sp, py))
        im.TextColored(v4(COL.warn, 0.06), txt)
      end
      im.SetCursorPos(im.ImVec2(px, py))
      im.TextColored(v4(COL.warn), txt)
      pcall(function() im.SetWindowFontScale(1.0) end)
      -- 底部提示
      local hint = '准备开始'
      local hw = im.CalcTextSize(hint).x
      textAt((vw - hw) * 0.5, py + 62, hint, COL.dim, FS.L2)
    end
  end)

  if opened then pcall(function() im.End() end) end
  if not ok then
    pcall(function()
      log('W', 'startride', '[StartRide] 玩法 HUD 绘制出错: ' .. tostring(err))
    end)
  end
end

function M.tune() return TUNE end

return M
