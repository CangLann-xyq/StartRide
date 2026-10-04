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
  if role == 'cop' then return '警察' end
  if role == 'robber' then return '强盗' end
  if role == 'seeker' then return '搜索者' end
  if role == 'hider' then return '躲藏者' end
  return '观战'
end

local function roleColor(role)
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
  return nil
end

local function modeLabel(mode)
  if mode == 'cops_robber' then return '警察抓强盗' end
  if mode == 'derby' then return '德比' end
  if mode == 'hide_seek' then return '捉迷藏' end
  return '自由驾驶'
end

-- 秒 → mm:ss
local function clockText(sec)
  local s = math.max(0, math.floor(tonumber(sec) or 0))
  return string.format('%d:%02d', math.floor(s / 60), s % 60)
end

-- 这个玩法是不是「按角色区分显示」的两种（警匪 / 捉迷藏）
local function isRoleMode(mode)
  return mode == 'cops_robber' or mode == 'hide_seek'
end

-- ---------------------------------------------------------------------------
-- 主渲染
--
-- snap  = StartRideMode.snapshot()
-- extra = { finishAgeMs, winnerName, ... }
-- ---------------------------------------------------------------------------
function M.render(snap, extra)
  if type(snap) ~= 'table' then return end
  -- ⚠️ ui_imgui 没就绪时直接跳过：不能画就不画，绝不能让 HUD 反过来把联机拖崩。
  --    （模块被 require 的时机早于图形层初始化，这个判定是必要的兜底。）
  if type(im) ~= 'table' then return end
  if snap.mode ~= 'cops_robber' and snap.mode ~= 'derby' and snap.mode ~= 'hide_seek' then return end
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
    textAt(tx, ty, roleLabel(snap.role), rcol, FS.L1)
    textSame('· ' .. modeLabel(snap.mode), COL.dim, FS.L3)

    -- 右上角：回合数（L3）
    textAt(cx + TUNE.cardW - 76, ty + 3, '第 ' .. tostring(snap.round or 0) .. ' 回合',
           COL.dim, FS.L3)

    -- 第二行：L2 阶段
    local line2 = ty + LH.L1 + 4
    if snap.mode == 'hide_seek' and snap.phase == 'running' then
      -- 捉迷藏：running 里再分「躲藏期 / 搜索期」，这是玩家最需要知道的状态
      local sl = stageLabel(snap.stage) or '进行中'
      local scol = (snap.stage == 'seek') and COL.danger or COL.hider
      textAt(tx, line2, sl, scol, FS.L2)
      if snap.stageSecondsLeft then
        textSame(clockText(snap.stageSecondsLeft), COL.text, FS.L2)
      end
    elseif snap.phase == 'countdown' and snap.secondsLeft then
      textAt(tx, line2, string.format('%.0f 秒后开始', snap.secondsLeft), COL.warn, FS.L2)
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
      local goal = (snap.role == 'seeker') and '找出所有躲藏者' or '别被找到'
      textAt(tx, line3, '目标 · ' .. goal, COL.dim, FS.L3)
    elseif snap.mode == 'derby' then
      textAt(tx, line3, '目标 · 活到最后', COL.dim, FS.L3)
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
      local hold = math.max(1, snap.findHoldMs or 2000)
      local cur = tonumber(snap.findMs) or 0
      local ratio = clamp01(cur / hold)
      barFill(dl, cx + TUNE.padX, barY, innerW, TUNE.barH, ratio, COL.seeker)
      if ratio > 0 then
        local who = (snap.role == 'hider') and '你被锁定' or '发现中'
        textAt(cx + TUNE.padX, barY - TUNE.barGap, who, COL.seeker, FS.L3)
        textAt(cx + TUNE.padX + 64, barY - TUNE.barGap,
               string.format('%.1f / %.1f 秒', cur / 1000, hold / 1000), COL.text, FS.L2)
      else
        textAt(cx + TUNE.padX, barY - TUNE.barGap,
               (snap.role == 'seeker') and '尚未贴近躲藏者' or '暂时安全', COL.dim, FS.L3)
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
          note = '搜索期 · 贴近躲藏者 '
            .. string.format('%.0f', (snap.findHoldMs or 2000) / 1000) .. ' 秒即可找到'
        else
          note = '搜索期 · 别让搜索者贴住你'
        end
        noteCol = COL.dim
      else
        note = ''
        noteCol = COL.dim
      end
      textAt(trackX, trackY + TUNE.barH + 8, note, noteCol, FS.L3)

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

    -- ============ 右上：捉迷藏 —— 找到计数 ============
    if snap.mode == 'hide_seek' and snap.phase == 'running' then
      local wx = vw - M0 - 200
      local wy = M0
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
      local wy = M0
      card(dl, wx, wy, 200, 52, COL.warn)
      textAt(wx + TUNE.padX, wy + TUNE.padY, '原地复位', COL.dim, FS.L3)
      local left = math.max(0, (snap.resetLimit or 0) - (snap.resetUsed or 0))
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY - 3,
             tostring(left) .. ' 次', left > 0 and COL.text or COL.danger, FS.L2)
      textAt(wx + TUNE.padX + 62, wy + TUNE.padY + LH.L2 - 1,
             '上限 ' .. tostring(snap.resetLimit or 0) .. ' 次', COL.dim, FS.L3)
    end

    -- ============ 中央：结算画面 ============
    if snap.phase == 'finished' then
      local age = extra.finishAgeMs or 0
      local enter = clamp01(age / TUNE.fadeInMs)
      local leave = clamp01((TUNE.fadeHoldMs - age) / TUNE.fadeOutMs)
      local alpha = smoothstep(enter) * leave

      if alpha > 0.01 then
        local cw, ch = 460, 150
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
          sub = '找到 ' .. tostring(snap.foundCount or 0) .. ' 人'
            .. ' · 剩余躲藏者 ' .. tostring(snap.hidersLeft or 0)
            .. ' · 即将返回等待'
        else
          sub = '即将返回等待…'
        end
        textAt(bx + 28, by + 104, sub, COL.dim, FS.L3, alpha)
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
