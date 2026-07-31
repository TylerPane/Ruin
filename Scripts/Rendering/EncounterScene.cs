using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.Resources;

namespace RuinGamePDT.Rendering;

public class EncounterScene(EncounterState state, TurnManager turns, Texture2D pixel, CombatResolver resolver, Dictionary<string, Texture2D> skillIcons, SpriteFont logFont)
{
    private const int TileSize = 16;
    private const int LogPanelWidth = 260;
    private const int LogPanelMargin = 8;

    private const int HotbarBoxSize = 32;
    private const int HotbarBoxGap = 2;
    private const int HotbarBoxCount = 10;
    private const int HotbarBarY = 830;

    private enum Mode { Idle, Movement, Attack }

    private Mode _mode = Mode.Idle;
    private Creature? _selected;
    private Attack? _activeAttack;
    private Dictionary<(int X, int Y), int> _reachable = new();
    private HashSet<(int X, int Y)> _validTargets = new();
    private HashSet<(int X, int Y)> _rangeTiles = new();
    private (int X, int Y) _hoverTile;

    private MouseState _prevMouse;
    private KeyboardState _prevKeyboard;
    private int _mapOffsetX;

    private readonly CombatResolver _resolver = resolver;
    private readonly Dictionary<string, Texture2D> _skillIcons = skillIcons;
    private readonly CombatLog _combatLog = new();

    public void AddCombatLogEntries(IEnumerable<CombatLogEntry> entries) => _combatLog.AddEntries(entries);

    private int ContentX => _mapOffsetX;

    public void Update(MouseState mouse, int viewportWidth)
    {
        int mapWidth = state.Map.Width * TileSize;
        _mapOffsetX = Math.Max(0, (viewportWidth - mapWidth) / 2);

        var kb = Keyboard.GetState();

        // Hover tile (clamped to map, accounting for the centered map offset)
        int hx = Math.Clamp((mouse.X - ContentX) / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);

        int scrollDelta = (mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue) / 40;
        if (scrollDelta != 0)
            _combatLog.HandleScroll(-scrollDelta);

        HandleKeyboard(kb);
        HandleMouseClick(mouse);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Input
    // ────────────────────────────────────────────────────────────────────────

    private void HandleKeyboard(KeyboardState kb)
    {
        // Space: end the selected merc's turn (any mode).
        if (JustPressed(kb, Keys.Space) && _selected != null && turns.CanMove(_selected))
        {
            turns.EndCreatureTurn(_selected);
            ResetToIdle();
            return;
        }

        // Esc: in Attack mode, return to Movement.
        if (JustPressed(kb, Keys.Escape) && _mode == Mode.Attack)
        {
            EnterMovementMode();
            return;
        }

        // Number keys 1-9: pick attack while in Movement or Attack mode.
        if (_selected != null && (_mode == Mode.Movement || _mode == Mode.Attack))
        {
            for (int k = 1; k <= 9; k++)
            {
                if (!JustPressed(kb, Keys.D0 + k)) continue;
                ActivateSlot(k - 1);
                break;
            }
        }

        // Skill keys 8/9/0 (Rush/Defensive Stance/First Aid): activate skills while in Movement or Attack mode.
        if (_selected is Mercenary && (_mode == Mode.Movement || _mode == Mode.Attack))
        {
            if (JustPressed(kb, Keys.D8)) ActivateSlot(7);
            else if (JustPressed(kb, Keys.D9)) ActivateSlot(8);
            else if (JustPressed(kb, Keys.D0)) ActivateSlot(9);
        }
    }

    // Slot 0-6: attacks. Slot 7: Rush (self-cast, bypasses resolver). Slot 8: Defensive Stance
    // (self-cast via resolver). Slot 9: First Aid (targeted heal, enters Attack mode).
    private void ActivateSlot(int slot)
    {
        if (_selected == null) return;

        if (slot <= 6)
        {
            if (slot >= _selected.Attacks.Count) return;
            var attack = _selected.Attacks[slot];
            if (state.GetRemainingActionPoints(_selected) < attack.ActionPointCost) return;
            EnterAttackMode(attack);
            return;
        }

        if (_selected is not Mercenary skillUser) return;

        switch (slot)
        {
            case 7: // Rush (Skills[0]): spend AP, grant MP this turn only (no stat mutation)
                if (skillUser.Skills.Count <= 0) return;
                var rush = skillUser.Skills[0];
                if (state.GetRemainingActionPoints(skillUser) < rush.ActionPointCost) return;
                state.SpendActionPoints(skillUser, rush.ActionPointCost);
                var effectDescriptions = new List<string>();
                if (rush.OnHit?.Stats is { Count: > 0 } stats)
                {
                    var s = stats[0];
                    state.AddMovement(skillUser, Random.Shared.Next(s.MinAmount, s.MaxAmount + 1));
                    effectDescriptions.Add($"{s.Stat} +");
                }
                _combatLog.AddSelfCastEntry(skillUser.Name, rush.Name, effectDescriptions);
                EnterMovementMode();
                break;

            case 8: // Defensive Stance (Skills[1]): self-cast
                if (skillUser.Skills.Count <= 1) return;
                var stance = skillUser.Skills[1];
                if (state.GetRemainingActionPoints(skillUser) < stance.ActionPointCost) return;
                var pos = state.GetPosition(skillUser);
                var entries = _resolver.Resolve(skillUser, stance, pos, state);
                _combatLog.AddEntries(entries);
                EnterMovementMode();
                break;

            case 9: // First Aid (Skills[2]): enter targeting mode
                if (skillUser.Skills.Count <= 2) return;
                var firstAid = skillUser.Skills[2];
                if (state.GetRemainingActionPoints(skillUser) < firstAid.ActionPointCost) return;
                EnterAttackMode(firstAid);
                break;
        }
    }

    private void HandleMouseClick(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _prevMouse.LeftButton != ButtonState.Released)
            return;

        int? clickedSlot = HotbarSlotAt(mouse.X, mouse.Y);
        if (clickedSlot != null && _selected != null && (_mode == Mode.Movement || _mode == Mode.Attack))
        {
            ActivateSlot(clickedSlot.Value);
            return;
        }

        int gridX = (mouse.X - ContentX) / TileSize;
        int gridY = mouse.Y / TileSize;

        if (mouse.X < ContentX || gridX >= state.Map.Width || gridY < 0 || gridY >= state.Map.Height)
        {
            ResetToIdle();
            return;
        }

        switch (_mode)
        {
            case Mode.Idle:
            {
                var creature = state.GetCreatureAt(gridX, gridY);
                if (creature is Mercenary && turns.CanMove(creature))
                {
                    _selected = creature;
                    EnterMovementMode();
                }
                break;
            }
            case Mode.Movement:
            {
                if (_reachable.ContainsKey((gridX, gridY)))
                {
                    state.MoveCreature(_selected!, gridX, gridY);
                    // stay in Movement mode — merc may still have AP or further moves
                }
                else
                {
                    ResetToIdle();
                }
                break;
            }
            case Mode.Attack:
            {
                if (_validTargets.Contains((gridX, gridY)))
                {
                    if (_activeAttack!.MaxDamage < 0)
                    {
                        // Heal skill: bypass resolver, apply HP directly
                        var target = state.GetCreatureAt(gridX, gridY);
                        if (target != null)
                        {
                            state.SpendActionPoints(_selected!, _activeAttack.ActionPointCost);
                            int heal = Random.Shared.Next(-_activeAttack.MaxDamage, -_activeAttack.MinDamage + 1);
                            target.CurrentHp += heal;
                            _combatLog.AddEntries(new[] { new CombatLogEntry(_selected!.Name, _activeAttack.Name, target.Name, WasHit: true, Damage: -heal, EffectsApplied: Array.Empty<string>()) });
                        }
                    }
                    else
                    {
                        var entries = _resolver.Resolve(_selected!, _activeAttack!, (gridX, gridY), state);
                        _combatLog.AddEntries(entries);
                    }
                    EnterMovementMode();
                }
                else
                {
                    EnterMovementMode();
                }
                break;
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Mode transitions
    // ────────────────────────────────────────────────────────────────────────

    private void ResetToIdle()
    {
        _mode = Mode.Idle;
        _selected = null;
        _activeAttack = null;
        _reachable = new();
        _validTargets = new();
        _rangeTiles = new();
    }

    private void EnterMovementMode()
    {
        if (_selected == null) { ResetToIdle(); return; }
        _mode = Mode.Movement;
        _activeAttack = null;
        _validTargets = new();
        _rangeTiles = new();
        _reachable = MovementValidator.GetReachableTiles(_selected, state);
    }

    private void EnterAttackMode(Attack attack)
    {
        if (_selected == null) return;
        _mode = Mode.Attack;
        _activeAttack = attack;
        _reachable = new();
        _rangeTiles = ComputeRangeTiles(_selected, attack);
        _validTargets = ComputeValidTargets(_selected, attack, _rangeTiles);
    }

    private HashSet<(int X, int Y)> ComputeRangeTiles(Creature attacker, Attack attack)
    {
        var result = new HashSet<(int X, int Y)>();

        // TODO(distance): switch from Chebyshev to Euclidean for circular range
        // (paired with CombatResolver.IsInRange).
        for (int x = 0; x < state.Map.Width; x++)
        for (int y = 0; y < state.Map.Height; y++)
        {
            if (_resolver.IsInRange(attacker, attack, (x, y), state))
                result.Add((x, y));
        }
        return result;
    }

    private HashSet<(int X, int Y)> ComputeValidTargets(Creature attacker, Attack attack, HashSet<(int X, int Y)> rangeTiles)
    {
        var result = new HashSet<(int X, int Y)>();
        bool singleTarget = IsSingleTarget(attack);

        foreach (var tile in rangeTiles)
        {
            if (singleTarget && state.GetCreatureAt(tile.X, tile.Y) == null) continue;
            result.Add(tile);
        }
        return result;
    }

    private static bool IsSingleTarget(Attack attack)
    {
        var offsets = attack.AttackShape.Offsets.ToList();
        return offsets.Count == 1 && offsets[0] == (0, 0);
    }

    private int? HotbarSlotAt(int mouseX, int mouseY)
    {
        int barX = HotbarBarX();
        if (mouseY < HotbarBarY || mouseY >= HotbarBarY + HotbarBoxSize) return null;
        if (mouseX < barX) return null;

        int offset = mouseX - barX;
        int stride = HotbarBoxSize + HotbarBoxGap;
        int slot = offset / stride;
        if (slot >= HotbarBoxCount) return null;
        if (offset % stride >= HotbarBoxSize) return null; // clicked in the gap between boxes

        return slot;
    }

    private bool JustPressed(KeyboardState kb, Keys k) =>
        kb.IsKeyDown(k) && !_prevKeyboard.IsKeyDown(k);

    // ────────────────────────────────────────────────────────────────────────
    // Rendering
    // ────────────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb)
    {
        int mapWidth = state.Map.Width * TileSize;
        _mapOffsetX = Math.Max(0, (sb.GraphicsDevice.Viewport.Width - mapWidth) / 2);

        DrawTerrain(sb);
        DrawMovementHighlights(sb);
        DrawAttackHighlights(sb);
        DrawAoePreview(sb);
        DrawCreatures(sb);
        DrawHpBars(sb);
        DrawActionBar(sb);
        DrawApPips(sb);
        _combatLog.Draw(sb, pixel, logFont, new Rectangle(LogPanelMargin, 0, LogPanelWidth, 900));
    }

    private void DrawTerrain(SpriteBatch sb)
    {
        for (int x = 0; x < state.Map.Width; x++)
        for (int y = 0; y < state.Map.Height; y++)
        {
            var color = state.Map.GetTile(x, y) switch
            {
                EncounterTileType.Obstacle => new Color(50, 50, 50),
                EncounterTileType.Hazard   => new Color(200, 100, 0),
                _                          => new Color(90, 90, 90)
            };
            sb.Draw(pixel, new Rectangle(ContentX + x * TileSize, y * TileSize, TileSize, TileSize), color);
        }
    }

    private void DrawMovementHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Movement) return;
        foreach (var (pos, _) in _reachable)
            sb.Draw(pixel, new Rectangle(ContentX + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Yellow * 0.35f);
    }

    private void DrawAttackHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Attack) return;
        foreach (var pos in _rangeTiles)
            sb.Draw(pixel, new Rectangle(ContentX + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.LightGray * 0.25f);

        // AOE attacks (e.g. Shout): every in-range tile is already a valid target, so
        // red would cover the whole range with no distinct "range" vs "target" meaning.
        // Only single-target attacks get the extra red "a creature is actually here" cue.
        if (_activeAttack != null && IsSingleTarget(_activeAttack))
        {
            foreach (var pos in _validTargets)
                sb.Draw(pixel, new Rectangle(ContentX + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Red * 0.35f);
        }
    }

    private void DrawAoePreview(SpriteBatch sb)
    {
        if (_mode != Mode.Attack || _activeAttack == null) return;
        if (!_validTargets.Contains(_hoverTile)) return;

        foreach (var (dx, dy) in _activeAttack.AttackShape.Offsets)
        {
            int x = _hoverTile.X + dx;
            int y = _hoverTile.Y + dy;
            if (x < 0 || x >= state.Map.Width || y < 0 || y >= state.Map.Height) continue;
            sb.Draw(pixel, new Rectangle(ContentX + x * TileSize, y * TileSize, TileSize, TileSize), Color.Cyan * 0.5f);
        }
    }

    private void DrawCreatures(SpriteBatch sb)
    {
        foreach (var merc in state.Mercenaries)
        {
            var pos = state.GetPosition(merc);
            var rect = new Rectangle(ContentX + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize);
            sb.Draw(pixel, rect, Color.DodgerBlue);
            if (merc == _selected || merc == turns.CurrentCreature)
                DrawSelectionRing(sb, rect);
        }
        foreach (var enemy in state.Enemies)
        {
            var pos = state.GetPosition(enemy);
            sb.Draw(pixel, new Rectangle(ContentX + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Crimson);
        }
    }

    private void DrawSelectionRing(SpriteBatch sb, Rectangle tileRect)
    {
        const int thickness = 2;
        var ringColor = Color.Yellow;
        sb.Draw(pixel, new Rectangle(tileRect.X - thickness, tileRect.Y - thickness, tileRect.Width + thickness * 2, thickness), ringColor);
        sb.Draw(pixel, new Rectangle(tileRect.X - thickness, tileRect.Bottom, tileRect.Width + thickness * 2, thickness), ringColor);
        sb.Draw(pixel, new Rectangle(tileRect.X - thickness, tileRect.Y - thickness, thickness, tileRect.Height + thickness * 2), ringColor);
        sb.Draw(pixel, new Rectangle(tileRect.Right, tileRect.Y - thickness, thickness, tileRect.Height + thickness * 2), ringColor);
    }

    private void DrawHpBars(SpriteBatch sb)
    {
        foreach (var c in state.Mercenaries.Concat(state.Enemies))
        {
            var pos = state.GetPosition(c);
            float max = c.CombatStats.HitPoints;
            float cur = Math.Max(0, c.CurrentHp);
            int fillW = max <= 0 ? 0 : (int)Math.Round(TileSize * (cur / max));

            int barX = ContentX + pos.X * TileSize;
            int barY = pos.Y * TileSize - 5;
            sb.Draw(pixel, new Rectangle(barX, barY, TileSize, 3), new Color(60, 0, 0));
            if (fillW > 0)
                sb.Draw(pixel, new Rectangle(barX, barY, fillW, 3), new Color(40, 200, 40));
        }
    }

    private int HotbarBarX()
    {
        int mapWidth = state.Map.Width * TileSize;
        int hotbarWidth = HotbarBoxCount * HotbarBoxSize + (HotbarBoxCount - 1) * HotbarBoxGap;
        return _mapOffsetX + Math.Max(0, (mapWidth - hotbarWidth) / 2);
    }

    private void DrawActionBar(SpriteBatch sb)
    {
        const int borderSize = 2;
        int barX = HotbarBarX();

        // Slots 0-6 -> keys 1-7; slot 7 -> key 8; slot 8 -> key 9; slot 9 -> key 0.
        int[] slotKeyLabels = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };

        for (int i = 0; i < HotbarBoxCount; i++)
        {
            int x = barX + i * (HotbarBoxSize + HotbarBoxGap);
            var outerRect = new Rectangle(x, HotbarBarY, HotbarBoxSize, HotbarBoxSize);
            sb.Draw(pixel, outerRect, Color.White * 0.3f);

            var innerRect = new Rectangle(x + borderSize, HotbarBarY + borderSize, HotbarBoxSize - borderSize * 2, HotbarBoxSize - borderSize * 2);
            sb.Draw(pixel, innerRect, Color.Black);

            string label = slotKeyLabels[i].ToString();
            var labelPos = new Vector2(x + HotbarBoxSize - borderSize - 8, HotbarBarY + HotbarBoxSize - borderSize - 12);
            sb.DrawString(logFont, label, labelPos, Color.White);
        }

        if (_selected is Mercenary merc)
        {
            // Attacks in slots 0..N-1
            for (int i = 0; i < Math.Min(7, merc.Attacks.Count); i++)
            {
                int x = barX + i * (HotbarBoxSize + HotbarBoxGap);
                var rect = new Rectangle(x + borderSize, HotbarBarY + borderSize, HotbarBoxSize - borderSize * 2, HotbarBoxSize - borderSize * 2);
                if (_skillIcons.TryGetValue(merc.Attacks[i].Name, out var icon))
                    sb.Draw(icon, rect, Color.White);
            }

            // Skills in fixed slots 7 (Rush/key 8), 8 (Defensive Stance/key 9), 9 (First Aid/key 0)
            int[] skillSlots = { 7, 8, 9 };
            for (int i = 0; i < Math.Min(skillSlots.Length, merc.Skills.Count); i++)
            {
                int slot = skillSlots[i];
                int x = barX + slot * (HotbarBoxSize + HotbarBoxGap);
                var rect = new Rectangle(x + borderSize, HotbarBarY + borderSize, HotbarBoxSize - borderSize * 2, HotbarBoxSize - borderSize * 2);
                if (_skillIcons.TryGetValue(merc.Skills[i].Name, out var icon))
                    sb.Draw(icon, rect, Color.White);
            }
        }
    }

    private void DrawApPips(SpriteBatch sb)
    {
        if (_selected == null) return;
        int max = _selected.CombatStats.ActionPoints;
        int remaining = state.GetRemainingActionPoints(_selected);
        const int pipSize = 8;
        const int pipGap = 4;
        int x0 = HotbarBarX();
        int y0 = 810;
        for (int i = 0; i < max; i++)
        {
            int x = x0 + i * (pipSize + pipGap);
            var rect = new Rectangle(x, y0, pipSize, pipSize);
            if (i < remaining)
                sb.Draw(pixel, rect, Color.Yellow);
            else
                sb.Draw(pixel, rect, Color.Black);
        }
    }
}
