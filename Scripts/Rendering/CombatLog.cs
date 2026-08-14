using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Combat;

namespace RuinGamePDT.Rendering;

public class CombatLog
{
    private readonly List<string> _lines = new();
    private int _scrollOffset;

    public IReadOnlyList<string> Lines => _lines;
    public int ScrollOffset => _scrollOffset;

    public void AddEntries(IEnumerable<CombatLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _lines.Add(FormatOutcomeLine(entry.AttackerName, entry.AttackName, entry.TargetName, entry.WasHit, entry.Damage, entry.TargetCurrentHp, entry.TargetMaxHp));
            foreach (var effect in entry.EffectsApplied)
                _lines.Add($"{entry.TargetName}: {effect} applied");
        }
    }

    public void AddSelfCastEntry(string casterName, string skillName, IEnumerable<string> effects, int casterCurrentHp = 0, int casterMaxHp = 0)
    {
        _lines.Add(FormatOutcomeLine(casterName, skillName, casterName, wasHit: true, damage: 0, targetCurrentHp: casterCurrentHp, targetMaxHp: casterMaxHp));
        foreach (var effect in effects)
            _lines.Add($"{casterName}: {effect} applied");
    }

    private static string FormatOutcomeLine(string attacker, string action, string target, bool wasHit, int damage, int targetCurrentHp, int targetMaxHp)
    {
        if (!wasHit)
            return $"{attacker} uses {action} on {target} — MISS";
        if (damage < 0)
            return $"{attacker} uses {action} on {target} — HEAL, {-damage} hp {target} Hp {targetCurrentHp}/{targetMaxHp}";
        return $"{attacker} uses {action} on {target} — HIT, {damage} dmg {target} Hp {targetCurrentHp}/{targetMaxHp}";
    }

    public void HandleScroll(int scrollDelta)
    {
        _scrollOffset = Math.Clamp(_scrollOffset + scrollDelta, 0, _lines.Count);
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle panelBounds)
    {
        sb.Draw(pixel, panelBounds, Color.Black * 0.6f);

        const int lineHeight = 16;
        const int padding = 6;
        int visibleLines = Math.Max(1, (panelBounds.Height - padding * 2) / lineHeight);

        int startIndex = Math.Max(0, _lines.Count - visibleLines - _scrollOffset);
        int endIndex = Math.Min(_lines.Count, startIndex + visibleLines);

        for (int i = startIndex; i < endIndex; i++)
        {
            int row = i - startIndex;
            var pos = new Vector2(panelBounds.X + padding, panelBounds.Y + padding + row * lineHeight);
            sb.DrawString(font, _lines[i], pos, Color.White);
        }
    }
}
