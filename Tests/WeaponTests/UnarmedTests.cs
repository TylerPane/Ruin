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
    public void Shout_HasCircularBurstShape_EuclideanDistanceFour()
    {
        var a = _u.Attacks.First(a => a.Name == "Shout");
        var offsets = a.AttackShape.Offsets.ToHashSet();

        // Caster's own tile is in the burst (self-buff).
        Assert.Contains((0, 0), offsets);

        // The 4 outermost cardinal tiles (distance exactly 4).
        Assert.Contains((4, 0),  offsets);
        Assert.Contains((-4, 0), offsets);
        Assert.Contains((0, 4),  offsets);
        Assert.Contains((0, -4), offsets);

        // A diagonal within Euclidean 4: sqrt(2²+2²) ≈ 2.83 <= 4.
        Assert.Contains((2, 2), offsets);

        // Just past distance 4 on the diagonal: sqrt(3²+3²) ≈ 4.24 > 4.
        Assert.DoesNotContain((3, 3), offsets);

        // A Manhattan-diamond tile that would have been included under the OLD
        // shape (|3|+|2|=5, outside Manhattan-4) but is now excluded on distance
        // grounds too: sqrt(3²+2²) ≈ 3.6 <= 4, so it's actually INCLUDED under
        // Euclidean distance — the circle is rounder/wider on axes than the
        // diamond, not strictly smaller. Assert inclusion instead:
        Assert.Contains((3, 2), offsets);

        // Straight past the radius on an axis is still excluded either way.
        Assert.DoesNotContain((5, 0), offsets);
    }

    [Fact]
    public void Shout_OnHit_BuffsPhysicalDefenseByTwoForThreeTurns()
    {
        var a = _u.Attacks.First(a => a.Name == "Shout");
        Assert.NotNull(a.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, a.OnHit!.Type);
        Assert.Equal(CombatStat.PhysicalDefense, a.OnHit.Stats[0].Stat);
        Assert.Equal(2, a.OnHit.Stats[0].MinAmount);
        Assert.Equal(2, a.OnHit.Stats[0].MaxAmount);
        Assert.Equal(3, a.OnHit.MinDuration);
        Assert.Equal(3, a.OnHit.MaxDuration);
    }
}
