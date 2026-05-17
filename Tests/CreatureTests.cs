using RuinGamePDT.Creatures;
using static RuinGamePDT.Creatures.BaseStat;

namespace RuinGamePDT.Tests;

internal class TestCreature(string name, int agility, int focus, int mind, int strength, int stamina)
    : Creature(name, agility, focus, mind, strength, stamina);

public class CreatureTests
{
    private static readonly Random Rng = new();

    private static int RandStat() => Rng.Next(1, 20);

    private static TestCreature Make(int agility = 5, int focus = 5, int mind = 5, int strength = 5, int stamina = 5)
        => new("Test", agility, focus, mind, strength, stamina);

    [Fact]
    public void BaseStats_InitializedFromConstructor()
    {
        int a = RandStat(), f = RandStat(), m = RandStat(), s = RandStat(), st = RandStat();
        var c = Make(a, f, m, s, st);

        Assert.Equal(a,  c.BaseStats.Agility);
        Assert.Equal(f,  c.BaseStats.Focus);
        Assert.Equal(m,  c.BaseStats.Mind);
        Assert.Equal(s,  c.BaseStats.Strength);
        Assert.Equal(st, c.BaseStats.Stamina);
    }

    [Fact]
    public void CombatStats_ScaledFromBaseStats()
    {
        int a = RandStat(), f = RandStat(), m = RandStat(), s = RandStat(), st = RandStat();
        var c = Make(a, f, m, s, st);

        Assert.Equal((a * 5), c.CombatStats.AbilityCooldown);
        Assert.Equal(a / 2 + 1, c.CombatStats.ActionPoints);
        Assert.Equal((a * 5), c.CombatStats.Evasion);
        Assert.Equal((f * 5), c.CombatStats.Accuracy);
        Assert.Equal((f * 5), c.CombatStats.CritChance);
        Assert.Equal((f * 5), c.CombatStats.Perception);
        Assert.Equal((m * 5), c.CombatStats.ManaPool);
        Assert.Equal((m * 1), c.CombatStats.MagicDamage);
        Assert.Equal((m * 1), c.CombatStats.MagicDefense);
        Assert.Equal((s * 1), c.CombatStats.AttackPower);
        Assert.Equal((s * 5), c.CombatStats.CritDamageBonus);
        Assert.Equal((s * 1), c.CombatStats.Speed);
        Assert.Equal((st * 2 + 5), c.CombatStats.HitPoints);
        Assert.Equal((st + 3), c.CombatStats.MovementPoints);
        Assert.Equal((st * 1), c.CombatStats.PhysicalDefense);
    }

    [Fact]
    public void CurrentResources_InitializedFromCombatStats()
    {
        int f = RandStat(), m = RandStat(), st = RandStat();
        var c = Make(focus: f, mind: m, stamina: st);

        Assert.Equal((st * 2 + 5), c.CurrentHp);
        Assert.Equal((m * 5), c.CurrentMana);
    }

}
