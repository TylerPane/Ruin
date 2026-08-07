namespace RuinGamePDT.Combat;

public record AttackShape(IEnumerable<(int x, int y)> Offsets)
{
    public static AttackShape CircularBurst(int radius)
    {
        var offsets = new List<(int, int)>();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
            if (Math.Sqrt(x * x + y * y) <= radius)
                offsets.Add((x, y));
        return new AttackShape(offsets);
    }
}
