using RuinGamePDT.Combat;

namespace RuinGamePDT.Tests;

public class AttackShapeTests
{
    [Fact]
    public void CircularBurst_RadiusZero_ContainsOnlyOrigin()
    {
        var offsets = AttackShape.CircularBurst(0).Offsets.ToHashSet();

        Assert.Single(offsets);
        Assert.Contains((0, 0), offsets);
    }

    [Fact]
    public void CircularBurst_RadiusTwo_ContainsOriginAndCardinalsButNotDiagonalCorner()
    {
        var offsets = AttackShape.CircularBurst(2).Offsets.ToHashSet();

        Assert.Contains((0, 0), offsets);
        Assert.Contains((2, 0), offsets);
        Assert.Contains((0, 2), offsets);
        Assert.Contains((-2, 0), offsets);
        Assert.Contains((0, -2), offsets);

        // sqrt(2²+2²) ≈ 2.83 > 2 — excluded.
        Assert.DoesNotContain((2, 2), offsets);

        // sqrt(1²+1²) ≈ 1.41 <= 2 — included.
        Assert.Contains((1, 1), offsets);
    }
}
