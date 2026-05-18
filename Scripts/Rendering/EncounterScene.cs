using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.Resources;

namespace RuinGamePDT.Rendering;

public class EncounterScene(EncounterState state, TurnManager turns, Texture2D pixel, CombatResolver resolver, Dictionary<string, Texture2D> skillIcons)
{
    private const int TileSize = 16;

    private enum Mode { Idle, Movement, Attack }

    private Mode _mode = Mode.Idle;
    private Creature? _selected;
    private Attack? _activeAttack;
    private Dictionary<(int X, int Y), int> _reachable = new();
    private HashSet<(int X, int Y)> _validTargets = new();
    private (int X, int Y) _hoverTile;

    private MouseState _prevMouse;
    private KeyboardState _prevKeyboard;

    private readonly CombatResolver _resolver = resolver;
    private readonly Dictionary<string, Texture2D> _skillIcons = skillIcons;

    public void Update(MouseState mouse)
    {
        var kb = Keyboard.GetState();

        // Hover tile (clamped to map)
        int hx = Math.Clamp(mouse.X / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);

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
                int idx = k - 1;
                if (idx >= _selected.Attacks.Count) break;
                var attack = _selected.Attacks[idx];
                if (state.GetRemainingActionPoints(_selected) < attack.ActionPointCost) break;
                EnterAttackMode(attack);
                break;
            }
        }
    }

    private void HandleMouseClick(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _prevMouse.LeftButton != ButtonState.Released)
            return;

        int gridX = mouse.X / TileSize;
        int gridY = mouse.Y / TileSize;

        if (gridX < 0 || gridX >= state.Map.Width || gridY < 0 || gridY >= state.Map.Height)
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
                    _resolver.Resolve(_selected!, _activeAttack!, (gridX, gridY), state);
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
    }

    private void EnterMovementMode()
    {
        if (_selected == null) { ResetToIdle(); return; }
        _mode = Mode.Movement;
        _activeAttack = null;
        _validTargets = new();
        _reachable = MovementValidator.GetReachableTiles(_selected, state);
    }

    private void EnterAttackMode(Attack attack)
    {
        if (_selected == null) return;
        _mode = Mode.Attack;
        _activeAttack = attack;
        _reachable = new();
        _validTargets = ComputeValidTargets(_selected, attack);
    }

    private HashSet<(int X, int Y)> ComputeValidTargets(Creature attacker, Attack attack)
    {
        var result = new HashSet<(int X, int Y)>();
        bool singleTarget = IsSingleTarget(attack);

        // TODO(distance): switch from Chebyshev to Euclidean for circular range
        // (paired with CombatResolver.IsInRange).
        for (int x = 0; x < state.Map.Width; x++)
        for (int y = 0; y < state.Map.Height; y++)
        {
            if (!_resolver.IsInRange(attacker, attack, (x, y), state)) continue;
            if (singleTarget && state.GetCreatureAt(x, y) == null) continue;
            result.Add((x, y));
        }
        return result;
    }

    private static bool IsSingleTarget(Attack attack)
    {
        var offsets = attack.AttackShape.Offsets.ToList();
        return offsets.Count == 1 && offsets[0] == (0, 0);
    }

    private bool JustPressed(KeyboardState kb, Keys k) =>
        kb.IsKeyDown(k) && !_prevKeyboard.IsKeyDown(k);

    // ────────────────────────────────────────────────────────────────────────
    // Rendering
    // ────────────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb)
    {
        DrawTerrain(sb);
        DrawMovementHighlights(sb);
        DrawAttackHighlights(sb);
        DrawAoePreview(sb);
        DrawCreatures(sb);
        DrawHpBars(sb);
        DrawActionBar(sb);
        DrawApPips(sb);
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
            sb.Draw(pixel, new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize), color);
        }
    }

    private void DrawMovementHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Movement) return;
        foreach (var (pos, _) in _reachable)
            sb.Draw(pixel, new Rectangle(pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Yellow * 0.35f);
    }

    private void DrawAttackHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Attack) return;
        foreach (var pos in _validTargets)
            sb.Draw(pixel, new Rectangle(pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Red * 0.35f);
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
            sb.Draw(pixel, new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize), Color.Cyan * 0.5f);
        }
    }

    private void DrawCreatures(SpriteBatch sb)
    {
        foreach (var merc in state.Mercenaries)
        {
            var pos = state.GetPosition(merc);
            var color = merc == _selected ? Color.Cyan : Color.DodgerBlue;
            sb.Draw(pixel, new Rectangle(pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), color);
        }
        foreach (var enemy in state.Enemies)
        {
            var pos = state.GetPosition(enemy);
            sb.Draw(pixel, new Rectangle(pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Crimson);
        }
    }

    private void DrawHpBars(SpriteBatch sb)
    {
        foreach (var c in state.Mercenaries.Concat(state.Enemies))
        {
            var pos = state.GetPosition(c);
            float max = c.CombatStats.HitPoints;
            float cur = Math.Max(0, c.CurrentHp);
            int fillW = max <= 0 ? 0 : (int)Math.Round(TileSize * (cur / max));

            int barX = pos.X * TileSize;
            int barY = pos.Y * TileSize - 5;
            sb.Draw(pixel, new Rectangle(barX, barY, TileSize, 3), new Color(60, 0, 0));
            if (fillW > 0)
                sb.Draw(pixel, new Rectangle(barX, barY, fillW, 3), new Color(40, 200, 40));
        }
    }

    private void DrawActionBar(SpriteBatch sb)
    {
        const int boxSize = 32;
        const int boxGap = 2;
        const int borderSize = 2;
        const int boxCount = 10;
        int barX = 8;
        int barY = 830;

        for (int i = 0; i < boxCount; i++)
        {
            int x = barX + i * (boxSize + boxGap);
            var outerRect = new Rectangle(x, barY, boxSize, boxSize);
            sb.Draw(pixel, outerRect, Color.White * 0.3f);

            var innerRect = new Rectangle(x + borderSize, barY + borderSize, boxSize - borderSize * 2, boxSize - borderSize * 2);
            sb.Draw(pixel, innerRect, Color.Black);
        }

        if (_selected is Mercenary merc)
        {
            for (int i = 0; i < Math.Min(3, merc.Attacks.Count); i++)
            {
                int x = barX + i * (boxSize + boxGap);
                var rect = new Rectangle(x + borderSize, barY + borderSize, boxSize - borderSize * 2, boxSize - borderSize * 2);
                var attack = merc.Attacks[i];
                if (_skillIcons.TryGetValue(attack.Name, out var icon))
                {
                    sb.Draw(icon, rect, Color.White);
                }
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
        int x0 = 8;
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
