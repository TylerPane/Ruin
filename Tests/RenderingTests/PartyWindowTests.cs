using Microsoft.Xna.Framework;
using RuinGamePDT.Creatures;
using RuinGamePDT.Party;
using RuinGamePDT.Rendering;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Tests;

public class PartyWindowTests
{
    private static Banner MakeBannerWithMercAndSword(out Mercenary merc, out Sword sword)
    {
        var banner = new Banner();
        merc = new Mercenary();
        sword = new Sword();
        banner.AddMercenary(merc);
        banner.Inventory.Add(sword);
        return banner;
    }

    [Fact]
    public void HandleClick_OnInventorySlot_SelectsWeapon()
    {
        var window = new PartyWindow();
        window.Toggle();
        var banner = MakeBannerWithMercAndSword(out _, out var sword);

        var slot = PartyWindow.GetInventorySlotBounds(1920, 900)[0, 0];
        window.HandleClick(new Point(slot.X + 1, slot.Y + 1), banner, 1920, 900);

        Assert.Same(sword, window.SelectedInventoryItem);
    }

    [Fact]
    public void HandleClick_SameSlotTwice_DeselectsWeapon()
    {
        var window = new PartyWindow();
        window.Toggle();
        var banner = MakeBannerWithMercAndSword(out _, out _);

        var slot = PartyWindow.GetInventorySlotBounds(1920, 900)[0, 0];
        var point = new Point(slot.X + 1, slot.Y + 1);
        window.HandleClick(point, banner, 1920, 900);
        window.HandleClick(point, banner, 1920, 900);

        Assert.Null(window.SelectedInventoryItem);
    }

    [Fact]
    public void HandleClick_OnWeaponSlot_WithSelectedWeapon_EquipsIt()
    {
        var window = new PartyWindow();
        window.Toggle();
        var banner = MakeBannerWithMercAndSword(out var merc, out var sword);

        var slot = PartyWindow.GetInventorySlotBounds(1920, 900)[0, 0];
        window.HandleClick(new Point(slot.X + 1, slot.Y + 1), banner, 1920, 900);

        var panel = PartyWindow.GetPanelBounds(1920, 900)[0];
        var weaponSlot = PartyWindow.GetEquipSlotBounds(panel, PartyWindow.EquipSlot.Weapon, PartyWindow.GetEquipSlotTopY(panel));
        window.HandleClick(new Point(weaponSlot.X + 1, weaponSlot.Y + 1), banner, 1920, 900);

        Assert.Same(sword, merc.EquippedWeapon);
        Assert.DoesNotContain(sword, banner.Inventory);
        Assert.Null(window.SelectedInventoryItem);
    }

    [Fact]
    public void HandleClick_OnArmorSlot_WithSelectedWeapon_DoesNotEquip_KeepsSelection()
    {
        var window = new PartyWindow();
        window.Toggle();
        var banner = MakeBannerWithMercAndSword(out var merc, out var sword);

        var slot = PartyWindow.GetInventorySlotBounds(1920, 900)[0, 0];
        window.HandleClick(new Point(slot.X + 1, slot.Y + 1), banner, 1920, 900);

        var panel = PartyWindow.GetPanelBounds(1920, 900)[0];
        var armorSlot = PartyWindow.GetEquipSlotBounds(panel, PartyWindow.EquipSlot.Armor, PartyWindow.GetEquipSlotTopY(panel));
        window.HandleClick(new Point(armorSlot.X + 1, armorSlot.Y + 1), banner, 1920, 900);

        Assert.IsType<Unarmed>(merc.EquippedWeapon);
        Assert.Same(sword, window.SelectedInventoryItem);
    }

    [Fact]
    public void HandleClick_OnMercPanel_WithoutSelection_DoesNothing()
    {
        var window = new PartyWindow();
        window.Toggle();
        var banner = MakeBannerWithMercAndSword(out var merc, out _);

        var panel = PartyWindow.GetPanelBounds(1920, 900)[0];
        var weaponSlot = PartyWindow.GetEquipSlotBounds(panel, PartyWindow.EquipSlot.Weapon, PartyWindow.GetEquipSlotTopY(panel));
        window.HandleClick(new Point(weaponSlot.X + 1, weaponSlot.Y + 1), banner, 1920, 900);

        Assert.IsType<Unarmed>(merc.EquippedWeapon);
    }

    [Fact]
    public void Toggle_OpensAndCloses()
    {
        var window = new PartyWindow();
        Assert.False(window.IsOpen);

        window.Toggle();
        Assert.True(window.IsOpen);

        window.Toggle();
        Assert.False(window.IsOpen);
    }

    [Fact]
    public void Close_ClosesWindow()
    {
        var window = new PartyWindow();
        window.Toggle();

        window.Close();

        Assert.False(window.IsOpen);
    }

    [Fact]
    public void GetPanelBounds_ReturnsFourEqualPanels_SpanningTopSixtyPercent()
    {
        var panels = PartyWindow.GetPanelBounds(1920, 900);

        Assert.Equal(Banner.MaxSize, panels.Length);
        foreach (var panel in panels)
        {
            Assert.Equal(1920 / Banner.MaxSize, panel.Width);
            Assert.Equal((int)(900 * 0.6f), panel.Height);
            Assert.Equal(0, panel.Y);
        }

        Assert.Equal(0, panels[0].X);
        Assert.Equal(1920 / Banner.MaxSize, panels[1].X);
        Assert.Equal((1920 / Banner.MaxSize) * 2, panels[2].X);
        Assert.Equal((1920 / Banner.MaxSize) * 3, panels[3].X);
    }

    [Fact]
    public void GetInventorySlotBounds_ReturnsSixteenByFourGrid_FillingInventory()
    {
        var inventory = PartyWindow.GetInventoryBounds(1920, 900);
        var slots = PartyWindow.GetInventorySlotBounds(1920, 900);

        Assert.Equal(PartyWindow.InventoryColumns, slots.GetLength(0));
        Assert.Equal(PartyWindow.InventoryRows, slots.GetLength(1));

        int slotWidth = inventory.Width / PartyWindow.InventoryColumns;
        int slotHeight = inventory.Height / PartyWindow.InventoryRows;

        Assert.Equal(inventory.X, slots[0, 0].X);
        Assert.Equal(inventory.Y, slots[0, 0].Y);
        Assert.Equal(slotWidth, slots[0, 0].Width);
        Assert.Equal(slotHeight, slots[0, 0].Height);

        Assert.Equal(inventory.X + slotWidth * (PartyWindow.InventoryColumns - 1), slots[PartyWindow.InventoryColumns - 1, 0].X);
        Assert.Equal(inventory.Y + slotHeight * (PartyWindow.InventoryRows - 1), slots[0, PartyWindow.InventoryRows - 1].Y);
    }

    [Fact]
    public void GetInventoryBounds_SpansBottomFortyPercent()
    {
        var inventory = PartyWindow.GetInventoryBounds(1920, 900);

        int panelHeight = (int)(900 * 0.6f);
        Assert.Equal(0, inventory.X);
        Assert.Equal(panelHeight, inventory.Y);
        Assert.Equal(1920, inventory.Width);
        Assert.Equal(900 - panelHeight, inventory.Height);
    }
}
