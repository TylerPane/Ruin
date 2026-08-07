# Attack Cooldowns and Enemy Attack Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give attacks a per-use cooldown (Skewer: 1 round, Quill Spray: 3 rounds), and cap enemy attacks per turn (default 1) instead of `EnemyAI` looping until AP runs out.

**Architecture:** `Attack` gains a `Cooldown` field (default 0, meaning "no cooldown" — every existing attack and every mercenary attack/skill keeps this default). `Creature` gains a `MaxAttacksPerTurn` field (default 1) and private cooldown-tracking state (`_cooldownsRemaining`), with `IsOnCooldown`/`StartCooldown`/`TickCooldowns` methods. `CombatResolver.Resolve` starts a cooldown after using an attack that has one. `TurnManager.AdvanceToNextTurn` ticks cooldowns down for the creature whose turn is starting, alongside the existing `TickStatusEffects()` call. `EnemyAI.ChooseBestAttack` skips cooldown-blocked attacks; `EnemyAI.TakeTurn` stops after `MaxAttacksPerTurn` attacks.

**Tech Stack:** C# / .NET 8 / xUnit

## Global Constraints

- "1 round" / "3 rounds" cooldown means the creature's own next 1 or 3 turns, ticked the same way `StatusEffect` durations already tick via `TickStatusEffects()` — not a full encounter round.
- `Attack.Cooldown` defaults to `0` (no cooldown). Every existing `Attack` constructor call across the codebase (mercenary weapons, mercenary skills, existing enemy attacks other than Skewer/Quill Spray) must continue compiling unchanged — `Cooldown` is a new optional trailing parameter.
- `Creature.MaxAttacksPerTurn` defaults to `1`. No existing creature needs to override it in this plan — Prickleback Goblin's default of 1 attack per turn is the desired behavior described in the spec.
- Cooldowns apply uniformly regardless of who uses the attack (`CombatResolver.Resolve` is the single funnel point), even though only enemy attacks are expected to carry a nonzero `Cooldown` today.
- Several existing `EnemyAITests` assert the OLD "attacks until AP exhausted" behavior and must be updated in this plan to match the new default-1-attack-per-turn behavior — this is a required part of Task 4, not an incidental side effect.

---

## File Map

| File | Change |
|------|--------|
| `Scripts/Combat/Attack.cs` | Add `Cooldown` property + optional constructor parameter |
| `Scripts/Creatures/Creature.cs` | Add `MaxAttacksPerTurn` field, `_cooldownsRemaining` dict, `IsOnCooldown`/`StartCooldown`/`TickCooldowns` methods |
| `Scripts/Encounter/CombatResolver.cs` | Start the attacker's cooldown after resolving an attack with `Cooldown > 0` |
| `Scripts/Encounter/TurnManager.cs` | Call `creature.TickCooldowns()` alongside the existing `TickStatusEffects()` call |
| `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs` | Give Skewer `Cooldown: 1`, Quill Spray `Cooldown: 3` |
| `Scripts/Encounter/EnemyAI.cs` | `ChooseBestAttack` skips cooldown-blocked attacks; `TakeTurn` stops after `MaxAttacksPerTurn` attacks |
| `Tests/CombatTests/AttackCooldownTests.cs` | Create — tests for `Creature`'s cooldown methods and `CombatResolver` starting cooldowns |
| `Tests/EncounterTests/EnemyAITests.cs` | Modify — update tests that assumed multi-attack-per-turn behavior; add cooldown-fallback and attack-cap tests |

---

## Task 1: `Attack.Cooldown` field

**Files:**
- Modify: `Scripts/Combat/Attack.cs`

**Interfaces:**
- Produces: `Attack.Cooldown` (`int`, public getter), new optional constructor parameter `int cooldown = 0` (added after the existing optional parameters, so no existing call site needs updating). Consumed by `CombatResolver` (Task 3), `PricklebackGoblin` (Task 5).

- [ ] **Step 1: Add the `Cooldown` property and constructor parameter**

Replace the full contents of `Scripts/Combat/Attack.cs`:

```csharp
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public class Attack
{
    public string Name { get; }
    public int MinDamage { get; }
    public int MaxDamage { get; }
    public int ActionPointCost { get; }
    public int Accuracy { get; }
    public AttackShape AttackShape { get; }
    public int Range { get; }
    public int MinRange { get; }
    public bool AutoCrit { get; }
    public int MinHits { get; }
    public int MaxHits { get; }
    public int Cooldown { get; }
    public Reaction? Reaction { get; }
    public AttackEffect? OnHit { get; }
    public AttackEffect? OnCrit { get; }

    public Attack(string name, int minDamage, int maxDamage, int actionPointCost, int accuracy, AttackShape attackShape, int range,
        Reaction? reaction = null, AttackEffect? onHit = null, AttackEffect? onCrit = null,
        int minRange = 0, bool autoCrit = false, int minHits = 1, int maxHits = 1, int cooldown = 0)
    {
        Name = name;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        ActionPointCost = actionPointCost;
        Accuracy = accuracy;
        AttackShape = attackShape;
        Range = range;
        MinRange = minRange;
        AutoCrit = autoCrit;
        MinHits = minHits;
        MaxHits = maxHits;
        Cooldown = cooldown;
        Reaction = reaction;
        OnHit = onHit;
        OnCrit = onCrit;
    }
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)` — every existing `new Attack(...)` call site still compiles since `cooldown` is a new trailing optional parameter.

- [ ] **Step 3: Run full test suite to confirm no regression**

Run: `dotnet test`
Expected: all existing tests still pass (pure additive change, no behavior change yet)

- [ ] **Step 4: Commit**

```bash
git add Scripts/Combat/Attack.cs
git commit -m "feat: add Attack.Cooldown field, defaults to 0 (no cooldown)"
```

---

## Task 2: `Creature` cooldown tracking and `MaxAttacksPerTurn`

**Files:**
- Modify: `Scripts/Creatures/Creature.cs`
- Test: `Tests/CombatTests/AttackCooldownTests.cs`

**Interfaces:**
- Consumes: `Attack.Cooldown` (Task 1).
- Produces (consumed by `CombatResolver` in Task 3, `TurnManager` in Task 4, `EnemyAI` in Task 6):
  - `Creature.MaxAttacksPerTurn` — `int` property, default `1`.
  - `Creature.IsOnCooldown(Attack attack) : bool`.
  - `Creature.StartCooldown(Attack attack) : void` — sets the attack's remaining cooldown to `attack.Cooldown`. No-ops if `attack.Cooldown <= 0`.
  - `Creature.TickCooldowns() : void` — decrements every tracked cooldown by 1, removing entries that reach 0.

- [ ] **Step 1: Write failing tests**

Create `Tests/CombatTests/AttackCooldownTests.cs`:

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Tests;

public class AttackCooldownTests
{
    private static Attack MakeAttack(int cooldown) =>
        new("Test", 1, 3, 1, 100, new AttackShape(new[] { (0, 0) }), 1, cooldown: cooldown);

    [Fact]
    public void IsOnCooldown_BeforeAnyUse_ReturnsFalse()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);
        var attack = MakeAttack(cooldown: 3);

        Assert.False(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void StartCooldown_ZeroCooldownAttack_NeverGoesOnCooldown()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);
        var attack = MakeAttack(cooldown: 0);

        creature.StartCooldown(attack);

        Assert.False(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void StartCooldown_PositiveCooldown_MarksOnCooldown()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);
        var attack = MakeAttack(cooldown: 1);

        creature.StartCooldown(attack);

        Assert.True(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void TickCooldowns_OneRoundCooldown_ClearsAfterOneTick()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);
        var attack = MakeAttack(cooldown: 1);
        creature.StartCooldown(attack);

        creature.TickCooldowns();

        Assert.False(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void TickCooldowns_ThreeRoundCooldown_RemainsOnCooldownUntilThirdTick()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);
        var attack = MakeAttack(cooldown: 3);
        creature.StartCooldown(attack);

        creature.TickCooldowns();
        Assert.True(creature.IsOnCooldown(attack));

        creature.TickCooldowns();
        Assert.True(creature.IsOnCooldown(attack));

        creature.TickCooldowns();
        Assert.False(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void MaxAttacksPerTurn_DefaultsToOne()
    {
        var creature = new TestCreature("T", 1, 1, 1, 1, 1);

        Assert.Equal(1, creature.MaxAttacksPerTurn);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AttackCooldownTests`
Expected: FAIL — `IsOnCooldown`/`StartCooldown`/`TickCooldowns`/`MaxAttacksPerTurn` do not exist on `Creature`

- [ ] **Step 3: Implement the additions on `Creature`**

In `Scripts/Creatures/Creature.cs`, add a `using RuinGamePDT.Combat;` import at the top if not already present (it already imports `RuinGamePDT.Combat` at line 1 — no change needed there).

Add a new field and three new methods. Insert them right after the `Weapon? EquippedWeapon { get; set; }` line (currently line 73):

```csharp
    public Weapon? EquippedWeapon { get; set; }
    public int MaxAttacksPerTurn { get; set; } = 1;

    private readonly Dictionary<Attack, int> _cooldownsRemaining = new();

    public bool IsOnCooldown(Attack attack) =>
        _cooldownsRemaining.TryGetValue(attack, out int remaining) && remaining > 0;

    public void StartCooldown(Attack attack)
    {
        if (attack.Cooldown <= 0) return;
        _cooldownsRemaining[attack] = attack.Cooldown;
    }

    public void TickCooldowns()
    {
        foreach (var attack in _cooldownsRemaining.Keys.ToList())
        {
            _cooldownsRemaining[attack]--;
            if (_cooldownsRemaining[attack] <= 0)
                _cooldownsRemaining.Remove(attack);
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter AttackCooldownTests`
Expected: all 6 pass

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 6: Commit**

```bash
git add Scripts/Creatures/Creature.cs Tests/CombatTests/AttackCooldownTests.cs
git commit -m "feat: add per-attack cooldown tracking and MaxAttacksPerTurn to Creature"
```

---

## Task 3: `CombatResolver` starts cooldowns on use

**Files:**
- Modify: `Scripts/Encounter/CombatResolver.cs`
- Modify: `Tests/EncounterTests/CombatResolverTests.cs`

**Interfaces:**
- Consumes: `Attack.Cooldown` (Task 1), `Creature.StartCooldown` (Task 2).

No signature change to `Resolve` — this task only adds a call to `attacker.StartCooldown(attack)` once, at the end of a successful `Resolve` call (regardless of how many individual hits/misses occurred within it).

- [ ] **Step 1: Write failing tests**

Add to `Tests/EncounterTests/CombatResolverTests.cs` (near the other tests, using the existing `MakeFight`/`Rolls`/`BasicAttack` helpers already in that file):

```csharp
    [Fact]
    public void Resolve_AttackWithCooldown_StartsCooldownOnAttacker()
    {
        var (state, attacker, defender) = MakeFight();
        var attack = new Attack("Cooldown Test", 1, 1, 1, 100,
            new AttackShape(new[] { (0, 0) }), range: 5, cooldown: 2);

        new CombatResolver(Rolls(1, 100, 0, 1)).Resolve(attacker, attack, (1, 0), state);

        Assert.True(attacker.IsOnCooldown(attack));
    }

    [Fact]
    public void Resolve_AttackWithoutCooldown_NeverStartsCooldown()
    {
        var (state, attacker, defender) = MakeFight();
        var attack = BasicAttack(minDmg: 1, maxDmg: 1, accuracy: 100);

        new CombatResolver(Rolls(1, 100, 0, 1)).Resolve(attacker, attack, (1, 0), state);

        Assert.False(attacker.IsOnCooldown(attack));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter CombatResolverTests`
Expected: FAIL — `Resolve_AttackWithCooldown_StartsCooldownOnAttacker` fails because the cooldown is never started

- [ ] **Step 3: Add the `StartCooldown` call in `Resolve`**

In `Scripts/Encounter/CombatResolver.cs`, replace:

```csharp
    public List<CombatLogEntry> Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        state.SpendActionPoints(attacker, attack.ActionPointCost);
        int hitCount = roll(attack.MinHits, attack.MaxHits + 1);
```

With:

```csharp
    public List<CombatLogEntry> Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        state.SpendActionPoints(attacker, attack.ActionPointCost);
        attacker.StartCooldown(attack);
        int hitCount = roll(attack.MinHits, attack.MaxHits + 1);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter CombatResolverTests`
Expected: all pass, including the 2 new tests

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 6: Commit**

```bash
git add Scripts/Encounter/CombatResolver.cs Tests/EncounterTests/CombatResolverTests.cs
git commit -m "feat: CombatResolver starts the attacker's cooldown when resolving an attack"
```

---

## Task 4: `TurnManager` ticks cooldowns

**Files:**
- Modify: `Scripts/Encounter/TurnManager.cs`
- Test: `Tests/EncounterTests/TurnManagerTests.cs`

**Interfaces:**
- Consumes: `Creature.TickCooldowns` (Task 2).

- [ ] **Step 1: Write failing tests**

`TurnManager.StartEncounter()` calls `AdvanceToNextTurn()` exactly once. With a single creature in the encounter, that creature is unconditionally the one and only entry in `_turnOrder`, so it is guaranteed to be `CurrentCreature` after `StartEncounter()` — no `if (tm.CurrentCreature == ...)` guard is needed (unlike tests with 2+ creatures, where turn order is randomized by a tiebreaker and existing tests in this file correctly guard on it). Each subsequent `EndCreatureTurn(creature)` call advances back to that same creature (the only one), ticking once more.

Add these tests to `Tests/EncounterTests/TurnManagerTests.cs`:

```csharp
    [Fact]
    public void StartEncounter_TicksCooldowns_ForTheSoleCreature()
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var creature = new TestCreature("A", 1, 1, 1, 1, 1);
        state.Mercenaries.Add(creature);
        state.PlaceCreature(creature, 0, 0);

        var attack = new Attack("Test", 1, 1, 1, 100,
            new AttackShape(new[] { (0, 0) }), range: 1, cooldown: 2);
        creature.StartCooldown(attack);

        var turnManager = new TurnManager(state);
        turnManager.StartEncounter(); // one tick: cooldown 2 -> 1

        Assert.True(creature.IsOnCooldown(attack));
    }

    [Fact]
    public void EndCreatureTurn_TicksCooldownAgain_OnReturnToSoleCreature()
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var creature = new TestCreature("A", 1, 1, 1, 1, 1);
        state.Mercenaries.Add(creature);
        state.PlaceCreature(creature, 0, 0);

        var attack = new Attack("Test", 1, 1, 1, 100,
            new AttackShape(new[] { (0, 0) }), range: 1, cooldown: 2);
        creature.StartCooldown(attack);

        var turnManager = new TurnManager(state);
        turnManager.StartEncounter();       // tick 1: cooldown 2 -> 1
        turnManager.EndCreatureTurn(creature); // tick 2: cooldown 1 -> 0, cleared

        Assert.False(creature.IsOnCooldown(attack));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter TurnManagerTests`
Expected: both new tests FAIL — `IsOnCooldown` still returns true after `EndCreatureTurn` because `TickCooldowns` isn't wired in yet (before this task's Step 3). `StartEncounter_TicksCooldowns_ForTheSoleCreature` also fails for the same reason: with no ticking at all, the cooldown never decrements away from `2`, but the assertion checks `IsOnCooldown` is still `true` at `1` remaining — since `IsOnCooldown` only cares about `> 0`, this specific assertion would actually pass even with zero ticking. Confirm by reading the actual test output rather than assuming: the meaningful failing test is `EndCreatureTurn_TicksCooldownAgain_OnReturnToSoleCreature`, which requires exactly 2 ticks to clear a cooldown of 2 and fails while `TickCooldowns` is never called.

- [ ] **Step 3: Add the `TickCooldowns` call in `AdvanceToNextTurn`**

In `Scripts/Encounter/TurnManager.cs`, replace:

```csharp
            state.ResetMovement(creature);
            state.ResetActionPoints(creature);
            creature.TickStatusEffects();
            break;
```

With:

```csharp
            state.ResetMovement(creature);
            state.ResetActionPoints(creature);
            creature.TickStatusEffects();
            creature.TickCooldowns();
            break;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter TurnManagerTests`
Expected: all pass (with the assertion from Step 2 adjusted to match actual behavior)

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 6: Commit**

```bash
git add Scripts/Encounter/TurnManager.cs Tests/EncounterTests/TurnManagerTests.cs
git commit -m "feat: tick creature cooldowns at the start of each of their turns"
```

---

## Task 5: Give Skewer and Quill Spray their cooldowns

**Files:**
- Modify: `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs`

**Interfaces:**
- Consumes: `Attack`'s `cooldown` constructor parameter (Task 1).

- [ ] **Step 1: Add cooldowns to Skewer and Quill Spray**

Replace the full contents of `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs`:

```csharp
using RuinGamePDT.Combat;

namespace RuinGamePDT.Creatures;

public class PricklebackGoblin : Monstrosity
{
    public PricklebackGoblin() : base("Prickleback Goblin", 6, 4, 1, 3, 3)
    {
        CombatStats.MovementPoints += 11;
        CombatStats.CritChance += 10;

        Attacks.Add(new Attack("Scratch", 1, 3, 1, 100, new AttackShape(new[] { (0, 0) }), 1));
        Attacks.Add(new Attack("Skewer", 2, 4, 1, 100, new AttackShape(new[] { (0, 0) }), 5,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 3),
            cooldown: 1));
        Attacks.Add(new Attack("Quill Spray", 0, 1, 1, 100, new AttackShape(new[] { (0, 0) }), 3,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 1),
            cooldown: 3));
    }
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs
git commit -m "feat: give Skewer a 1-round cooldown and Quill Spray a 3-round cooldown"
```

---

## Task 6: `EnemyAI` respects cooldowns and the per-turn attack cap

**Files:**
- Modify: `Scripts/Encounter/EnemyAI.cs`
- Modify: `Tests/EncounterTests/EnemyAITests.cs`

**Interfaces:**
- Consumes: `Creature.IsOnCooldown` (Task 2), `Creature.MaxAttacksPerTurn` (Task 2).

This task has two parts: (1) update `ChooseBestAttack` to skip cooldown-blocked attacks, and (2) update `TakeTurn`'s loop to stop after `MaxAttacksPerTurn` attacks instead of looping until AP runs out. Part (2) changes existing, tested behavior — several current tests in `EnemyAITests.cs` assert the old "attacks until AP exhausted" behavior and must be updated to match the new default of 1 attack per turn. This is expected and required, not a regression to avoid.

- [ ] **Step 1: Update `EnemyAITests.cs` — fix tests that assumed multi-attack-per-turn**

Read the current `Tests/EncounterTests/EnemyAITests.cs` in full before editing (it's reproduced above in this plan's context-gathering, but re-read the live file to catch any drift). Make these specific changes:

Replace the `TakeTurn_ChainsAttacksUntilAPExhausted` test (which asserts the goblin spends all 4 AP in one turn) with a test asserting the NEW capped behavior:

```csharp
    [Fact]
    public void TakeTurn_StopsAfterOneAttack_ByDefault()
    {
        // Prickleback (Agility 6) → AP = 4, MaxAttacksPerTurn defaults to 1.
        // Even with AP remaining, the goblin should stop after its one allowed attack.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100); // 200 HP, survives

        int apBefore = state.GetRemainingActionPoints(g);
        Ai().TakeTurn(g, state);

        Assert.Equal(4, apBefore);
        // Only 1 attack (AP cost 1) should have been spent, leaving 3 AP.
        Assert.Equal(3, state.GetRemainingActionPoints(g));
    }
```

Replace the `TakeTurn_PicksHighestAverageDamageAttack` test's misleading comment (it references "Bleed applied" as proof Skewer/Quill Spray fired, which still works since Scratch has no OnHit — no assertion change needed, only clarify the comment now that only 1 attack fires):

```csharp
    [Fact]
    public void TakeTurn_PicksHighestAverageDamageAttack()
    {
        // Goblin attacks: Scratch (1-3 avg 2), Skewer (2-4 avg 3), Quill Spray (0-1 avg 0.5).
        // All reach an adjacent target. Goblin should pick Skewer (highest avg) as its
        // one attack this turn. Skewer applies Bleed on hit; Scratch does not — so a
        // Bleed status effect after exactly one attack confirms Skewer was chosen.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100); // survive the hit

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;     // hit count
            if (min == 1 && max == 101) return 100; // hit, then crit (both rolls 100)
            return max - 1;                          // damage = max - 1 (top of range)
        };

        Ai(roll).TakeTurn(g, state);

        Assert.NotEmpty(m.StatusEffects);
    }
```

The `TakeTurn_SingleTarget_PicksLowestHpMerc` test's comment says "goblin will use AP on multiple attacks" — this is now false (it uses exactly 1). The test's actual assertion (`woundedMerc.CurrentHp < woundedHpBefore` after the FIRST/only attack) still holds correctly under the new 1-attack cap, since focus-fire target selection is unchanged. Update only the misleading comment:

```csharp
    [Fact]
    public void TakeTurn_SingleTarget_PicksLowestHpMerc()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var healthyMerc = PlaceMerc(state, 6, 5, stamina: 2); // lower HP but still higher than wounded
        var woundedMerc = PlaceMerc(state, 6, 6, stamina: 1);
        woundedMerc.CurrentHp = 5;

        float healthyHpBefore = healthyMerc.CurrentHp;
        float woundedHpBefore = woundedMerc.CurrentHp;

        // Single AI call — goblin makes its one allowed attack this turn, both mercs in
        // range. Focus-fire should target the wounded one.
        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return min; // minimum damage to keep wounded merc alive past first hit
        };
        Ai(roll).TakeTurn(g, state);

        Assert.True(woundedMerc.CurrentHp < woundedHpBefore, "wounded merc should have taken damage first");
    }
```

The `TakeTurn_DoesNotMove_WhenAlreadyAtBestTile` test's comment says "Use AlwaysHit so the attack loop terminates after AP exhaustion" — this is now stale (the loop terminates after 1 attack, not AP exhaustion). Update the comment only:

```csharp
    [Fact]
    public void TakeTurn_DoesNotMove_WhenAlreadyAtBestTile()
    {
        // Goblin already adjacent to merc; movement won't improve Chebyshev distance.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5);

        var startPos = state.GetPosition(g);
        Ai().TakeTurn(g, state);

        // Goblin shouldn't have moved away from the merc — Chebyshev distance
        // stays at 1 either way, but moving wouldn't strictly decrease it.
        Assert.Equal(startPos, state.GetPosition(g));
    }
```

- [ ] **Step 2: Write new failing tests for cooldown fallback and attack cap**

Add these tests to `Tests/EncounterTests/EnemyAITests.cs`:

```csharp
    [Fact]
    public void ChooseBestAttack_SkipsAttackOnCooldown_FallsBackToNextBest()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100);

        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        g.StartCooldown(skewer); // Skewer (highest avg dmg) is now unavailable

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        // Scratch (next-best) has no OnHit effect; Quill Spray does apply Bleed.
        // Since Scratch has higher avg damage (2) than Quill Spray (0.5), Scratch
        // should be chosen — no Bleed should be applied.
        Assert.Empty(m.StatusEffects);
    }

    [Fact]
    public void TakeTurn_UsingAttackWithCooldown_StartsItsCooldown()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100);

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        Assert.True(g.IsOnCooldown(skewer));
    }
```

- [ ] **Step 3: Run all `EnemyAITests` to verify current state (some fail, some pass)**

Run: `dotnet test --filter EnemyAITests`
Expected: `TakeTurn_StopsAfterOneAttack_ByDefault`, `ChooseBestAttack_SkipsAttackOnCooldown_FallsBackToNextBest`, and `TakeTurn_UsingAttackWithCooldown_StartsItsCooldown` FAIL (behavior not implemented yet); other tests pass (comment-only changes don't affect behavior)

- [ ] **Step 4: Update `ChooseBestAttack` to skip cooldown-blocked attacks**

In `Scripts/Encounter/EnemyAI.cs`, replace:

```csharp
    private (Attack attack, (int X, int Y) targetTile)? ChooseBestAttack(Creature enemy, EncounterState state)
    {
        (Attack attack, (int X, int Y) tile, float avgDmg)? best = null;

        foreach (var attack in enemy.Attacks)
        {
            if (state.GetRemainingActionPoints(enemy) < attack.ActionPointCost) continue;

            var tile = BestTargetTile(enemy, attack, state);
            if (tile == null) continue;

            float avg = (attack.MinDamage + attack.MaxDamage) / 2f;
            if (best == null || avg > best.Value.avgDmg)
                best = (attack, tile.Value, avg);
        }

        return best == null ? null : (best.Value.attack, best.Value.tile);
    }
```

With:

```csharp
    private (Attack attack, (int X, int Y) targetTile)? ChooseBestAttack(Creature enemy, EncounterState state)
    {
        (Attack attack, (int X, int Y) tile, float avgDmg)? best = null;

        foreach (var attack in enemy.Attacks)
        {
            if (enemy.IsOnCooldown(attack)) continue;
            if (state.GetRemainingActionPoints(enemy) < attack.ActionPointCost) continue;

            var tile = BestTargetTile(enemy, attack, state);
            if (tile == null) continue;

            float avg = (attack.MinDamage + attack.MaxDamage) / 2f;
            if (best == null || avg > best.Value.avgDmg)
                best = (attack, tile.Value, avg);
        }

        return best == null ? null : (best.Value.attack, best.Value.tile);
    }
```

- [ ] **Step 5: Update `TakeTurn` to stop after `MaxAttacksPerTurn` attacks**

In `Scripts/Encounter/EnemyAI.cs`, replace:

```csharp
    public List<CombatLogEntry> TakeTurn(Creature enemy, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        if (!state.IsPlaced(enemy)) return entries;
        if (state.Mercenaries.Count == 0) return entries;

        var target = FindNearestMerc(enemy, state);
        if (target == null) return entries;

        MoveToward(enemy, target, state);

        while (true)
        {
            if (!state.IsPlaced(enemy)) return entries;
            if (state.Mercenaries.Count == 0) return entries;

            var pick = ChooseBestAttack(enemy, state);
            if (pick == null) return entries;

            entries.AddRange(resolver.Resolve(enemy, pick.Value.attack, pick.Value.targetTile, state));
        }
    }
```

With:

```csharp
    public List<CombatLogEntry> TakeTurn(Creature enemy, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        if (!state.IsPlaced(enemy)) return entries;
        if (state.Mercenaries.Count == 0) return entries;

        var target = FindNearestMerc(enemy, state);
        if (target == null) return entries;

        MoveToward(enemy, target, state);

        int attacksThisTurn = 0;
        while (attacksThisTurn < enemy.MaxAttacksPerTurn)
        {
            if (!state.IsPlaced(enemy)) return entries;
            if (state.Mercenaries.Count == 0) return entries;

            var pick = ChooseBestAttack(enemy, state);
            if (pick == null) return entries;

            entries.AddRange(resolver.Resolve(enemy, pick.Value.attack, pick.Value.targetTile, state));
            attacksThisTurn++;
        }

        return entries;
    }
```

- [ ] **Step 6: Run all `EnemyAITests` to verify they pass**

Run: `dotnet test --filter EnemyAITests`
Expected: all pass

- [ ] **Step 7: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 8: Commit**

```bash
git add Scripts/Encounter/EnemyAI.cs Tests/EncounterTests/EnemyAITests.cs
git commit -m "feat: EnemyAI skips cooldown-blocked attacks and stops after MaxAttacksPerTurn"
```

---

## Self-Review Notes

- **Spec coverage:** `Attack.Cooldown` default-0 (Task 1) — `Creature.MaxAttacksPerTurn` default-1, cooldown tracking methods (Task 2) — cooldown starts uniformly via `CombatResolver.Resolve` (Task 3) — cooldown ticks per-creature-turn via `TurnManager` (Task 4) — Skewer=1, Quill Spray=3 (Task 5) — `EnemyAI` fallback past cooldown-blocked attacks and per-turn attack cap (Task 6). All spec requirements have a task.
- **Placeholder scan:** none — Task 4's tests use a single-creature encounter specifically so the tick count is deterministic (`StartEncounter()` calls `AdvanceToNextTurn()` exactly once; with one creature it's unconditionally `CurrentCreature`), avoiding the need for the `if (tm.CurrentCreature == ...)` guard other multi-creature tests in the same file require due to randomized turn-order tiebreaking.
- **Type consistency:** `Creature.IsOnCooldown(Attack)`, `StartCooldown(Attack)`, `TickCooldowns()` signatures are used identically in Task 3 (`CombatResolver`) and Task 6 (`EnemyAI`) as defined in Task 2. `MaxAttacksPerTurn` is `int` throughout.
- **Existing test updates are load-bearing, not optional:** Task 6 explicitly rewrites `TakeTurn_ChainsAttacksUntilAPExhausted` and fixes stale comments in 2 other tests — skipping this would leave the test suite red after Task 6's behavior change, which is why it's called out as "required, not a regression to avoid" in the task's own interface section.
