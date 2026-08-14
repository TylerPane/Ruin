using RuinGamePDT.Party;
using RuinGamePDT.Rendering;

namespace RuinGamePDT.Tests;

public class PartyWindowTests
{
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
    public void GetPanelBounds_ReturnsFourEqualPanels_SpanningViewport()
    {
        var panels = PartyWindow.GetPanelBounds(1920, 900);

        Assert.Equal(Banner.MaxSize, panels.Length);
        foreach (var panel in panels)
        {
            Assert.Equal(1920 / Banner.MaxSize, panel.Width);
            Assert.Equal(900, panel.Height);
            Assert.Equal(0, panel.Y);
        }

        Assert.Equal(0, panels[0].X);
        Assert.Equal(1920 / Banner.MaxSize, panels[1].X);
        Assert.Equal((1920 / Banner.MaxSize) * 2, panels[2].X);
        Assert.Equal((1920 / Banner.MaxSize) * 3, panels[3].X);
    }
}
