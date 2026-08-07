using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;

namespace RuinGamePDT.Rendering;

public class EnemyInspectWindow
{
    private const int WindowWidth = 180;
    private const int WindowHeight = 140;
    private const int PortraitSize = 48;
    private const int AttackBoxSize = 24;
    private const int AttackBoxGap = 4;
    private const int Padding = 8;

    private Rectangle _bounds;
    private int? _hoveredAttackIndex;

    public bool IsOpen { get; private set; }
    public Creature? InspectedEnemy { get; private set; }

    public void Open(Creature enemy, (int X, int Y) anchorScreenPos, int viewportWidth, int viewportHeight)
    {
        IsOpen = true;
        InspectedEnemy = enemy;
        _hoveredAttackIndex = null;

        int x = Math.Clamp(anchorScreenPos.X, 0, Math.Max(0, viewportWidth - WindowWidth));
        int y = Math.Clamp(anchorScreenPos.Y, 0, Math.Max(0, viewportHeight - WindowHeight));
        _bounds = new Rectangle(x, y, WindowWidth, WindowHeight);
    }

    public void Close()
    {
        IsOpen = false;
        InspectedEnemy = null;
        _hoveredAttackIndex = null;
    }

    public bool Contains(int mouseX, int mouseY) => IsOpen && _bounds.Contains(mouseX, mouseY);

    public void UpdateHover(int mouseX, int mouseY)
    {
        _hoveredAttackIndex = IsOpen ? HotboxAt(mouseX, mouseY) : null;
    }

    public int? HotboxAt(int mouseX, int mouseY)
    {
        if (!IsOpen || InspectedEnemy == null) return null;

        int rowY = _bounds.Y + PortraitSize + Padding * 2;
        for (int i = 0; i < InspectedEnemy.Attacks.Count; i++)
        {
            int boxX = _bounds.X + Padding + i * (AttackBoxSize + AttackBoxGap);
            var box = new Rectangle(boxX, rowY, AttackBoxSize, AttackBoxSize);
            if (box.Contains(mouseX, mouseY)) return i;
        }
        return null;
    }

    public string FormatTooltip(Attack attack)
    {
        var line = $"{attack.Name}\n{attack.MinDamage}-{attack.MaxDamage} dmg  {attack.Accuracy}%  Range {attack.Range}";

        var effects = new List<string>();
        if (attack.OnHit != null) effects.AddRange(CombatResolver.DescribeEffect(attack.OnHit));
        if (attack.OnCrit != null) effects.AddRange(CombatResolver.DescribeEffect(attack.OnCrit));

        return effects.Count == 0 ? line : $"{line}\n{string.Join(", ", effects)}";
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)
    {
        if (!IsOpen || InspectedEnemy == null) return;

        sb.Draw(pixel, _bounds, Color.Black * 0.85f);

        var portraitRect = new Rectangle(_bounds.X + Padding, _bounds.Y + Padding, PortraitSize, PortraitSize);
        sb.Draw(pixel, portraitRect, Color.Crimson);
        sb.DrawString(font, InspectedEnemy.Name, new Vector2(portraitRect.X, portraitRect.Bottom + 2), Color.White);

        int rowY = _bounds.Y + PortraitSize + Padding * 2;
        for (int i = 0; i < InspectedEnemy.Attacks.Count; i++)
        {
            int boxX = _bounds.X + Padding + i * (AttackBoxSize + AttackBoxGap);
            sb.Draw(pixel, new Rectangle(boxX, rowY, AttackBoxSize, AttackBoxSize), Color.DarkSlateGray);
        }

        if (_hoveredAttackIndex is int idx && idx < InspectedEnemy.Attacks.Count)
        {
            string tooltip = FormatTooltip(InspectedEnemy.Attacks[idx]);
            var tooltipPos = new Vector2(_bounds.X, _bounds.Bottom + 4);
            sb.Draw(pixel, new Rectangle((int)tooltipPos.X, (int)tooltipPos.Y, WindowWidth, 40), Color.Black * 0.9f);
            sb.DrawString(font, tooltip, tooltipPos + new Vector2(4, 4), Color.White);
        }
    }
}
