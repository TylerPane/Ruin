using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Party;
using RuinGamePDT.Weapons;
using static RuinGamePDT.Creatures.BaseStat;

namespace RuinGamePDT.Tests;

public class MercenaryTests
{
    [Fact]
    public void EquipWeapon_ReplacesEquippedWeapon()
    {
        var m = new Mercenary();
        var sword = new Sword();

        m.EquipWeapon(sword, new Banner());

        Assert.Same(sword, m.EquippedWeapon);
    }

    [Fact]
    public void EquipWeapon_ReplacesAttacksWithNewWeaponsAttacks()
    {
        var m = new Mercenary();
        var sword = new Sword();

        m.EquipWeapon(sword, new Banner());

        Assert.Equal(sword.Attacks.Count, m.Attacks.Count);
        foreach (var attack in sword.Attacks)
            Assert.Contains(attack, m.Attacks);
        foreach (var attack in new Unarmed().Attacks)
            Assert.DoesNotContain(attack, m.Attacks);
    }

    [Fact]
    public void EquipWeapon_ReturnsPreviousWeaponToBannerInventory()
    {
        var m = new Mercenary();
        var banner = new Banner();
        var sword = new Sword();
        m.EquipWeapon(sword, banner);

        var bow = new Bow();
        m.EquipWeapon(bow, banner);

        Assert.Contains(sword, banner.Inventory);
    }

    [Fact]
    public void EquipWeapon_DoesNotReturnUnarmedToInventory()
    {
        var m = new Mercenary();
        var banner = new Banner();
        var sword = new Sword();

        m.EquipWeapon(sword, banner);

        Assert.Empty(banner.Inventory);
    }

    [Fact]
    public void EquipWeapon_RemovesEquippedWeaponFromBannerInventory()
    {
        var m = new Mercenary();
        var banner = new Banner();
        var sword = new Sword();
        banner.Inventory.Add(sword);

        m.EquipWeapon(sword, banner);

        Assert.DoesNotContain(sword, banner.Inventory);
    }

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
    public void Mercenary_HasDefensiveStance_InSkills_ButNotRush()
    {
        var m = new Mercenary();
        Assert.DoesNotContain(m.Skills, s => s.Name == "Rush");
        Assert.Contains(m.Skills, s => s.Name == "Defensive Stance");
        Assert.DoesNotContain(m.Skills, s => s.Name == "Dodge");
        Assert.DoesNotContain(m.Skills, s => s.Name == "Block");
    }

    [Fact]
    public void Rush_HasCorrectProperties()
    {
        var m = new Mercenary(stamina: 4); // MovementPoints = 4+3 = 7, half = 3
        var rush = m.CreateRush();
        Assert.Equal(0, rush.MinDamage);
        Assert.Equal(0, rush.MaxDamage);
        Assert.Equal(1, rush.ActionPointCost);
        Assert.Equal(0, rush.Range);
        Assert.NotNull(rush.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, rush.OnHit!.Type);
        Assert.Single(rush.OnHit.Stats);
        Assert.Equal(CombatStat.MovementPoints, rush.OnHit.Stats[0].Stat);
        Assert.Equal(3, rush.OnHit.Stats[0].MinAmount);
        Assert.Equal(3, rush.OnHit.Stats[0].MaxAmount);
        Assert.Equal(1, rush.OnHit.MinDuration);
        Assert.Equal(1, rush.OnHit.MaxDuration);
    }

    [Fact]
    public void DefensiveStance_HasCorrectProperties()
    {
        var m = new Mercenary();
        var ds = m.Skills.First(s => s.Name == "Defensive Stance");
        Assert.Equal(0, ds.MinDamage);
        Assert.Equal(0, ds.MaxDamage);
        Assert.Equal(1, ds.ActionPointCost);
        Assert.Equal(0, ds.Range);
        Assert.NotNull(ds.OnHit);
        Assert.Equal(AttackEffectType.StatIncrease, ds.OnHit!.Type);
        Assert.Equal(2, ds.OnHit.Stats.Count);

        var evasion = ds.OnHit.Stats.First(s => s.Stat == CombatStat.Evasion);
        Assert.Equal(5,  evasion.MinAmount);
        Assert.Equal(15, evasion.MaxAmount);

        var physDef = ds.OnHit.Stats.First(s => s.Stat == CombatStat.PhysicalDefense);
        Assert.Equal(5,  physDef.MinAmount);
        Assert.Equal(15, physDef.MaxAmount);

        Assert.Equal(1, ds.OnHit.MinDuration);
        Assert.Equal(1, ds.OnHit.MaxDuration);
    }

    [Fact]
    public void Mercenary_WithMindBelow3_DoesNotHaveFirstAid()
    {
        var m = new Mercenary(mind: 2);
        Assert.DoesNotContain(m.Skills, s => s.Name == "First Aid");
    }

    [Fact]
    public void Mercenary_WithMind3_HasFirstAid()
    {
        var m = new Mercenary(mind: 3);
        Assert.Contains(m.Skills, s => s.Name == "First Aid");
    }

    [Fact]
    public void FirstAid_HasCorrectProperties()
    {
        var m = new Mercenary(mind: 4);
        var fa = m.Skills.First(s => s.Name == "First Aid");
        Assert.Equal(-4, fa.MinDamage); // -Mind
        Assert.Equal(-1, fa.MaxDamage);
        Assert.Equal(2, fa.ActionPointCost);
        Assert.Equal(1, fa.Range);
        Assert.Null(fa.OnHit);
        Assert.Null(fa.OnCrit);
    }
}
