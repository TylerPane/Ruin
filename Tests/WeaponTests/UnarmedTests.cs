using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Resources;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Tests;

public class UnarmedTests
{
    private readonly Unarmed _u = new();

    [Fact] public void Unarmed_HasCorrectType()    => Assert.Equal(WeaponType.Unarmed, _u.Type);
    [Fact] public void Unarmed_HasThreeAttacks()   => Assert.Equal(3, _u.Attacks.Count);

    [Fact]
    public void Punch_HasCorrectProperties()
    {
        var a = _u.Attacks.First(a => a.Name == "Punch");
        Assert.Equal(1, a.MinDamage);
        Assert.Equal(2, a.MaxDamage);
        Assert.Equal(1, a.ActionPointCost);
        Assert.Equal(1, a.Range);
        Assert.Single(a.AttackShape.Offsets);
        Assert.Null(a.OnHit);
        Assert.Null(a.OnCrit);
        Assert.Null(a.Reaction);
    }

    [Fact]
    public void ThrowStone_HasCorrectProperties()
    {
        var a = _u.Attacks.First(a => a.Name == "Throw Stone");
        Assert.Equal(1, a.MinDamage);
        Assert.Equal(2, a.MaxDamage);
        Assert.Equal(1, a.ActionPointCost);
        Assert.Equal(5, a.Range);
        Assert.Single(a.AttackShape.Offsets);
        Assert.Null(a.OnHit);
        Assert.Null(a.OnCrit);
        Assert.Null(a.Reaction);
    }

    [Fact]
    public void Shout_HasNoDamageAndCorrectRange()
    {
        var a = _u.Attacks.First(a => a.Name == "Shout");
        Assert.Equal(0, a.MinDamage);
        Assert.Equal(0, a.MaxDamage);
        Assert.Equal(1, a.ActionPointCost);
        Assert.Equal(4, a.Range);
        Assert.Null(a.OnCrit);
        Assert.Null(a.Reaction);
    }

    [Fact]
    public void Shout_HasDiamondBurstShape_ManhattanDistanceFour()
    {
        var a = _u.Attacks.First(a => a.Name == "Shout");
        var offsets = a.AttackShape.Offsets.ToHashSet();

        // 41 tiles for Manhattan distance ≤ 4 (1+4+8+12+16).
        Assert.Equal(41, offsets.Count);

        // Caster's own tile is in the burst (self-buff).
        Assert.Contains((0, 0), offsets);

        // The 4 outermost cardinal tiles.
        Assert.Contains((4, 0),  offsets);
        Assert.Contains((-4, 0), offsets);
        Assert.Contains((0, 4),  offsets);
        Assert.Contains((0, -4), offsets);

        // A diagonal at the boundary (|2|+|2| = 4).
        Assert.Contains((2, 2), offsets);

        // One step beyond Manhattan-4 is excluded.
        Assert.DoesNotContain((5, 0),  offsets);
        Assert.DoesNotContain((3, 2),  offsets); // |3|+|2| = 5
        Assert.DoesNotContain((4, 1),  offsets); // |4|+|1| = 5
    }

    [Fact]
    public void Shout_OnHit_BuffsPhysicalDefenseByTwoForThreeTurns()
    {
        var a = _u.Attacks.First(a => a.Name == "Shout");
        Assert.NotNull(a.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, a.OnHit!.Type);
        Assert.Equal(CombatStat.PhysicalDefense, a.OnHit.TargetStat);
        Assert.Equal(2, a.OnHit.MinAmount);
        Assert.Equal(2, a.OnHit.MaxAmount);
        Assert.Equal(3, a.OnHit.MinDuration);
        Assert.Equal(3, a.OnHit.MaxDuration);
    }
}
