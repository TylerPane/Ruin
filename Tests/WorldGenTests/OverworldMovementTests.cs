using RuinGamePDT.Party;
using RuinGamePDT.Resources;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class OverworldMovementTests
{
    private static WorldData MakeWorld(int radius = 3)
    {
        var world = new WorldData(-radius, -radius, radius, radius);
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                world.SetTile(x, y, new WorldTile(x, y, BiomeType.Plains));
        return world;
    }

    [Fact]
    public void Step_MovesBanner_AndScoutsSurroundingTiles()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        OverworldMovement.Step(banner, world, 1, 0);

        Assert.Equal(1, banner.X);
        Assert.Equal(0, banner.Y);
        Assert.True(world.GetTile(1, 0)!.IsScouted);
        Assert.True(world.GetTile(2, 1)!.IsScouted);
        Assert.True(world.GetTile(0, -1)!.IsScouted);
        Assert.False(world.GetTile(3, 3)!.IsScouted);
    }

    [Fact]
    public void Step_SupportsDiagonalMovement()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        OverworldMovement.Step(banner, world, 1, 1);

        Assert.Equal(1, banner.X);
        Assert.Equal(1, banner.Y);
    }

    [Fact]
    public void Step_DoesNothing_WhenDestinationIsOutOfBounds()
    {
        var world = MakeWorld(radius: 3);
        var banner = new Banner { X = 3, Y = 3 };

        OverworldMovement.Step(banner, world, 1, 1);

        Assert.Equal(3, banner.X);
        Assert.Equal(3, banner.Y);
    }

    [Fact]
    public void MoveToAdjacent_ReturnsTrue_AndMoves_WhenTileIsOrthogonallyAdjacent()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        bool moved = OverworldMovement.MoveToAdjacent(banner, world, 0, 1);

        Assert.True(moved);
        Assert.Equal(0, banner.X);
        Assert.Equal(1, banner.Y);
    }

    [Fact]
    public void MoveToAdjacent_ReturnsTrue_AndMoves_WhenTileIsDiagonallyAdjacent()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        bool moved = OverworldMovement.MoveToAdjacent(banner, world, 1, 1);

        Assert.True(moved);
        Assert.Equal(1, banner.X);
        Assert.Equal(1, banner.Y);
    }

    [Fact]
    public void MoveToAdjacent_ReturnsFalse_WhenTileIsTwoAway()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        bool moved = OverworldMovement.MoveToAdjacent(banner, world, 2, 0);

        Assert.False(moved);
        Assert.Equal(0, banner.X);
        Assert.Equal(0, banner.Y);
    }

    [Fact]
    public void MoveToAdjacent_ReturnsFalse_WhenDestinationIsOutOfBounds()
    {
        var world = MakeWorld(radius: 3);
        var banner = new Banner { X = 3, Y = 3 };

        bool moved = OverworldMovement.MoveToAdjacent(banner, world, 4, 4);

        Assert.False(moved);
        Assert.Equal(3, banner.X);
        Assert.Equal(3, banner.Y);
    }

    [Fact]
    public void MoveToAdjacent_ReturnsFalse_WhenSameTile()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        bool moved = OverworldMovement.MoveToAdjacent(banner, world, 0, 0);

        Assert.False(moved);
    }

    [Fact]
    public void ScoutAround_MarksNineTiles()
    {
        var world = MakeWorld();
        var banner = new Banner { X = 0, Y = 0 };

        OverworldMovement.ScoutAround(banner, world);

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
            Assert.True(world.GetTile(dx, dy)!.IsScouted);

        Assert.False(world.GetTile(2, 2)!.IsScouted);
    }
}
