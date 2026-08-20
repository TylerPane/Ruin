using RuinGamePDT.Creatures;
using RuinGamePDT.Party;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Tests;

public class BannerTests
{
    [Fact]
    public void NewBanner_HasEmptyInventory()
    {
        var banner = new Banner();
        Assert.Empty(banner.Inventory);
    }

    [Fact]
    public void Inventory_CanAddWeapon()
    {
        var banner = new Banner();
        var sword = new Sword();
        banner.Inventory.Add(sword);
        Assert.Contains(sword, banner.Inventory);
    }

    [Fact]
    public void AddMercenary_ReturnsTrue_WhenBelowMaxSize()
    {
        var banner = new Banner();
        Assert.True(banner.AddMercenary(new Mercenary()));
        Assert.Single(banner.Mercenaries);
    }

    [Fact]
    public void AddMercenary_ReturnsFalse_WhenAtMaxSize()
    {
        var banner = new Banner();
        for (int i = 0; i < Banner.MaxSize; i++)
            banner.AddMercenary(new Mercenary());

        Assert.False(banner.AddMercenary(new Mercenary()));
        Assert.Equal(Banner.MaxSize, banner.Mercenaries.Count);
    }

    [Fact]
    public void NewBanner_StartsAtOrigin()
    {
        var banner = new Banner();
        Assert.Equal(0, banner.X);
        Assert.Equal(0, banner.Y);
    }
}
