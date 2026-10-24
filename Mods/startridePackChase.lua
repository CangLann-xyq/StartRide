-- StartRide 模式组件 · 警匪追逐（cops_robber）
--
-- 职责：把「警匪局专属的位置限制 + 追捕信息」画成一块独立面板，右下角常驻。
--   和模式本体（startrideMode）解耦：本文件只读 snapshot，不碰任何玩法状态。
--
-- 视觉沿用 StartRide 主 HUD 的语言：玻璃深底 + 左侧 3px 强调条 + 左对齐三级字号。
-- ⚠️ 不参考/不复制其它联机插件的面板样式（用户明确要求「绘制不要和它一样」）。
--
-- 绘制原语由 HUD 通过 ctx 注入（blk / card / barFill / gauge / textAt ...），
-- 这样本文件不需要自己 require ui_imgui，也不会把绘图失败带进玩法。

local M = { id = 'cops_robber', title = '警匪追逐' }

local function fmtM(v)
  local n = tonumber(v)
  if not n then return '—' end
  return string.format('%.0f m', n)
end

local function fmtSec(v)
  local n = tonumber(v)
  if not n then return nil end
  if n >= 60 then
    return string.format('%d:%02d', math.floor(n / 60), math.floor(n % 60))
  end
  return string.format('%.0f 秒', n)
end

-- 角度 → 人话（相对车头）
local function bearingText(rad, halfDeg)
  if not rad then return nil end
  local deg = math.deg(rad)
  local absDeg = math.abs(deg)
  local half = tonumber(halfDeg) or 15
  if absDeg <= half then return '正前方' end
  if absDeg >= 150 then return '正后方' end
  return (deg < 0 and '左 ' or '右 ') .. string.format('%.0f', absDeg) .. '°'
end

-- ctx: { dl, x, y, w, im, blk, card, barFill, textAt, textSame, COL, FS, LH, TUNE }
function M.panel(ctx, snap)
  if type(ctx) ~= 'table' or type(snap) ~= 'table' then return 0 end
  if snap.mode ~= M.id then return 0 end
  local phase = tostring(snap.phase or '')
  if phase == 'lobby' then return 0 end

  local COL, FS, LH = ctx.COL, ctx.FS, ctx.LH
  local dl, x, y, w = ctx.dl, ctx.x, ctx.y, ctx.w
  local padX = (ctx.TUNE and ctx.TUNE.padX) or 16
  local padY = (ctx.TUNE and ctx.TUNE.padY) or 13
  local isCop = (snap.role == 'cop')
  local accent = isCop and COL.cop or COL.robber

  local rowH = LH.L3 + 6
  local h = padY * 2 + LH.L1 + rowH * 5
  ctx.card(dl, x, y, w, h, accent)

  local tx = x + padX
  local ty = y + padY
  ctx.textAt(tx, ty, '追捕要素', accent, FS.L1)
  ctx.textSame(isCop and '· 警方' or '· 强盗', COL.dim, FS.L3)

  local ly = ty + LH.L1 + 2

  -- ① 位置限制：活动圈 + 出圈后果
  local radius = tonumber(snap.zoneRadius) or 0
  if radius > 0 then
    ctx.textAt(tx, ly, '活动圈', COL.dim, FS.L3)
    ctx.textAt(tx + 62, ly, fmtM(radius),
              (snap.zoneOut == true) and COL.warn or COL.text, FS.L3)
    if snap.zoneOut == true and snap.zoneGraceLeft then
      local left = fmtSec(snap.zoneGraceLeft)
      ctx.textAt(tx + w - padX - 96, ly, (left or '—') .. '后拉回', COL.warn, FS.L3)
    end
  else
    ctx.textAt(tx, ly, '活动圈', COL.dim, FS.L3)
    ctx.textAt(tx + 62, ly, '开放（无限制）', COL.dim, FS.L3)
  end
  ly = ly + rowH

  -- ② 本模式的功能底线（房主也关不掉的那几条）
  ctx.textAt(tx, ly, '底线锁定', COL.dim, FS.L3)
  ctx.textAt(tx + 62, ly, '传送 · 换车 · 下车', COL.text, FS.L3)
  ly = ly + rowH

  -- ③ 追捕信息：警察看目标方位，强盗看最近威胁
  if isCop then
    ctx.textAt(tx, ly, '可疑目标', COL.dim, FS.L3)
    local dist = tonumber(snap.robberDist)
    if dist then
      ctx.textAt(tx + 62, ly, fmtM(dist), COL.text, FS.L3)
      local bt = bearingText(snap.robberBearing, ctx.TUNE and ctx.TUNE.radarHalfDeg)
      if bt then
        ctx.textAt(tx + 62, ly + rowH, '方位', COL.dim, FS.L3)
        ctx.textAt(tx + 62 + 38, ly + rowH, bt, COL.text, FS.L3)
      end
    else
      ctx.textAt(tx + 62, ly, '信号丢失', COL.dim, FS.L3)
      ctx.textAt(tx + 62, ly + rowH, '方位', COL.dim, FS.L3)
      ctx.textAt(tx + 62 + 38, ly + rowH, '超出范围', COL.dim, FS.L3)
    end
    ly = ly + rowH * 2
  else
    local th = snap.threat
    local lvl = (type(th) == 'table' and tonumber(th.level)) or 0
    local lvlText = (lvl >= 0.66 and '贴得很近') or (lvl >= 0.33 and '被跟上') or '暂时安全'
    local lvlCol = (lvl >= 0.66 and COL.danger) or (lvl >= 0.33 and COL.warn) or COL.dim
    ctx.textAt(tx, ly, '最近警察', COL.dim, FS.L3)
    ctx.textAt(tx + 62, ly, (type(th) == 'table' and fmtM(th.dist)) or '—', COL.text, FS.L3)
    ctx.textAt(tx, ly + rowH, '威胁', COL.dim, FS.L3)
    ctx.textAt(tx + 62, ly + rowH, lvlText, lvlCol, FS.L3)
    ly = ly + rowH * 2
  end

  -- ④ 抓捕进度：本局核心胜负条件，放在最底一行（不抢主卡的位置）
  local hold = math.max(1, tonumber(snap.captureHoldMs) or 5000)
  local cur = math.max(0, tonumber(snap.captureMs) or 0)
  local ratio = math.min(1, cur / hold)
  local barY = y + h - padY - (ctx.TUNE and ctx.TUNE.barH or 8)
  local innerW = w - padX * 2
  ctx.barFill(dl, tx, barY, innerW, (ctx.TUNE and ctx.TUNE.barH or 8), ratio,
              (ratio > 0) and COL.ok or COL.dim)
  ctx.textAt(tx, barY - 18,
             snap.captured and '已抓捕完成' or (isCop and '贴身计时' or '被贴身'),
             COL.dim, FS.L3)
  ctx.textAt(tx + innerW - 76, barY - 18,
             string.format('%.1f / %.1f s', cur / 1000, hold / 1000),
             (ratio > 0) and COL.text or COL.dim, FS.L3)
  return h
end

return M
