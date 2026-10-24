-- StartRide 模式组件 · 德比（derby）
--
-- 独立面板（右下）：车损环 + 存活人数 + 复位预算 + 场地位置限制。
-- 只读 snapshot；绘制原语由 HUD 注入 ctx。

local M = { id = 'derby', title = '德比' }

local function fmtNum(v)
  local n = tonumber(v)
  if not n then return '—' end
  return string.format('%.0f', n)
end

function M.panel(ctx, snap)
  if type(ctx) ~= 'table' or type(snap) ~= 'table' then return 0 end
  if snap.mode ~= M.id then return 0 end
  if tostring(snap.phase or '') == 'lobby' then return 0 end

  local COL, FS, LH = ctx.COL, ctx.FS, ctx.LH
  local dl, x, y, w = ctx.dl, ctx.x, ctx.y, ctx.w
  local padX = (ctx.TUNE and ctx.TUNE.padX) or 16
  local padY = (ctx.TUNE and ctx.TUNE.padY) or 13
  local eliminated = (snap.eliminated == true)
  local accent = eliminated and COL.danger or COL.warn

  local rowH = LH.L3 + 6
  local h = padY * 2 + LH.L1 + rowH * 5
  ctx.card(dl, x, y, w, h, accent)

  local tx = x + padX
  local ty = y + padY
  ctx.textAt(tx, ty, '场况', accent, FS.L1)
  ctx.textSame(eliminated and '· 已出局' or '· 对决中', COL.dim, FS.L3)

  local ly = ty + LH.L1 + 2

  -- ① 存活 / 出局
  ctx.textAt(tx, ly, '存活', COL.dim, FS.L3)
  ctx.textAt(tx + 46, ly, fmtNum(snap.derbyAlive) .. ' 人', COL.text, FS.L3)
  ctx.textAt(tx + w - padX - 96, ly, '撞击 ' .. fmtNum(snap.ramCount) .. ' 次', COL.dim, FS.L3)
  ly = ly + rowH

  -- ② 车损
  local limit = math.max(1, tonumber(snap.derbyLimit) or 8000)
  local dmg = math.max(0, tonumber(snap.derbyDamage) or 0)
  local ratio = math.min(1, dmg / limit)
  local dmgCol = (ratio >= 0.85 and COL.danger) or (ratio >= 0.5 and COL.warn) or COL.ok
  ctx.textAt(tx, ly, '车损', COL.dim, FS.L3)
  ctx.textAt(tx + 46, ly, fmtNum(dmg) .. ' / ' .. fmtNum(limit), dmgCol, FS.L3)
  ly = ly + rowH

  -- 车损条（细条，贴着文字下面）
  ctx.barFill(dl, tx, ly, w - padX * 2, 4, ratio, dmgCol)
  ly = ly + 12

  -- ③ 复位预算（德比允许复位，但封顶 + 冷却；这里只显示剩余）
  local rl = tonumber(snap.resetLimit) or 0
  local ru = tonumber(snap.resetUsed) or 0
  ctx.textAt(tx, ly, '复位', COL.dim, FS.L3)
  if rl > 0 then
    local left = math.max(0, rl - ru)
    ctx.textAt(tx + 46, ly, left .. ' / ' .. rl .. ' 次',
              (left > 0) and COL.text or COL.danger, FS.L3)
  else
    ctx.textAt(tx + 46, ly, '不限次', COL.text, FS.L3)
  end
  ly = ly + rowH

  -- ④ 位置限制：场地圈 + 出圈后果
  local radius = tonumber(snap.zoneRadius) or 0
  ctx.textAt(tx, ly, '场地', COL.dim, FS.L3)
  if radius > 0 then
    ctx.textAt(tx + 46, ly, fmtNum(radius) .. ' m · 出圈拉回',
              (snap.zoneOut == true) and COL.warn or COL.text, FS.L3)
  else
    ctx.textAt(tx + 46, ly, '不设边界', COL.dim, FS.L3)
  end

  return h
end

return M
