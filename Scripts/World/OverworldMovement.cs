using RuinGamePDT.Party;

namespace RuinGamePDT.World;

public static class OverworldMovement
{
    public static bool IsAdjacent(int x, int y, int bannerX, int bannerY)
    {
        int dx = Math.Abs(x - bannerX);
        int dy = Math.Abs(y - bannerY);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    /// <summary>Moves the banner by (dx, dy) (including diagonals) and scouts the tiles around its new position. No-op if the destination is out of the world's bounds.</summary>
    public static void Step(Banner banner, WorldData world, int dx, int dy)
    {
        int nx = banner.X + dx;
        int ny = banner.Y + dy;
        if (!world.InBounds(nx, ny)) return;

        banner.X = nx;
        banner.Y = ny;
        ScoutAround(banner, world);
    }

    /// <summary>Moves the banner to (x, y) if it is adjacent (including diagonally) to the banner's current position and within the world's bounds, and scouts around it.</summary>
    public static bool MoveToAdjacent(Banner banner, WorldData world, int x, int y)
    {
        if (!IsAdjacent(x, y, banner.X, banner.Y)) return false;
        if (!world.InBounds(x, y)) return false;

        banner.X = x;
        banner.Y = y;
        ScoutAround(banner, world);
        return true;
    }

    /// <summary>Marks the banner's current tile and the 8 tiles surrounding it as scouted.</summary>
    public static void ScoutAround(Banner banner, WorldData world)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var tile = world.GetTile(banner.X + dx, banner.Y + dy);
            if (tile != null) tile.IsScouted = true;
        }
    }
}
