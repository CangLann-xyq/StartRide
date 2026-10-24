-- StartRide 模式组件 · 赛道日（track_day）
--
-- 独立面板（右下）：阶段 / 回合剩余 / 圈速 / 位置限制。
-- 赛道日在旧版 HUD 里完全没有面板（render 直接跳过），这里补上：
--   这个模式最需要知道的恰恰是「还剩多久」和「被拉回赛道」。
--
-- ⚠️ 不编造圈速：真正的单圈成绩要先有「起终点计时门」（进/出判定半径），
--    现在没有，所以这一版显示的是**真实的**已跑时长 + 剩余时长，不是假圈速。
-- 只读 snapshot；绘制原语由 HUD 注入 ctx。

local M = { id = 'track_day', title = '赛道日' }

local function fmtSec(v)
  local n = tonumber(v)
  if not n then return nil end
  if n >= 60 then return string.format('%d:%02d', math.floor(n / 60), math.floor(n % 60)) end
  return string.format('%.0f 秒', n)
end

function M.panel(ctx, snap)
  if type(ctx) ~= 'table' or type(snap) ~= 'table' then return 0 end
  if snap.mode ~= M.id then return 0 end
  if tostring(snap.phase or '') == 'lobby' then return 0 end

  local COL, FS, LH = ctx.COL, ctx.FS, ctx.LH
  local dl, x, y, w = ctx.dl, ctx.x, ctx.y, ctx.w
  local padX = (ctx.TUNE and ctx.TUNE.padX) or 16
  local padY = (ctx.TUNE and ctx.TUNE.padY) or 13
  local accent = COL.accent

  local rowH = LH.L3 + 6
  local h = padY * 2 + LH.L1 + rowH * 5
  ctx.card(dl, x, y, w, h, accent)

  local tx = x + padX
  local ty = y + padY
  ctx.textAt(tx, ty, '赛道', accent, FS.L1)
  ctx.textSame('· 第 ' .. tostring(snap.round or 0) .. ' 回合', COL.dim, FS.L3)

  local ly = ty + LH.L1 + 2

  -- ① 阶段
  local phase = tostring(snap.phase or '')
  local phaseText = (phase == 'countdown') and '起跑倒计时'
    or (phase == 'running' and '计时中')
    or (phase == 'finished' and '已结束')
    or (phase == 'staging' and '就位中')
    or '准备'
  ctx.textAt(tx, ly, '状态', COL.dim, FS.L3)
  ctx.textAt(tx + 46, ly, phaseText, COL.text, FS.L3)
  ly = ly + rowH

  -- ② 起跑倒数（倒数阶段）/ 已跑时长（跑起来之后）
  if phase == 'countdown' then
    ctx.textAt(tx, ly, '起跑', COL.dim, FS.L3)
    ctx.textAt(tx + 46, ly, fmtSec(snap.secondsLeft) or '—', COL.text, FS.L3)
  else
    ctx.textAt(tx, ly, '已跑', COL.dim, FS.L3)
    ctx.textAt(tx + 46, ly, fmtSec(snap.elapsed) or '—', COL.text, FS.L3)
    local left = tonumber(snap.roundLeft)
    ctx.textAt(tx + w - padX - 96, ly, '剩余 ' .. (fmtSec(left) or '—'),
              (left and left <= 30) and COL.warn or COL.dim, FS.L3)
  end
  ly = ly + rowH

  -- ③ 剩余 / 复位预算（复位＝白送一次近道，所以这里要能看见还剩几次）
  ctx.textAt(tx, ly, phase == 'countdown' and '剩余' or '复位', COL.dim, FS.L3)
  if phase == 'countdown' then
    local left = tonumber(snap.roundLeft)
    ctx.textAt(tx + 46, ly, fmtSec(left) or '—',
              (left and left <= 30) and COL.warn or COL.text, FS.L3)
  else
    local used = tonumber(snap.resetUsed) or 0
    local limit = tonumber(snap.resetLimit) or 0
    ctx.textAt(tx + 46, ly, string.format('%d / %d', used, limit),
              (limit > 0 and used >= limit) and COL.warn or COL.text, FS.L3)
  end
  ly = ly + rowH

  -- ④ 车型（赛道日放行零件/换车，但要提示车型校验状态）
  ctx.textAt(tx, ly, '车型', COL.dim, FS.L3)
  local vn = tostring(snap.vehicleName or '')
  if vn == '' then vn = '未读取' end
  ctx.textAt(tx + 46, ly,
            (snap.vehicleOk == false) and '不符要求' or vn,
            (snap.vehicleOk == false) and COL.warn or COL.dim, FS.L3)
  ly = ly + rowH

  -- ⑤ 位置限制：赛道圈 + 出圈后果（抄近道会被拉回）
  local radius = tonumber(snap.zoneRadius) or 0
  ctx.textAt(tx, ly, '赛道圈', COL.dim, FS.L3)
  if radius > 0 then
    local tail = (snap.zoneOut == true) and ' · 已在赛道外' or ' · 出圈拉回'
    ctx.textAt(tx + 46, ly,
              string.format('%.0f m', radius) .. tail,
              (snap.zoneOut == true) and COL.warn or COL.text, FS.L3)
  else
    ctx.textAt(tx + 46, ly, '不设边界', COL.dim, FS.L3)
  end

  return h
end

return M
