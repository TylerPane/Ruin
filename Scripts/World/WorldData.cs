namespace RuinGamePDT.World;

public class WorldData(int minX, int minY, int maxX, int maxY)
{
    private readonly Dictionary<(int X, int Y), WorldTile> _tiles = new();

    public int MinX { get; } = minX;
    public int MinY { get; } = minY;
    public int MaxX { get; } = maxX;
    public int MaxY { get; } = maxY;

    public bool InBounds(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

    public void SetTile(int x, int y, WorldTile tile) => _tiles[(x, y)] = tile;
    public WorldTile? GetTile(int x, int y) => _tiles.GetValueOrDefault((x, y));
}
