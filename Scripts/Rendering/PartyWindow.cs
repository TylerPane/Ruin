using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Party;

namespace RuinGamePDT.Rendering;

public class PartyWindow
{
    private static readonly Color PanelBackground = new(210, 180, 140);
    private static readonly Color TextColor = Color.Black;

    private const int Padding = 8;
    private const int IconSize = 40;
    private const int LineHeight = 16;

    public bool IsOpen { get; private set; }

    public void Toggle() => IsOpen = !IsOpen;
    public void Close() => IsOpen = false;

    public static Rectangle[] GetPanelBounds(int viewportWidth, int viewportHeight)
    {
        int panelWidth = viewportWidth / Banner.MaxSize;
        var bounds = new Rectangle[Banner.MaxSize];
        for (int i = 0; i < Banner.MaxSize; i++)
            bounds[i] = new Rectangle(i * panelWidth, 0, panelWidth, viewportHeight);
        return bounds;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font, Banner banner, int viewportWidth, int viewportHeight)
    {
        if (!IsOpen) return;

        var panels = GetPanelBounds(viewportWidth, viewportHeight);
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            sb.Draw(pixel, panel, PanelBackground);
            DrawBorder(sb, pixel, panel);

            if (i >= banner.Mercenaries.Count) continue;
            DrawMercenaryPanel(sb, pixel, font, panel, banner.Mercenaries[i]);
        }
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle panel)
    {
        const int thickness = 2;
        sb.Draw(pixel, new Rectangle(panel.X, panel.Y, panel.Width, thickness), Color.Black);
        sb.Draw(pixel, new Rectangle(panel.X, panel.Bottom - thickness, panel.Width, thickness), Color.Black);
        sb.Draw(pixel, new Rectangle(panel.X, panel.Y, thickness, panel.Height), Color.Black);
        sb.Draw(pixel, new Rectangle(panel.Right - thickness, panel.Y, thickness, panel.Height), Color.Black);
    }

    private void DrawMercenaryPanel(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle panel, Creatures.Mercenary merc)
    {
        int x = panel.X + Padding;
        int y = panel.Y + Padding;

        sb.Draw(pixel, new Rectangle(x, y, IconSize, IconSize), Color.SaddleBrown);
        y += IconSize + Padding;

        sb.DrawString(font, merc.Name, new Vector2(x, y), TextColor);
        y += LineHeight;

        foreach (var line in new[]
        {
            $"Agility: {merc.BaseStats.Agility}",
            $"Focus: {merc.BaseStats.Focus}",
            $"Mind: {merc.BaseStats.Mind}",
            $"Strength: {merc.BaseStats.Strength}",
            $"Stamina: {merc.BaseStats.Stamina}",
            $"HP: {merc.CurrentHp}/{merc.CombatStats.HitPoints}",
        })
        {
            sb.DrawString(font, line, new Vector2(x, y), TextColor);
            y += LineHeight;
        }

        y += Padding;
        sb.DrawString(font, $"Weapon: {merc.EquippedWeapon?.Type.ToString() ?? "None"}", new Vector2(x, y), TextColor);
        y += LineHeight + Padding;

        sb.DrawString(font, "Skills:", new Vector2(x, y), TextColor);
        y += LineHeight;

        foreach (var attack in merc.Attacks)
        {
            sb.DrawString(font, $"- {attack.Name}", new Vector2(x, y), TextColor);
            y += LineHeight;
        }
        foreach (var skill in merc.Skills)
        {
            sb.DrawString(font, $"- {skill.Name}", new Vector2(x, y), TextColor);
            y += LineHeight;
        }
    }
}
