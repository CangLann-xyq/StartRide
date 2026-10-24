-- StartRide 模式组件 · 捉迷藏（hide_seek）
--
-- 独立面板（右下）：阶段 / 目标距离 / 找到计数 / 位置限制。
-- 躲藏方与搜索方看到不同的第二行（一个看「可疑搜索者」，一个看「最近躲藏者」）。
-- 只读 snapshot；绘制原语由 HUD 注入 ctx。

local M = { id = 'hide_seek', title = '捉迷藏' }

local function fmtM(v)
  local n = tonumber(v)
  if not n then return '—' end
  return string.format('%.0f m', n)
end

local function fmtSec(v)
  local n = tonumber(v)
  if not n then return '—' end
  if n >= 60 then return string.format('%d:%02d', math.floor(n / 60), math.floor(n % 60)) end
  return string.format('%.0f 秒', n)
end

local function stageText(st)
  local s = tostring(st or '')
  if s == 'hide' then return '躲藏期' end
  if s == 'seek' then return '搜索期' end
  if s == 'escape' then return '逃脱期' end
  return '进行中'
end

function M.panel(ctx, snap)
  if type(ctx) ~= 'table' or type(snap) ~= 'table' then return 0 end
  if snap.mode ~= M.id then return 0 end
  if tostring(snap.phase or '') == 'lobby' then return 0 end

  local COL, FS, LH = ctx.COL, ctx.FS, ctx.LH
  local dl, x, y, w = ctx.dl, ctx.x, ctx.y, ctx.w
  local padX = (ctx.TUNE and ctx.TUNE.padX) or 16
  local padY = (ctx.TUNE and ctx.TUNE.padY) or 13
  local isSeeker = (snap.role == 'seeker')
  local accent = isSeeker and COL.seeker or COL.hider

  local rowH = LH.L3 + 6
  local h = padY * 2 + LH.L1 + rowH * 5
  ctx.card(dl, x, y, w, h, accent)

  local tx = x + padX
  local ty = y + padY
  ctx.textAt(tx, ty, isSeeker and '搜寻侧' or '躲藏侧', accent, FS.L1)
  ctx.textSame('· ' .. stageText(snap.stage), COL.dim, FS.L3)

  local ly = ty + LH.L1 + 2

  -- ① 阶段与倒计时
  ctx.textAt(tx, ly, '阶段', COL.dim, FS.L3)
  ctx.textAt(tx + 46, ly, stageText(snap.stage), COL.text, FS.L3)
  local sl = snap.stageSecondsLeft
  if sl then
    ctx.textAt(tx + w - padX - 96, ly, '剩 ' .. fmtSec(sl), COL.dim, FS.L3)
  end
  ly = ly + rowH

  -- ② 目标距离（搜索者看躲藏者；躲藏者看最近搜索者）
  ctx.textAt(tx, ly, isSeeker and '最近目标' or '最近威胁', COL.dim, FS.L3)
  ctx.textAt(tx + 62, ly, fmtM(snap.targetDist), COL.text, FS.L3)
  ly = ly + rowH

  -- ③ 找到计数
  ctx.textAt(tx, ly, '已找到', COL.dim, FS.L3)
  ctx.textAt(tx + 62, ly, (tonumber(snap.foundCount) or 0) .. ' 人', COL.text, FS.L3)
  ctx.textAt(tx + w - padX - 96, ly,
             '剩 ' .. (tonumber(snap.hidersLeft) or 0) .. '/' .. (tonumber(snap.hiderTotal) or 0),
             COL.dim, FS.L3)
  ly = ly + rowH

  -- ④ 锁定进度（被发现的实时进度；搜索者没有这个概念时显示「未锁定」）
  local detect = math.min(1, math.max(0, tonumber(snap.detection) or 0))
  ctx.textAt(tx, ly, isSeeker and '发现进度' or '暴露度', COL.dim, FS.L3)
  ctx.barFill(dl, tx + 62, ly + 5, w - padX * 2 - 62 - 46, 4, detect, COL.seeker)
  ctx.textAt(tx + w - padX - 40, ly,
             string.format('%.0f%%', detect * 100),
             (detect > 0.01) and COL.seeker or COL.dim, FS.L3)
  ly = ly + rowH

  -- ⑤ 位置限制
  local radius = tonumber(snap.zoneRadius) or 0
  ctx.textAt(tx, ly, '活动圈', COL.dim, FS.L3)
  if radius > 0 then
    local tail = (snap.zoneOut == true) and ' · 正在圈外' or ' · 出圈拉回'
    ctx.textAt(tx + 62, ly, fmtM(radius) .. tail,
              (snap.zoneOut == true) and COL.warn or COL.text, FS.L3)
  else
    ctx.textAt(tx + 62, ly, '不设边界', COL.dim, FS.L3)
  end

  return h
end

return M
