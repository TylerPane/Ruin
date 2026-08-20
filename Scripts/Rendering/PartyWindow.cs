using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Items;
using RuinGamePDT.Party;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Rendering;

public class PartyWindow
{
    private static readonly Color PanelBackground = new(210, 180, 140);
    private static readonly Color TextColor = Color.Black;
    private static readonly Color SelectedHighlight = Color.Gold;

    private const int EquipSlotSize = 64;
    private const int EquipSlotGap = 6;
    private const float PanelRightPadding = 0.05f;

    public Item? SelectedInventoryItem { get; private set; }

    private const int Padding = 8;
    private const int IconSize = 40;
    private const int LineHeight = 22;
    private const float FontScale = 1.3f;

    public bool IsOpen { get; private set; }

    public void Toggle() => IsOpen = !IsOpen;
    public void Close() => IsOpen = false;

    private const float PanelHeightRatio = 0.6f;

    public static Rectangle[] GetPanelBounds(int viewportWidth, int viewportHeight)
    {
        int panelWidth = viewportWidth / Banner.MaxSize;
        int panelHeight = (int)(viewportHeight * PanelHeightRatio);
        var bounds = new Rectangle[Banner.MaxSize];
        for (int i = 0; i < Banner.MaxSize; i++)
            bounds[i] = new Rectangle(i * panelWidth, 0, panelWidth, panelHeight);
        return bounds;
    }

    public static Rectangle GetInventoryBounds(int viewportWidth, int viewportHeight)
    {
        int panelHeight = (int)(viewportHeight * PanelHeightRatio);
        return new Rectangle(0, panelHeight, viewportWidth, viewportHeight - panelHeight);
    }

    public const int InventoryColumns = 12;
    public const int InventoryRows = 3;

    public static Rectangle[,] GetInventorySlotBounds(int viewportWidth, int viewportHeight)
    {
        var inventory = GetInventoryBounds(viewportWidth, viewportHeight);
        int slotWidth = inventory.Width / InventoryColumns;
        int slotHeight = inventory.Height / InventoryRows;

        var slots = new Rectangle[InventoryColumns, InventoryRows];
        for (int col = 0; col < InventoryColumns; col++)
            for (int row = 0; row < InventoryRows; row++)
                slots[col, row] = new Rectangle(
                    inventory.X + col * slotWidth,
                    inventory.Y + row * slotHeight,
                    slotWidth,
                    slotHeight);
        return slots;
    }

    public enum EquipSlot { Armor, Weapon, Consumable }

    public static int GetEquipSlotTopY(Rectangle panel) => panel.Y + Padding + IconSize + Padding + LineHeight;

    public static Rectangle GetEquipSlotBounds(Rectangle panel, EquipSlot slot, int topY)
    {
        int x = panel.Right - EquipSlotSize - (int)(panel.Width * PanelRightPadding);
        int y = topY + (int)slot * (EquipSlotSize + EquipSlotGap);
        return new Rectangle(x, y, EquipSlotSize, EquipSlotSize);
    }

    private static bool ItemMatchesSlot(Item item, EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => item is Weapon,
        EquipSlot.Armor => item is Armor,
        EquipSlot.Consumable => item is Consumable,
        _ => false
    };

    public void HandleClick(Point point, Banner banner, int viewportWidth, int viewportHeight)
    {
        if (!IsOpen) return;

        var slots = GetInventorySlotBounds(viewportWidth, viewportHeight);
        for (int col = 0; col < InventoryColumns; col++)
        {
            for (int row = 0; row < InventoryRows; row++)
            {
                if (!slots[col, row].Contains(point)) continue;

                int index = row * InventoryColumns + col;
                if (index >= banner.Inventory.Count) return;

                var clicked = banner.Inventory[index];
                SelectedInventoryItem = SelectedInventoryItem == clicked ? null : clicked;
                return;
            }
        }

        if (SelectedInventoryItem is null) return;

        var panels = GetPanelBounds(viewportWidth, viewportHeight);
        for (int i = 0; i < panels.Length; i++)
        {
            if (!panels[i].Contains(point)) continue;
            if (i >= banner.Mercenaries.Count) return;

            int equipSlotTopY = GetEquipSlotTopY(panels[i]);
            var weaponSlotBounds = GetEquipSlotBounds(panels[i], EquipSlot.Weapon, equipSlotTopY);
            if (weaponSlotBounds.Contains(point) && SelectedInventoryItem is Weapon weapon)
            {
                banner.Mercenaries[i].EquipWeapon(weapon, banner);
                SelectedInventoryItem = null;
            }
            return;
        }
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

        var inventory = GetInventoryBounds(viewportWidth, viewportHeight);
        sb.Draw(pixel, inventory, PanelBackground);

        var slots = GetInventorySlotBounds(viewportWidth, viewportHeight);
        for (int col = 0; col < InventoryColumns; col++)
        {
            for (int row = 0; row < InventoryRows; row++)
            {
                int index = row * InventoryColumns + col;
                var slot = slots[col, row];

                bool isSelected = index < banner.Inventory.Count && banner.Inventory[index] == SelectedInventoryItem;

                if (index < banner.Inventory.Count)
                    sb.Draw(pixel, slot, Color.SaddleBrown);

                DrawBorder(sb, pixel, slot, isSelected ? SelectedHighlight : Color.Black);
            }
        }

        DrawBorder(sb, pixel, inventory);
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle panel) => DrawBorder(sb, pixel, panel, Color.Black);

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle panel, Color color)
    {
        const int thickness = 2;
        sb.Draw(pixel, new Rectangle(panel.X, panel.Y, panel.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(panel.X, panel.Bottom - thickness, panel.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(panel.X, panel.Y, thickness, panel.Height), color);
        sb.Draw(pixel, new Rectangle(panel.Right - thickness, panel.Y, thickness, panel.Height), color);
    }

    private void DrawMercenaryPanel(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle panel, Creatures.Mercenary merc)
    {
        int x = panel.X + Padding + (int)(panel.Width * 0.05f);
        int y = panel.Y + Padding;

        sb.Draw(pixel, new Rectangle(x, y, IconSize, IconSize), Color.SaddleBrown);
        y += IconSize + Padding;

        DrawText(sb, font, merc.Name, x, ref y);

        int equipSlotTopY = GetEquipSlotTopY(panel);

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
            DrawText(sb, font, line, x, ref y);
        }

        DrawEquipSlot(sb, pixel, font, panel, EquipSlot.Armor, "Armor", null, equipSlotTopY);
        DrawEquipSlot(sb, pixel, font, panel, EquipSlot.Weapon, "Weapon", merc.EquippedWeapon is Unarmed ? null : merc.EquippedWeapon, equipSlotTopY);
        DrawEquipSlot(sb, pixel, font, panel, EquipSlot.Consumable, "Consumable", null, equipSlotTopY);

        int detailsY = Math.Max(y, GetEquipSlotBounds(panel, EquipSlot.Consumable, equipSlotTopY).Bottom) + Padding;

        DrawText(sb, font, $"Weapon: {merc.EquippedWeapon?.Type.ToString() ?? "None"}", x, ref detailsY);
        foreach (var attack in merc.Attacks)
            DrawText(sb, font, $"- {attack.Name}", x, ref detailsY);
        detailsY += Padding;

        DrawText(sb, font, "Skills:", x, ref detailsY);
        foreach (var skill in merc.Skills)
            DrawText(sb, font, $"- {skill.Name}", x, ref detailsY);
    }

    private void DrawEquipSlot(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle panel, EquipSlot slot, string label, Item? equipped, int topY)
    {
        var bounds = GetEquipSlotBounds(panel, slot, topY);
        bool isValidTarget = SelectedInventoryItem is not null && ItemMatchesSlot(SelectedInventoryItem, slot);

        sb.Draw(pixel, bounds, equipped is not null ? Color.SaddleBrown : PanelBackground);
        DrawBorder(sb, pixel, bounds, isValidTarget ? SelectedHighlight : Color.Black);

        sb.DrawString(font, label, new Vector2(bounds.X, bounds.Bottom + 2), TextColor, 0f, Vector2.Zero, FontScale * 0.6f, SpriteEffects.None, 0f);
    }

    private static void DrawText(SpriteBatch sb, SpriteFont font, string text, int x, ref int y)
    {
        sb.DrawString(font, text, new Vector2(x, y), TextColor, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
        y += LineHeight;
    }
}
