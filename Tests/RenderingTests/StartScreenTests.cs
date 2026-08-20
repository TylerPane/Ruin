using RuinGamePDT.Rendering;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class StartScreenTests
{
    [Fact]
    public void NewStartScreen_DefaultsToSmall_NotConfirmed()
    {
        var screen = new StartScreen();

        Assert.Equal(0, screen.SelectedIndex);
        Assert.Equal(WorldSize.Small, screen.Selected);
        Assert.False(screen.Confirmed);
    }

    [Fact]
    public void GetOptionBounds_ReturnsFourStackedOptions_CenteredHorizontally()
    {
        var bounds = new System.Collections.Generic.List<Microsoft.Xna.Framework.Rectangle>();
        for (int i = 0; i < 4; i++)
            bounds.Add(StartScreen.GetOptionBounds(i, 1920, 900));

        for (int i = 0; i < 4; i++)
            Assert.Equal(bounds[0].X, bounds[i].X);

        Assert.True(bounds[0].Y < bounds[1].Y);
        Assert.True(bounds[1].Y < bounds[2].Y);
        Assert.True(bounds[2].Y < bounds[3].Y);
    }
}
