# Defensive Stance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Mercenary's Dodge and Block skills with a single Defensive Stance skill that buffs Evasion and PhysicalDefense by 5–15 each for 1 round / 1 AP.

**Architecture:** Refactor `AttackEffect` from single-stat to a `IReadOnlyList<StatChange>` model; update `Creature.ApplyStatusEffect` to loop that list; update `Mercenary` to register Defensive Stance instead of Dodge and Block. All existing single-stat skills wrap one `StatChange` in the list.

**Tech Stack:** C# / .NET 8 / xUnit

---

## File Map

| File | Change |
|------|--------|
| `Scripts/Combat/AttackEffect.cs` | Add `StatChange` record; replace `TargetStat`/`MinAmount`/`MaxAmount` with `IReadOnlyList<StatChange> Stats` |
| `Scripts/Creatures/Creature.cs` | Update `ApplyStatusEffect` to loop `effect.Stats` |
| `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs` | Replace Dodge + Block with Defensive Stance |
| `Tests/CombatTests/CreatureStatusEffectTests.cs` | Update existing tests; add multi-stat test |
| `Tests/MercenaryTests.cs` | Replace Dodge/Block tests with Defensive Stance tests; update Rush property test |
| `Tests/EncounterTests/CombatResolverTests.cs` | Update `AttackEffect` construction calls |

---

## Task 1: Refactor `AttackEffect` — add `StatChange`, update constructor

**Files:**
- Modify: `Scripts/Combat/AttackEffect.cs`

- [ ] **Step 1: Replace contents of `AttackEffect.cs`**

```csharp
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public record StatChange(CombatStat Stat, int MinAmount, int MaxAmount);

public record AttackEffect(
    AttackEffectType Type,
    IReadOnlyList<StatChange> Stats,
    int MinDuration,
    int MaxDuration
);
```

- [ ] **Step 2: Verify project fails to build (expected — consumers still use old signature)**

Run: `dotnet build`
Expected: compile errors referencing `TargetStat`, `MinAmount`, `MaxAmount` on `AttackEffect`

---

## Task 2: Update `Creature.ApplyStatusEffect`

**Files:**
- Modify: `Scripts/Creatures/Creature.cs` — `ApplyStatusEffect` method (~line 116)

- [ ] **Step 1: Replace `ApplyStatusEffect`**

```csharp
public void ApplyStatusEffect(AttackEffect effect)
{
    int duration = Random.Shared.Next(effect.MinDuration, effect.MaxDuration + 1);
    foreach (var statChange in effect.Stats)
    {
        var statusEffect = new StatusEffect(
            (StatusEffectType)effect.Type,
            statChange.Stat,
            Random.Shared.Next(statChange.MinAmount, statChange.MaxAmount + 1),
            duration
        );
        StatusEffects.Add(statusEffect);
    }
}
```

- [ ] **Step 2: Verify build still fails only in test/consumer files, not in `Creature.cs`**

Run: `dotnet build`
Expected: compile errors only in `Mercenary.cs`, `CombatResolverTests.cs`, `CreatureStatusEffectTests.cs`, `MercenaryTests.cs`

---

## Task 3: Update `Mercenary` — replace Dodge + Block with Defensive Stance; fix Rush

**Files:**
- Modify: `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs`

- [ ] **Step 1: Replace Rush, Dodge, Block skill registrations**

Old Rush `onHit`:
```csharp
onHit: new AttackEffect(AttackEffectType.StatIncrease, CombatStat.MovementPoints, mpHalf, mpHalf, 1, 1)
```

New Rush `onHit`:
```csharp
onHit: new AttackEffect(AttackEffectType.StatIncrease,
    new[] { new StatChange(CombatStat.MovementPoints, mpHalf, mpHalf) },
    MinDuration: 1, MaxDuration: 1)
```

Remove the entire Dodge skill block and the entire Block skill block.

Add after Rush:
```csharp
Skills.Add(new Skill(
    name: "Defensive Stance",
    minDamage: 0,
    maxDamage: 0,
    actionPointCost: 1,
    accuracy: 100,
    attackShape: new AttackShape(new[] { (0, 0) }),
    range: 0,
    onHit: new AttackEffect(
        AttackEffectType.StatIncrease,
        new[]
        {
            new StatChange(CombatStat.Evasion, 5, 15),
            new StatChange(CombatStat.PhysicalDefense, 5, 15)
        },
        MinDuration: 1,
        MaxDuration: 1)
));
```

The final `Mercenary.cs` constructor body should look like:

```csharp
EquippedWeapon = new Unarmed();
Attacks.AddRange(EquippedWeapon.Attacks);

int mpHalf = CombatStats.MovementPoints / 2;

Skills.Add(new Skill(
    name: "Rush",
    minDamage: 0,
    maxDamage: 0,
    actionPointCost: 1,
    accuracy: 100,
    attackShape: new AttackShape(new[] { (0, 0) }),
    range: 0,
    onHit: new AttackEffect(AttackEffectType.StatIncrease,
        new[] { new StatChange(CombatStat.MovementPoints, mpHalf, mpHalf) },
        MinDuration: 1, MaxDuration: 1)
));

Skills.Add(new Skill(
    name: "Defensive Stance",
    minDamage: 0,
    maxDamage: 0,
    actionPointCost: 1,
    accuracy: 100,
    attackShape: new AttackShape(new[] { (0, 0) }),
    range: 0,
    onHit: new AttackEffect(
        AttackEffectType.StatIncrease,
        new[]
        {
            new StatChange(CombatStat.Evasion, 5, 15),
            new StatChange(CombatStat.PhysicalDefense, 5, 15)
        },
        MinDuration: 1,
        MaxDuration: 1)
));

if (BaseStats.Mind >= 3)
{
    Skills.Add(new Skill(
        name: "First Aid",
        minDamage: -BaseStats.Mind,
        maxDamage: -1,
        actionPointCost: 2,
        accuracy: 100,
        attackShape: new AttackShape(new[] { (0, 0) }),
        range: 1
    ));
}
```

- [ ] **Step 2: Verify build fails only in test files now**

Run: `dotnet build`
Expected: errors only in test files

---

## Task 4: Update `CombatResolverTests` — fix `AttackEffect` construction

**Files:**
- Modify: `Tests/EncounterTests/CombatResolverTests.cs`

All `new AttackEffect(...)` calls in this file use the old 6-arg positional form. Update each one.

- [ ] **Step 1: Update `BasicAttack` helper and all test `AttackEffect` usages**

In `BasicAttack` helper, `onHit` and `onCrit` parameters are passed through unchanged — no change needed there. The two tests that construct `AttackEffect` directly are `OnHitShould_FireOnHit` and `OnCritShould_FireOnlyOnCrit`.

Update `OnHitShould_FireOnHit`:
```csharp
var onHit = new AttackEffect(AttackEffectType.Bleed,
    new[] { new StatChange(CombatStat.HitPoints, 2, 2) },
    MinDuration: 1, MaxDuration: 1);
```

Update `OnCritShould_FireOnlyOnCrit`:
```csharp
var onCrit = new AttackEffect(AttackEffectType.Bleed,
    new[] { new StatChange(CombatStat.HitPoints, 2, 2) },
    MinDuration: 1, MaxDuration: 1);
```

- [ ] **Step 2: Verify build fails only in remaining test files**

Run: `dotnet build`
Expected: errors only in `CreatureStatusEffectTests.cs` and `MercenaryTests.cs`

---

## Task 5: Update `CreatureStatusEffectTests`

**Files:**
- Modify: `Tests/CombatTests/CreatureStatusEffectTests.cs`

- [ ] **Step 1: Rewrite test file**

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Tests;

public class CreatureStatusEffectTests
{
    [Fact]
    public void ApplyStatusEffect_WithEqualMinMax_ProducesExactValue()
    {
        var merc = new Mercenary();
        var effect = new AttackEffect(AttackEffectType.Bleed,
            new[] { new StatChange(CombatStat.HitPoints, 5, 5) },
            MinDuration: 2, MaxDuration: 2);

        merc.ApplyStatusEffect(effect);

        Assert.Single(merc.StatusEffects);
        Assert.Equal(5, merc.StatusEffects[0].Amount);
        Assert.Equal(2, merc.StatusEffects[0].Duration);
    }

    [Fact]
    public void ApplyStatusEffect_InclusiveUpperBound_CanProduceMaxValue()
    {
        var merc = new Mercenary();
        int observedMaxAmount = int.MinValue;
        int observedMaxDuration = int.MinValue;

        for (int i = 0; i < 200; i++)
        {
            merc.StatusEffects.Clear();
            merc.ApplyStatusEffect(new AttackEffect(AttackEffectType.Bleed,
                new[] { new StatChange(CombatStat.HitPoints, 1, 3) },
                MinDuration: 1, MaxDuration: 3));
            observedMaxAmount   = Math.Max(observedMaxAmount,   merc.StatusEffects[0].Amount);
            observedMaxDuration = Math.Max(observedMaxDuration, merc.StatusEffects[0].Duration);
        }

        Assert.Equal(3, observedMaxAmount);
        Assert.Equal(3, observedMaxDuration);
    }

    [Fact]
    public void ApplyStatusEffect_PropagatesTargetStat_FromStatChange()
    {
        var merc = new Mercenary();
        var effect = new AttackEffect(AttackEffectType.StatReduction,
            new[] { new StatChange(CombatStat.Accuracy, 3, 3) },
            MinDuration: 1, MaxDuration: 1);

        merc.ApplyStatusEffect(effect);

        Assert.Single(merc.StatusEffects);
        Assert.Equal(CombatStat.Accuracy, merc.StatusEffects[0].TargetStat);
    }

    [Fact]
    public void ApplyStatusEffect_MultiStat_CreatesOneStatusEffectPerStat()
    {
        var merc = new Mercenary();
        var effect = new AttackEffect(AttackEffectType.StatIncrease,
            new[]
            {
                new StatChange(CombatStat.Evasion, 10, 10),
                new StatChange(CombatStat.PhysicalDefense, 5, 5)
            },
            MinDuration: 1, MaxDuration: 1);

        merc.ApplyStatusEffect(effect);

        Assert.Equal(2, merc.StatusEffects.Count);
        Assert.Contains(merc.StatusEffects, s => s.TargetStat == CombatStat.Evasion && s.Amount == 10);
        Assert.Contains(merc.StatusEffects, s => s.TargetStat == CombatStat.PhysicalDefense && s.Amount == 5);
    }

    [Fact]
    public void ApplyStatusEffect_MultiStat_AllShareSameDuration()
    {
        var merc = new Mercenary();
        var effect = new AttackEffect(AttackEffectType.StatIncrease,
            new[]
            {
                new StatChange(CombatStat.Evasion, 5, 5),
                new StatChange(CombatStat.PhysicalDefense, 5, 5)
            },
            MinDuration: 2, MaxDuration: 2);

        merc.ApplyStatusEffect(effect);

        Assert.Equal(2, merc.StatusEffects.Count);
        Assert.All(merc.StatusEffects, s => Assert.Equal(2, s.Duration));
    }
}
```

- [ ] **Step 2: Verify build fails only in `MercenaryTests.cs`**

Run: `dotnet build`
Expected: errors only in `MercenaryTests.cs`

---

## Task 6: Update `MercenaryTests`

**Files:**
- Modify: `Tests/MercenaryTests.cs`

- [ ] **Step 1: Replace Dodge/Block tests with Defensive Stance; fix Rush property test**

Replace the `Mercenary_HasRushDodgeAndBlock_InSkills` test:
```csharp
[Fact]
public void Mercenary_HasRushAndDefensiveStance_InSkills()
{
    var m = new Mercenary();
    Assert.Contains(m.Skills, s => s.Name == "Rush");
    Assert.Contains(m.Skills, s => s.Name == "Defensive Stance");
    Assert.DoesNotContain(m.Skills, s => s.Name == "Dodge");
    Assert.DoesNotContain(m.Skills, s => s.Name == "Block");
}
```

Replace the `Rush_HasCorrectProperties` test:
```csharp
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
    Assert.Single(rush.OnHit.Stats);
    Assert.Equal(CombatStat.MovementPoints, rush.OnHit.Stats[0].Stat);
    Assert.Equal(3, rush.OnHit.Stats[0].MinAmount);
    Assert.Equal(3, rush.OnHit.Stats[0].MaxAmount);
    Assert.Equal(1, rush.OnHit.MinDuration);
    Assert.Equal(1, rush.OnHit.MaxDuration);
}
```

Remove the `Dodge_HasCorrectProperties` and `Block_HasCorrectProperties` tests entirely.

Add in their place:
```csharp
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
```

- [ ] **Step 2: Build and run all tests**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 3: Commit**

```bash
git add Scripts/Combat/AttackEffect.cs Scripts/Creatures/Creature.cs Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs Tests/CombatTests/CreatureStatusEffectTests.cs Tests/MercenaryTests.cs Tests/EncounterTests/CombatResolverTests.cs
git commit -m "feat: replace Dodge+Block with Defensive Stance; refactor AttackEffect to multi-stat"
```
