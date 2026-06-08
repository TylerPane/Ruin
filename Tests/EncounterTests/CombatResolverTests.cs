using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class CombatResolverTests
{
    //Setup helpers
    private static Func<int, int, int> Rolls(params int[] values)
    {
        var queue = new Queue<int>(values);
        return (_, _) => queue.Dequeue();
    }

    private static (EncounterState state, Creature attacker, Creature defender) MakeFight(
        int attackerAgility = 0, int attackerFocus = 1, int attackerStrength = 0,
        int defenderAgility = 0, int defenderStrength = 0, int defenderStamina = 10)
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var attacker = new TestCreature("A", attackerAgility, attackerFocus, 1, attackerStrength, 1);
        var defender = new TestCreature("D", defenderAgility, 1, 1, defenderStrength, defenderStamina);
        state.Mercenaries.Add(attacker);
        state.Enemies.Add(defender);
        state.PlaceCreature(attacker, 0, 0);
        state.PlaceCreature(defender, 1, 0);
        return (state, attacker, defender);
    }

    private static Attack BasicAttack(int minDmg = 5, int maxDmg = 5, int accuracy = 100, int ap = 1,
        IEnumerable<(int, int)>? shape = null, AttackEffect? onHit = null, AttackEffect? onCrit = null,
        bool autoCrit = false, int minHits = 1, int maxHits = 1)
    {
        return new Attack("Test", minDmg, maxDmg, ap, accuracy,
            new AttackShape(shape ?? new[] { (0, 0) }), range: 5,
            reaction: null, onHit: onHit, onCrit: onCrit,
            minRange: 0, autoCrit: autoCrit, minHits: minHits, maxHits: maxHits);
    }

    [Fact]
    public void HitRollAtExactThresholdShould_DealDamage()
    {
        // accuracy 80, evasion 0 → threshold = 20. Roll 20 hits.
        var (state, attacker, defender) = MakeFight(attackerStrength: 1, defenderStamina: 0);
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 80);
        int hpBefore = defender.CurrentHp;

        // rolls: hitCount=1, hit=20, crit=0 (miss crit), dmg=5
        new CombatResolver(Rolls(1, 20, 0, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.True(defender.CurrentHp < hpBefore);
    }

    [Fact]
    public void RollBelowThresholdShould_Miss()
    {
        // accuracy 80, evasion 0 → threshold = 20. Roll 19 misses.
        var (state, attacker, defender) = MakeFight();
        var attack = BasicAttack(accuracy: 80);
        int hpBefore = defender.CurrentHp;

        // rolls: hitCount=1, hit=19 (miss). No further rolls consumed.
        new CombatResolver(Rolls(1, 19)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(hpBefore, defender.CurrentHp);
    }

    [Fact]
    public void EvasionShould_RaiseHitThreshold()
    {
        // accuracy 80, evasion 20 → threshold = 40. Roll 39 misses, 40 hits.
        // Defender agility 4 → Evasion = 4 * 5 = 20.
        var (state, attacker, defender) = MakeFight(attackerStrength: 1, defenderAgility: 4, defenderStamina: 0);
        var attack = BasicAttack(accuracy: 80);
        int hpBefore = defender.CurrentHp;

        new CombatResolver(Rolls(1, 39)).Resolve(attacker, attack, (1, 0), state);
        Assert.Equal(hpBefore, defender.CurrentHp);

        // Reset to a fresh fight and verify 40 lands.
        var (state2, attacker2, defender2) = MakeFight(attackerStrength: 1, defenderAgility: 4, defenderStamina: 0);
        int hp2 = defender2.CurrentHp;
        new CombatResolver(Rolls(1, 40, 0, 5)).Resolve(attacker2, attack, (1, 0), state2);
        Assert.True(defender2.CurrentHp < hp2);
    }

    [Fact]
    public void CritShould_DoubleDamageWithoutCritRoll()
    {
        var (state, attacker, defender) = MakeFight(defenderStamina: 0);
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, autoCrit: true);
        int hpBefore = defender.CurrentHp;

        // rolls: hitCount=1, hit=100, dmg=5. No crit roll because AutoCrit.
        new CombatResolver(Rolls(1, 100, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(0, defender.CurrentHp); // 5 base * 2 crit = 10 > 5 max HP, clamped to 0
    }

    [Fact]
    public void CritAtExactThresholdShould_DoublesDamage()
    {
        // attacker crit chance 25 (focus 5 → CritChance = 25).
        // Crit if roll >= 100 - 25 = 75. Roll 75 = crit.
        var (state, attacker, defender) = MakeFight(attackerFocus: 5, defenderStamina: 0);
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 100);
        int hpBefore = defender.CurrentHp;

        // rolls: hitCount=1, hit=100, crit=75, dmg=5
        new CombatResolver(Rolls(1, 100, 75, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(0, defender.CurrentHp); // 5 base * 2 crit = 10 > 5 max HP, clamped to 0
    }

    [Fact]
    public void CritBelowThresholdShould_NotCrit()
    {
        var (state, attacker, defender) = MakeFight(attackerFocus: 5, defenderStamina: 0);
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 100);
        int hpBefore = defender.CurrentHp;

        // rolls: hitCount=1, hit=100, crit=74 (one below threshold), dmg=5
        new CombatResolver(Rolls(1, 100, 74, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(hpBefore - 5, defender.CurrentHp);
    }

    [Fact]
    public void DamageShould_IncludeAttackPowerAndSubtractsDefense()
    {
        // strength 4 → AttackPower = 4. Defender stamina 2 → PhysicalDefense = 2.
        var (state, attacker, defender) = MakeFight(attackerStrength: 4, defenderStamina: 2);
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 100);
        int hpBefore = defender.CurrentHp;

        // hitCount=1, hit=100, crit=0 (no crit), dmg=5
        // final = 5 + 4 - 2 = 7
        new CombatResolver(Rolls(1, 100, 0, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(hpBefore - 7, defender.CurrentHp);
    }

    [Fact]
    public void DamageShould_BeOneWhenDefenseExceedsAttack()
    {
        var (state, attacker, defender) = MakeFight(defenderStamina: 20); // PhysDef = 20
        var attack = BasicAttack(minDmg: 1, maxDmg: 1, accuracy: 100);
        int hpBefore = defender.CurrentHp;

        new CombatResolver(Rolls(1, 100, 0, 1)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(hpBefore-1, defender.CurrentHp);
    }

    [Fact]
    public void AoEAbilityShould_HitsAllCreaturesInShape()
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var attacker = new TestCreature("A", 0, 1, 1, 1, 1);
        var d1 = new TestCreature("D1", 0, 1, 1, 0, 0);
        var d2 = new TestCreature("D2", 0, 1, 1, 0, 0);
        state.Mercenaries.Add(attacker);
        state.Enemies.Add(d1);
        state.Enemies.Add(d2);
        state.PlaceCreature(attacker, 0, 0);
        state.PlaceCreature(d1, 5, 5);
        state.PlaceCreature(d2, 6, 5);

        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 100, shape: new[] { (0, 0), (1, 0) });

        // Targeting (5,5): hits d1 at (5,5) and d2 at (6,5).
        // Rolls: hitCount=1, then per defender: hit, crit, dmg = 3 rolls each → 7 total.
        new CombatResolver(Rolls(1, 100, 0, 5, 100, 0, 5)).Resolve(attacker, attack, (5, 5), state);

        Assert.True(d1.CurrentHp < 20);
        Assert.True(d2.CurrentHp < 20);
    }

    [Fact]
    public void AoEShould_SkipEmptyTiles()
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var attacker = new TestCreature("A", 0, 1, 1, 1, 1);
        var d1 = new TestCreature("D1", 0, 1, 1, 0, 0);
        state.Mercenaries.Add(attacker);
        state.Enemies.Add(d1);
        state.PlaceCreature(attacker, 0, 0);
        state.PlaceCreature(d1, 5, 5);

        // 3-tile shape, only middle has a defender.
        var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 100, shape: new[] { (-1, 0), (0, 0), (1, 0) });

        // Only 1 defender → 3 rolls consumed (hit, crit, dmg) after hitCount.
        new CombatResolver(Rolls(1, 100, 0, 5)).Resolve(attacker, attack, (5, 5), state);

        Assert.True(d1.CurrentHp < 20f);
    }

    [Fact]
    public void Resolve_MultiHit_AppliesEachHit()
    {
        var (state, attacker, defender) = MakeFight(defenderStamina: 0);
        var attack = BasicAttack(minDmg: 2, maxDmg: 2, accuracy: 100, minHits: 3, maxHits: 3);
        float hpBefore = defender.CurrentHp;

        // hitCount=3, then 3 iterations of (hit=100, crit=0, dmg=2) = 9 rolls + 1 hitCount = 10
        new CombatResolver(Rolls(3, 100, 0, 2, 100, 0, 2, 100, 0, 2)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(0, defender.CurrentHp); // 3 hits * 2 dmg = 6 > 5 max HP, clamped to 0
    }

    [Fact]
    public void DeathShould_RemoveDefenderFromState()
    {
        var (state, attacker, defender) = MakeFight(defenderStamina: 0); // 5 HP
        var attack = BasicAttack(minDmg: 10, maxDmg: 10, accuracy: 100);

        new CombatResolver(Rolls(1, 100, 0, 10)).Resolve(attacker, attack, (1, 0), state);

        Assert.Null(state.GetCreatureAt(1, 0));
        Assert.DoesNotContain(defender, state.Enemies);
    }

    [Fact]
    public void MultiHitShould_StopOnDeath()
    {
        var (state, attacker, defender) = MakeFight(defenderStamina: 0); // 5 HP
        var attack = BasicAttack(minDmg: 10, maxDmg: 10, accuracy: 100, minHits: 3, maxHits: 3);

        // hitCount=3, but first hit kills. Only 4 rolls consumed (hitCount + hit + crit + dmg).
        new CombatResolver(Rolls(3, 100, 0, 10)).Resolve(attacker, attack, (1, 0), state);

        Assert.DoesNotContain(defender, state.Enemies);
    }

    [Fact]
    public void OnHitShould_FireOnHit()
    {
        var (state, attacker, defender) = MakeFight();
        var onHit = new AttackEffect(AttackEffectType.Bleed, CombatStat.HitPoints, 2, 2, 1, 1);
        var attack = BasicAttack(minDmg: 1, maxDmg: 1, accuracy: 100, onHit: onHit);

        new CombatResolver(Rolls(1, 100, 0, 1)).Resolve(attacker, attack, (1, 0), state);

        Assert.Single(defender.StatusEffects);
        Assert.Equal(StatusEffectType.Bleed, defender.StatusEffects[0].Type);
    }

    [Fact]
    public void OnCritShould_FireOnlyOnCrit()
    {
        var (state, attacker, defender) = MakeFight(attackerFocus: 5);
        var onCrit = new AttackEffect(AttackEffectType.Bleed, CombatStat.HitPoints, 2, 2, 1, 1);
        var attack = BasicAttack(minDmg: 1, maxDmg: 1, accuracy: 100, onCrit: onCrit);

        // First: no crit (crit roll 74 < 75 threshold)
        new CombatResolver(Rolls(1, 100, 74, 1)).Resolve(attacker, attack, (1, 0), state);
        Assert.Empty(defender.StatusEffects);

        // Second: crit (75 >= 75)
        new CombatResolver(Rolls(1, 100, 75, 1)).Resolve(attacker, attack, (1, 0), state);
        Assert.Single(defender.StatusEffects);
    }

    [Fact]
    public void UsingActionShould_DeductActionPoints()
    {
        var (state, attacker, defender) = MakeFight(attackerAgility: 10);
        int apBefore = state.GetRemainingActionPoints(attacker);
        var attack = BasicAttack(ap: 2, accuracy: 100);

        new CombatResolver(Rolls(1, 100, 0, 5)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(apBefore - 2, state.GetRemainingActionPoints(attacker));
    }

    [Fact]
    public void SkillShould_BeInRangeInclusive()
    {
        var (state, attacker, _) = MakeFight(); 
        var attack = BasicAttack();
        // attacker at (0,0), basic range = 5, minRange = 0.
        var resolver = new CombatResolver(Rolls());

        Assert.True(resolver.IsInRange(attacker, attack, (5, 0), state));
        Assert.True(resolver.IsInRange(attacker, attack, (5, 5), state));   // Chebyshev: max(5,5)=5
        Assert.False(resolver.IsInRange(attacker, attack, (6, 0), state));
        Assert.False(resolver.IsInRange(attacker, attack, (6, 6), state));
    }

    [Fact]
    public void SkillShould_RespectMinRange()
    {
        var (state, attacker, _) = MakeFight();
        var attack = new Attack("MinRangeAtk", 1, 1, 1, 100,
            new AttackShape(new[] { (0, 0) }), range: 10,
            minRange: 3);
        var resolver = new CombatResolver(Rolls());

        Assert.False(resolver.IsInRange(attacker, attack, (2, 0), state));  // too close
        Assert.True(resolver.IsInRange(attacker, attack, (3, 0), state));   // exactly min
        Assert.True(resolver.IsInRange(attacker, attack, (10, 0), state));  // exactly max
        Assert.False(resolver.IsInRange(attacker, attack, (11, 0), state)); // too far
    }

    [Fact]
    public void ZeroDamageAttackShould_NotChangeHp()
    {
        // Shout scenario: 0/0 damage, high PhysicalDefense on defender — used to cause negative dmg (heal).
        var (state, attacker, defender) = MakeFight(defenderStamina: 20);
        var attack = BasicAttack(minDmg: 0, maxDmg: 0, accuracy: 100);
        int hpBefore = defender.CurrentHp;

        new CombatResolver(Rolls(1, 100, 0, 0)).Resolve(attacker, attack, (1, 0), state);

        Assert.Equal(hpBefore, defender.CurrentHp);
    }

    [Fact]
    public void CurrentHpShould_NotExceedMaxHp()
    {
        var creature = new TestCreature("T", 0, 1, 1, 0, 5); // HitPoints = 5*2+5 = 15
        creature.CurrentHp = 10;

        creature.CurrentHp += 999;

        Assert.Equal(creature.CombatStats.HitPoints, creature.CurrentHp);
    }

    [Fact]
    public void NegativeDamageAttackShould_HealTarget()
    {
        // Defender stamina 5 → HitPoints = 5*2+5 = 15, PhysicalDefense = 5. Start at 10 HP.
        // baseDmg = -3, dmg = -3 + 0 - 5 = -8, so heals 8.
        var (state, attacker, defender) = MakeFight(defenderStamina: 5);
        defender.CurrentHp = 10;
        var attack = BasicAttack(minDmg: -3, maxDmg: -3, accuracy: 100);

        // rolls: hitCount=1, hit=100, crit=0, dmg=-3
        new CombatResolver(Rolls(1, 100, 0, -3)).Resolve(attacker, attack, (1, 0), state);

        // Defender heals: 10 + 8 = 18, clamped to 15
        Assert.Equal(15, defender.CurrentHp);
    }

    [Fact]
    public void NegativeDamageAttackShould_NotExceedMaxHp()
    {
        // Defender stamina 5 → HitPoints = 15, PhysicalDefense = 5. Already at 14 HP.
        // baseDmg = -5, dmg = -5 + 0 - 5 = -10, so heals 10.
        var (state, attacker, defender) = MakeFight(defenderStamina: 5);
        defender.CurrentHp = 14;
        var attack = BasicAttack(minDmg: -5, maxDmg: -5, accuracy: 100);

        new CombatResolver(Rolls(1, 100, 0, -5)).Resolve(attacker, attack, (1, 0), state);

        // Defender heals: 14 + 10 = 24, clamped to 15
        Assert.Equal(15, defender.CurrentHp);
    }
}
