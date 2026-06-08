# Mercenary Skills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four active skills (Rush, Dodge, Block, First Aid) to mercenaries, modeled as `Skill : Attack` subclass instances stored in a new `Creature.Skills` list.

**Architecture:** `Skill` subclasses `Attack` as a pure type marker with no new fields. `Creature` gains a `Skills` property. `Mercenary` populates it at construction. `CombatResolver` gets a one-line fix to let negative damage (heals) pass through unchanged.

**Tech Stack:** C# / .NET 8, xUnit

---

## File Map

| Action | File | What changes |
|---|---|---|
| Create | `Scripts/Combat/Skill.cs` | New `Skill : Attack` subclass |
| Modify | `Scripts/Creatures/Creature.cs` | Add `public List<Skill> Skills { get; } = [];` |
| Modify | `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs` | Populate `Skills` in constructor |
| Modify | `Scripts/Encounter/CombatResolver.cs` | Allow negative `MaxDamage` to pass through |
| Create | `Tests/CombatTests/SkillTests.cs` | Tests for `Skill` type identity |
| Modify | `Tests/MercenaryTests.cs` | Tests for skill list population |
| Modify | `Tests/EncounterTests/CombatResolverTests.cs` | Test for negative-damage (heal) resolution |

---

## Task 1: `Skill` class and type-identity test

**Files:**
- Create: `Scripts/Combat/Skill.cs`
- Create: `Tests/CombatTests/SkillTests.cs`

- [ ] **Step 1: Write the failing test**

Add `Tests/CombatTests/SkillTests.cs`:

```csharp
using RuinGamePDT.Combat;

namespace RuinGamePDT.Tests;

public class SkillTests
{
    [Fact]
    public void Skill_IsAssignableFromAttack()
    {
        Assert.True(typeof(Attack).IsAssignableFrom(typeof(Skill)));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test --filter "Skill_IsAssignableFromAttack"
```

Expected: compile error — `Skill` does not exist.

- [ ] **Step 3: Create `Skill` class**

Create `Scripts/Combat/Skill.cs`:

```csharp
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public class Skill(
    string name,
    int minDamage,
    int maxDamage,
    int actionPointCost,
    int accuracy,
    AttackShape attackShape,
    int range,
    Reaction? reaction = null,
    AttackEffect? onHit = null,
    AttackEffect? onCrit = null,
    int minRange = 0,
    bool autoCrit = false,
    int minHits = 1,
    int maxHits = 1)
    : Attack(name, minDamage, maxDamage, actionPointCost, accuracy, attackShape, range,
             reaction, onHit, onCrit, minRange, autoCrit, minHits, maxHits);
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test --filter "Skill_IsAssignableFromAttack"
```

Expected: PASS

- [ ] **Step 5: Commit**

```
git add Scripts/Combat/Skill.cs Tests/CombatTests/SkillTests.cs
git commit -m "feat: add Skill subclass of Attack"
```

---

## Task 2: `Creature.Skills` property

**Files:**
- Modify: `Scripts/Creatures/Creature.cs`
- Modify: `Tests/CreatureTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `Tests/CreatureTests.cs` inside `CreatureTests`:

```csharp
[Fact]
public void Creature_HasEmptySkillsListByDefault()
{
    var c = Make();
    Assert.Empty(c.Skills);
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test --filter "Creature_HasEmptySkillsListByDefault"
```

Expected: compile error — `Skills` does not exist on `Creature`.

- [ ] **Step 3: Add `Skills` property to `Creature`**

In `Scripts/Creatures/Creature.cs`, add this line immediately after the `Attacks` property (around line 64):

```csharp
public List<Skill> Skills { get; } = [];
```

Also add the using at the top if not already present:

```csharp
using RuinGamePDT.Combat;
```

- [ ] **Step 4: Run test to verify it passes**

```
dotnet test --filter "Creature_HasEmptySkillsListByDefault"
```

Expected: PASS

- [ ] **Step 5: Commit**

```
git add Scripts/Creatures/Creature.cs Tests/CreatureTests.cs
git commit -m "feat: add Skills list to Creature"
```

---

## Task 3: Mercenary Rush, Dodge, and Block skills

**Files:**
- Modify: `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs`
- Modify: `Tests/MercenaryTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Tests/MercenaryTests.cs` inside `MercenaryTests`:

```csharp
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
```

Also add to the using section at the top if not already present:
```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test --filter "Rush_HasCorrectProperties|Dodge_HasCorrectProperties|Block_HasCorrectProperties|Mercenary_HasRushDodgeAndBlock"
```

Expected: FAIL — `Skills` is empty.

- [ ] **Step 3: Add Rush, Dodge, Block to `Mercenary` constructor**

In `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs`, update the public constructor:

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Creatures;

public class Mercenary : Creature
{
    protected override int StatCap => 10;

    public Mercenary(int agility = 1, int focus = 1, int mind = 1, int strength = 1, int stamina = 1)
        : base("Mercenary", agility, focus, mind, strength, stamina)
    {
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
            onHit: new AttackEffect(AttackEffectType.StatIncrease, CombatStat.MovementPoints, mpHalf, mpHalf, 1, 1)
        ));

        Skills.Add(new Skill(
            name: "Dodge",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 0,
            onHit: new AttackEffect(AttackEffectType.StatIncrease, CombatStat.Evasion, 5, 15, 1, 1)
        ));

        Skills.Add(new Skill(
            name: "Block",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 0,
            onHit: new AttackEffect(AttackEffectType.StatIncrease, CombatStat.PhysicalDefense, 5, 15, 1, 1)
        ));
    }

    protected Mercenary(string name, int agility, int focus, int mind, int strength, int stamina)
        : base(name, agility, focus, mind, strength, stamina) { }

    public static Mercenary CreateRandom() => new(
        agility:  Random.Shared.Next(1, 6),
        focus:    Random.Shared.Next(1, 6),
        mind:     Random.Shared.Next(1, 6),
        strength: Random.Shared.Next(1, 6),
        stamina:  Random.Shared.Next(1, 6)
    );
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test --filter "Rush_HasCorrectProperties|Dodge_HasCorrectProperties|Block_HasCorrectProperties|Mercenary_HasRushDodgeAndBlock"
```

Expected: PASS

- [ ] **Step 5: Run full suite to check for regressions**

```
dotnet test
```

Expected: all pass.

- [ ] **Step 6: Commit**

```
git add Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs Tests/MercenaryTests.cs
git commit -m "feat: add Rush, Dodge, and Block skills to Mercenary"
```

---

## Task 4: First Aid skill (Mind prerequisite)

**Files:**
- Modify: `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs`
- Modify: `Tests/MercenaryTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Tests/MercenaryTests.cs` inside `MercenaryTests`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test --filter "FirstAid|WithMind"
```

Expected: FAIL — First Aid not in skills list.

- [ ] **Step 3: Add First Aid to `Mercenary` constructor**

In `Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs`, add this block after the Block skill definition (still inside the public constructor):

```csharp
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

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test --filter "FirstAid|WithMind"
```

Expected: PASS

- [ ] **Step 5: Run full suite to check for regressions**

```
dotnet test
```

Expected: all pass.

- [ ] **Step 6: Commit**

```
git add Scripts/Creatures/Humanoids/Mercenaries/Mercenary.cs Tests/MercenaryTests.cs
git commit -m "feat: add First Aid skill to Mercenary (requires Mind >= 3)"
```

---

## Task 5: `CombatResolver` negative-damage (heal) support

**Files:**
- Modify: `Scripts/Encounter/CombatResolver.cs`
- Modify: `Tests/EncounterTests/CombatResolverTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `Tests/EncounterTests/CombatResolverTests.cs` inside `CombatResolverTests`:

```csharp
[Fact]
public void NegativeDamageAttackShould_HealTarget()
{
    // Defender stamina 5 → HitPoints = 5*2+5 = 15. Start at 10 HP, heal for 3.
    var (state, attacker, defender) = MakeFight(defenderStamina: 5);
    defender.CurrentHp = 10;
    var attack = BasicAttack(minDmg: -3, maxDmg: -3, accuracy: 100);

    // rolls: hitCount=1, hit=100, crit=0, dmg=-3
    new CombatResolver(Rolls(1, 100, 0, -3)).Resolve(attacker, attack, (1, 0), state);

    Assert.Equal(13, defender.CurrentHp);
}

[Fact]
public void NegativeDamageAttackShould_NotExceedMaxHp()
{
    // Defender stamina 5 → HitPoints = 15. Already at 14 HP, heal for 5.
    var (state, attacker, defender) = MakeFight(defenderStamina: 5);
    defender.CurrentHp = 14;
    var attack = BasicAttack(minDmg: -5, maxDmg: -5, accuracy: 100);

    new CombatResolver(Rolls(1, 100, 0, -5)).Resolve(attacker, attack, (1, 0), state);

    Assert.Equal(15, defender.CurrentHp); // clamped at max
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test --filter "NegativeDamageAttack"
```

Expected: FAIL — heal currently treated as 0 damage (the `else dmg = 0` branch fires for any non-positive MaxDamage).

- [ ] **Step 3: Fix the damage branch in `CombatResolver`**

In `Scripts/Encounter/CombatResolver.cs`, replace:

```csharp
if (attack.MaxDamage > 0) dmg = Math.Max(1, dmg);
else dmg = 0;
```

With:

```csharp
if (attack.MaxDamage > 0)       dmg = Math.Max(1, dmg);
else if (attack.MaxDamage == 0) dmg = 0;
// MaxDamage < 0: negative dmg is a heal — CurrentHp clamp handles the cap
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test --filter "NegativeDamageAttack"
```

Expected: PASS

- [ ] **Step 5: Run full suite to check for regressions**

```
dotnet test
```

Expected: all pass.

- [ ] **Step 6: Commit**

```
git add Scripts/Encounter/CombatResolver.cs Tests/EncounterTests/CombatResolverTests.cs
git commit -m "feat: support negative damage (heals) in CombatResolver"
```
