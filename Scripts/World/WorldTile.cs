using RuinGamePDT.Resources;

namespace RuinGamePDT.World;

public class WorldTile(int x, int y, BiomeType biome)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public BiomeType Biome { get; } = biome;
    public bool IsScouted { get; set; }
}
