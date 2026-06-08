using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Weapons;
using static RuinGamePDT.Creatures.BaseStat;

namespace RuinGamePDT.Tests;

public class MercenaryTests
{
    [Fact]
    public void Mercenary_IsACreature()
    {
        Assert.IsAssignableFrom<Creature>(new Mercenary());
    }

    [Fact]
    public void Mercenary_HasCorrectName()
    {
        Assert.Equal("Mercenary", new Mercenary().Name);
    }

    [Fact]
    public void Mercenary_StatsWithinRange_AreAccepted()
    {
        var m = new Mercenary(agility: 3, focus: 4, mind: 5, strength: 6, stamina: 7);
        Assert.Equal(3, m.BaseStats.Agility);
        Assert.Equal(4, m.BaseStats.Focus);
        Assert.Equal(5, m.BaseStats.Mind);
        Assert.Equal(6, m.BaseStats.Strength);
        Assert.Equal(7, m.BaseStats.Stamina);
    }

    [Fact]
    public void Mercenary_StatsAboveCap_AreClampedToTen()
    {
        var m = new Mercenary(agility: 15, focus: 15, mind: 15, strength: 15, stamina: 15);
        Assert.Equal(10, m.BaseStats.Agility);
        Assert.Equal(10, m.BaseStats.Focus);
        Assert.Equal(10, m.BaseStats.Mind);
        Assert.Equal(10, m.BaseStats.Strength);
        Assert.Equal(10, m.BaseStats.Stamina);
    }

    [Fact]
    public void Mercenary_RaiseStat_CannotExceedCap()
    {
        var m = new Mercenary(agility: 9);
        m.RaiseStat(Agility, 5);
        Assert.Equal(10, m.BaseStats.Agility);
    }

    [Fact]
    public void Mercenary_DefaultEquippedWeapon_IsUnarmed()
    {
        Assert.IsType<Unarmed>(new Mercenary().EquippedWeapon);
    }

    [Fact]
    public void Mercenary_Attacks_PopulatedFromEquippedWeapon()
    {
        var m = new Mercenary();
        Assert.Equal(m.EquippedWeapon!.Attacks.Count, m.Attacks.Count);
        foreach (var weaponAttack in m.EquippedWeapon.Attacks)
            Assert.Contains(weaponAttack, m.Attacks);
    }

    [Fact]
    public void Mercenary_RaiseStat_PreservesEquippedWeaponAndAttacks()
    {
        var m = new Mercenary();
        var weapon = m.EquippedWeapon;
        int attackCount = m.Attacks.Count;

        m.RaiseStat(Strength, 1);

        Assert.Same(weapon, m.EquippedWeapon);
        Assert.Equal(attackCount, m.Attacks.Count);
    }

    [Fact]
    public void CreateRandom_AllStatsAreWithinOneToFive()
    {
        for (int i = 0; i < 50; i++)
        {
            var m = Mercenary.CreateRandom();
            Assert.InRange(m.BaseStats.Agility,  1, 5);
            Assert.InRange(m.BaseStats.Focus,    1, 5);
            Assert.InRange(m.BaseStats.Mind,     1, 5);
            Assert.InRange(m.BaseStats.Strength, 1, 5);
            Assert.InRange(m.BaseStats.Stamina,  1, 5);
        }
    }

    [Fact]
    public void CreateRandom_ProducesVariedStats()
    {
        // With 50 mercs, the chance all five stats on every merc equal 1 is astronomically small.
        var mercs = Enumerable.Range(0, 50).Select(_ => Mercenary.CreateRandom()).ToList();
        Assert.Contains(mercs, m => m.BaseStats.Agility  > 1);
        Assert.Contains(mercs, m => m.BaseStats.Focus    > 1);
        Assert.Contains(mercs, m => m.BaseStats.Mind     > 1);
        Assert.Contains(mercs, m => m.BaseStats.Strength > 1);
        Assert.Contains(mercs, m => m.BaseStats.Stamina  > 1);
    }

    [Fact]
    public void Mercenary_HasRushDodgeAndBlock_InSkills()
    {
        var m = new Mercenary();
        Assert.Contains(m.Skills, s => s.Name == "Rush");
        Assert.Contains(m.Skills, s => s.Name == "Dodge");
        Assert.Contains(m.Skills, s => s.Name == "Block");
    }

    [Fact]
    public void Rush_HasCorrectProperties()
    {
        var m = new Mercenary(stamina: 4); // MovementPoints = 4+3 = 7, half = 3
        var rush = m.Skills.First(s => s.Name == "Rush");
        Assert.Equal(0, rush.MinDamage);
        Assert.Equal(0, rush.MaxDamage);
        Assert.Equal(1, rush.ActionPointCost);
        Assert.Equal(0, rush.Range);
        Assert.NotNull(rush.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, rush.OnHit!.Type);
        Assert.Equal(CombatStat.MovementPoints, rush.OnHit.TargetStat);
        Assert.Equal(3, rush.OnHit.MinAmount);
        Assert.Equal(3, rush.OnHit.MaxAmount);
        Assert.Equal(1, rush.OnHit.MinDuration);
        Assert.Equal(1, rush.OnHit.MaxDuration);
    }

    [Fact]
    public void Dodge_HasCorrectProperties()
    {
        var m = new Mercenary();
        var dodge = m.Skills.First(s => s.Name == "Dodge");
        Assert.Equal(0, dodge.MinDamage);
        Assert.Equal(0, dodge.MaxDamage);
        Assert.Equal(1, dodge.ActionPointCost);
        Assert.Equal(0, dodge.Range);
        Assert.NotNull(dodge.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, dodge.OnHit!.Type);
        Assert.Equal(CombatStat.Evasion, dodge.OnHit.TargetStat);
        Assert.Equal(5, dodge.OnHit.MinAmount);
        Assert.Equal(15, dodge.OnHit.MaxAmount);
        Assert.Equal(1, dodge.OnHit.MinDuration);
        Assert.Equal(1, dodge.OnHit.MaxDuration);
    }

    [Fact]
    public void Block_HasCorrectProperties()
    {
        var m = new Mercenary();
        var block = m.Skills.First(s => s.Name == "Block");
        Assert.Equal(0, block.MinDamage);
        Assert.Equal(0, block.MaxDamage);
        Assert.Equal(1, block.ActionPointCost);
        Assert.Equal(0, block.Range);
        Assert.NotNull(block.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, block.OnHit!.Type);
        Assert.Equal(CombatStat.PhysicalDefense, block.OnHit.TargetStat);
        Assert.Equal(5, block.OnHit.MinAmount);
        Assert.Equal(15, block.OnHit.MaxAmount);
        Assert.Equal(1, block.OnHit.MinDuration);
        Assert.Equal(1, block.OnHit.MaxDuration);
    }
}
