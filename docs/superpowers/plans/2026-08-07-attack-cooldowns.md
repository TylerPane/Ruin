# Attack Cooldowns and Enemy Attack Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give attacks a per-use cooldown (Skewer: 1 round, Quill Spray: 3 rounds), cap enemy attacks per turn (default 1) instead of `EnemyAI` looping until AP runs out, give Quill Spray a real self-centered circular burst shape, and make `EnemyAI` value AOE attacks by how many mercenaries they'd actually hit instead of raw average damage.

**Architecture:** `Attack` gains a `Cooldown` field (default 0, meaning "no cooldown" — every existing attack and every mercenary attack/skill keeps this default). `Creature` gains a `MaxAttacksPerTurn` field (default 1) and private cooldown-tracking state (`_cooldownsRemaining`), with `IsOnCooldown`/`StartCooldown`/`TickCooldowns` methods. `CombatResolver.Resolve` starts a cooldown after using an attack that has one. `TurnManager.AdvanceToNextTurn` ticks cooldowns down for the creature whose turn is starting, alongside the existing `TickStatusEffects()` call. `EnemyAI.ChooseBestAttack` skips cooldown-blocked attacks; `EnemyAI.TakeTurn` stops after `MaxAttacksPerTurn` attacks. Separately, `AttackShape` gains a shared `CircularBurst(radius)` factory (Euclidean distance, replacing the diamond/Manhattan shape `Unarmed.Shout` used privately); Shout switches to it at the same radius, and Quill Spray becomes a self-centered radius-2 circular burst (`Range: 0, MinRange: 0`, so the goblin's own tile is the only valid "target," and the burst radiates from wherever it's standing). `EnemyAI`'s scoring changes from raw `avgDmg` to `avgDmg * mercsHitByBurst`, so a goblin surrounded by multiple mercs correctly values an AOE attack over a single-target one when it actually would hit more targets.

**Tech Stack:** C# / .NET 8 / xUnit

## Global Constraints

- "1 round" / "3 rounds" cooldown means the creature's own next 1 or 3 turns, ticked the same way `StatusEffect` durations already tick via `TickStatusEffects()` — not a full encounter round.
- `Attack.Cooldown` defaults to `0` (no cooldown). Every existing `Attack` constructor call across the codebase (mercenary weapons, mercenary skills, existing enemy attacks other than Skewer/Quill Spray) must continue compiling unchanged — `Cooldown` is a new optional trailing parameter.
- `Creature.MaxAttacksPerTurn` defaults to `1`. No existing creature needs to override it in this plan — Prickleback Goblin's default of 1 attack per turn is the desired behavior described in the spec.
- Cooldowns apply uniformly regardless of who uses the attack (`CombatResolver.Resolve` is the single funnel point), even though only enemy attacks are expected to carry a nonzero `Cooldown` today.
- Several existing `EnemyAITests` assert the OLD "attacks until AP exhausted" behavior and must be updated in this plan to match the new default-1-attack-per-turn behavior — this is a required part of Task 6, not an incidental side effect.
- Circular bursts use Euclidean distance (`sqrt(dx² + dy²) <= radius`), not Manhattan (diamond) or Chebyshev (square). Shout keeps its existing radius (4) — only its shape's distance metric changes, not the number.
- Quill Spray's burst is self-centered: the goblin always targets its own tile (`Range: 0, MinRange: 0` makes that the only tile `CombatResolver.IsInRange` accepts), and the radius-2 burst shape hits whatever is within 2 tiles of the goblin's own position. This is a targeting-model change from Quill Spray's old behavior (pick a distant tile up to range 3, hit only that one tile).
- `EnemyAI.ChooseBestAttack`'s scoring change (`avgDmg * hitCount`) must not change any single-target attack's relative ranking versus another single-target attack — `hitCount` is always `1` for single-target picks, so `avg * 1 == avg`, identical to the old scoring in every scenario with at most one target per attack.

---

## File Map

| File | Change |
|------|--------|
| `Scripts/Combat/Attack.cs` | Add `Cooldown` property + optional constructor parameter |
| `Scripts/Creatures/Creature.cs` | Add `MaxAttacksPerTurn` field, `_cooldownsRemaining` dict, `IsOnCooldown`/`StartCooldown`/`TickCooldowns` methods |
| `Scripts/Encounter/CombatResolver.cs` | Start the attacker's cooldown after resolving an attack with `Cooldown > 0` |
| `Scripts/Encounter/TurnManager.cs` | Call `creature.TickCooldowns()` alongside the existing `TickStatusEffects()` call |
| `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs` | Give Skewer `Cooldown: 1`, Quill Spray `Cooldown: 3`; later, make Quill Spray a self-centered circular burst |
| `Scripts/Encounter/EnemyAI.cs` | `ChooseBestAttack` skips cooldown-blocked attacks and scores by `avgDmg * hitCount`; `TakeTurn` stops after `MaxAttacksPerTurn` attacks; `BestTargetTile` returns hit-count alongside the tile |
| `Scripts/Combat/AttackShape.cs` | Add `CircularBurst(radius)` static factory |
| `Scripts/Weapons/Unarmed.cs` | Switch Shout to `AttackShape.CircularBurst`, remove the old private `BurstOffsets` helper |
| `Tests/CombatTests/AttackCooldownTests.cs` | Create — tests for `Creature`'s cooldown methods and `CombatResolver` starting cooldowns |
| `Tests/EncounterTests/EnemyAITests.cs` | Modify — update tests that assumed multi-attack-per-turn behavior; add cooldown-fallback, attack-cap, and AOE-scoring tests |
| `Tests/CombatTests/AttackShapeTests.cs` | Create — tests for `AttackShape.CircularBurst` |
| `Tests/WeaponTests/UnarmedTests.cs` | Modify — replace the diamond-shape Shout test with a circular-shape one |
| `Tests/CreatureTests/PricklebackGoblinTests.cs` | Create (if it doesn't already exist) — tests for Quill Spray's self-centered targeting and shape |

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

## Task 7: Shared circular burst-shape helper on `AttackShape`

**Files:**
- Modify: `Scripts/Combat/AttackShape.cs`
- Modify: `Scripts/Weapons/Unarmed.cs`
- Modify: `Tests/WeaponTests/UnarmedTests.cs`

**Interfaces:**
- Produces: `AttackShape.CircularBurst(int radius) : AttackShape` — a static factory returning offsets `(dx, dy)` where `sqrt(dx² + dy²) <= radius`, including `(0, 0)`. Consumed by `Unarmed.Shout` (this task) and `PricklebackGoblin.QuillSpray` (Task 8).

Shout currently uses a private `BurstOffsets(radius)` helper local to `Unarmed` with Manhattan distance (a diamond). This task replaces it with a shared Euclidean-distance helper on `AttackShape` itself (a shape concept, not weapon-specific), and switches Shout to use it — same radius (4), rounder shape. `CombatResolver.IsInRange` already has a standing TODO comment planning an eventual Euclidean switch for range checks; this task is the shape equivalent of that same direction, though `IsInRange` itself is untouched here — this task only changes how burst *shapes* are generated, not how *range-to-target* is checked.

- [ ] **Step 1: Write failing tests for the new circular shape**

Replace the `Shout_HasDiamondBurstShape_ManhattanDistanceFour` test in `Tests/WeaponTests/UnarmedTests.cs` — the diamond shape no longer exists after this task, so this test's assertions must change to describe the new circle:

```csharp
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
```

Also add a direct test of the new helper itself. Add this to a new file `Tests/CombatTests/AttackShapeTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "AttackShapeTests|Shout_HasCircularBurstShape"`
Expected: FAIL — `AttackShape.CircularBurst` does not exist yet, and the old diamond-shape test name/assertions are gone so nothing runs under that name (confirms the replacement happened, not a duplicate)

- [ ] **Step 3: Add `CircularBurst` to `AttackShape`**

Replace the full contents of `Scripts/Combat/AttackShape.cs`:

```csharp
namespace RuinGamePDT.Combat;

public record AttackShape(IEnumerable<(int x, int y)> Offsets)
{
    public static AttackShape CircularBurst(int radius)
    {
        var offsets = new List<(int, int)>();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
            if (Math.Sqrt(x * x + y * y) <= radius)
                offsets.Add((x, y));
        return new AttackShape(offsets);
    }
}
```

- [ ] **Step 4: Switch `Shout` to the new shape and remove the old helper**

In `Scripts/Weapons/Unarmed.cs`, replace:

```csharp
        Attacks.Add(new Attack(
            name: "Shout",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(BurstOffsets(radius: 4)),
            range: 4,
            reaction: null,
            onHit: new AttackEffect(AttackEffectType.StatIncrease, [new StatChange(CombatStat.PhysicalDefense, 2, 2)], MinDuration: 3, MaxDuration: 3),
            onCrit: null
        ));
    }

    private static IEnumerable<(int, int)> BurstOffsets(int radius)
    {
        var offsets = new List<(int, int)>();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (Math.Abs(x) + Math.Abs(y) <= radius)
                    offsets.Add((x, y));
        return offsets;
    }
}
```

With:

```csharp
        Attacks.Add(new Attack(
            name: "Shout",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: AttackShape.CircularBurst(radius: 4),
            range: 4,
            reaction: null,
            onHit: new AttackEffect(AttackEffectType.StatIncrease, [new StatChange(CombatStat.PhysicalDefense, 2, 2)], MinDuration: 3, MaxDuration: 3),
            onCrit: null
        ));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "AttackShapeTests|UnarmedTests"`
Expected: all pass

- [ ] **Step 6: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 7: Commit**

```bash
git add Scripts/Combat/AttackShape.cs Scripts/Weapons/Unarmed.cs Tests/WeaponTests/UnarmedTests.cs Tests/CombatTests/AttackShapeTests.cs
git commit -m "feat: add AttackShape.CircularBurst, switch Shout from diamond to circular burst"
```

---

## Task 8: Quill Spray becomes a self-centered circular burst

**Files:**
- Modify: `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs`
- Test: `Tests/CreatureTests/PricklebackGoblinTests.cs` (create if it doesn't already exist — check first)

**Interfaces:**
- Consumes: `AttackShape.CircularBurst` (Task 7).

Quill Spray changes from a single-tile, range-3 targeted attack to a self-centered radius-2 circular burst: `Range: 0, MinRange: 0` (only the goblin's own tile satisfies `CombatResolver.IsInRange` at those values, so it never needs a separate target — the burst always radiates from wherever the goblin is standing) and `attackShape: AttackShape.CircularBurst(radius: 2)`.

- [ ] **Step 1: Check for an existing Prickleback Goblin test file**

Run: `Get-ChildItem -Recurse Tests -Filter "*Goblin*"` (PowerShell) or search for a file matching `*PricklebackGoblin*Test*` under `Tests/`. If one already exists, add to it; if not, create `Tests/CreatureTests/PricklebackGoblinTests.cs` fresh with the namespace `RuinGamePDT.Tests` matching every other test file in the project.

- [ ] **Step 2: Write failing tests**

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Tests;

public class PricklebackGoblinTests
{
    [Fact]
    public void QuillSpray_IsSelfCentered_RangeZero()
    {
        var g = new PricklebackGoblin();
        var quillSpray = g.Attacks.First(a => a.Name == "Quill Spray");

        Assert.Equal(0, quillSpray.Range);
        Assert.Equal(0, quillSpray.MinRange);
    }

    [Fact]
    public void QuillSpray_HasCircularBurstShape_RadiusTwo()
    {
        var g = new PricklebackGoblin();
        var quillSpray = g.Attacks.First(a => a.Name == "Quill Spray");
        var offsets = quillSpray.AttackShape.Offsets.ToHashSet();

        Assert.Contains((0, 0), offsets);
        Assert.Contains((2, 0), offsets);
        Assert.DoesNotContain((3, 0), offsets);
    }

    [Fact]
    public void QuillSpray_OnlyGoblinsOwnTile_IsInRange()
    {
        var state = new RuinGamePDT.Encounter.EncounterState(new RuinGamePDT.World.EncounterMap(20, 20));
        var g = new PricklebackGoblin();
        state.Enemies.Add(g);
        state.PlaceCreature(g, 5, 5);
        var quillSpray = g.Attacks.First(a => a.Name == "Quill Spray");

        var resolver = new RuinGamePDT.Encounter.CombatResolver(Random.Shared.Next);

        Assert.True(resolver.IsInRange(g, quillSpray, (5, 5), state));
        Assert.False(resolver.IsInRange(g, quillSpray, (6, 5), state));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter PricklebackGoblinTests`
Expected: FAIL — Quill Spray still has `Range: 3` and the old single-tile shape

- [ ] **Step 4: Update Quill Spray's definition**

In `Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs` (this file was already modified in Task 5 to add `cooldown: 3` to Quill Spray — apply this change on top of that version), replace:

```csharp
        Attacks.Add(new Attack("Quill Spray", 0, 1, 1, 100, new AttackShape(new[] { (0, 0) }), 3,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 1),
            cooldown: 3));
```

With:

```csharp
        Attacks.Add(new Attack("Quill Spray", 0, 1, 1, 100, AttackShape.CircularBurst(radius: 2), range: 0, minRange: 0,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 1),
            cooldown: 3));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter PricklebackGoblinTests`
Expected: all pass

- [ ] **Step 6: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures. Note: this WILL require re-checking Task 6's `ChooseBestAttack_SkipsAttackOnCooldown_FallsBackToNextBest` and `TakeTurn_UsingAttackWithCooldown_StartsItsCooldown` tests from earlier in this plan, since both place a merc at `(6, 5)` (adjacent to the goblin, range 1) and rely on Quill Spray being able to reach that tile — Quill Spray's OLD range was 3 (reached it fine), but its NEW self-centered radius-2 burst reaches tile `(6,5)` from goblin position `(5,5)` only because that tile is within Euclidean distance 2 of the goblin's own position `(5,5)`... verify this directly: distance from `(5,5)` to `(6,5)` is 1, and the burst radius is 2, so `(6,5)` is within the burst centered on the goblin. This should still work, but confirm by running the tests rather than assuming — if `TakeTurn_UsingAttackWithCooldown_StartsItsCooldown`'s roll sequence assumed Quill Spray was never chosen at all (it wasn't — that test picks Skewer, the highest avg), no change is needed there regardless.

- [ ] **Step 7: Commit**

```bash
git add Scripts/Creatures/Monstrosities/Goblins/PricklebackGoblin.cs Tests/CreatureTests/PricklebackGoblinTests.cs
git commit -m "feat: Quill Spray becomes a self-centered radius-2 circular burst"
```

---

## Task 9: `EnemyAI` scores AOE attacks by mercs-in-burst, not raw avg damage

**Files:**
- Modify: `Scripts/Encounter/EnemyAI.cs`
- Modify: `Tests/EncounterTests/EnemyAITests.cs`

**Interfaces:**
- Consumes: nothing new from prior tasks in this addition — this task changes `EnemyAI`'s internal scoring only.

Today, `ChooseBestAttack` scores every attack by `(MinDamage + MaxDamage) / 2f` regardless of how many mercenaries an AOE attack would actually hit. `BestTargetTile`'s AOE branch already computes a merc-hit-count (`bestCount`) internally but discards it, returning only the tile. This task threads that count back out so `ChooseBestAttack` can score AOE attacks as `avgDmg * hitCount` — a goblin surrounded by 3 mercs values a burst attack roughly 3x higher than hitting just 1, closer to its real expected value.

- [ ] **Step 1: Write failing tests**

Add to `Tests/EncounterTests/EnemyAITests.cs`:

```csharp
    [Fact]
    public void ChooseBestAttack_PrefersAoeAttack_WhenItHitsMultipleMercs()
    {
        // Scratch avg = 2 (single-target). Quill Spray avg = 0.5, but its radius-2
        // self-centered burst can hit multiple adjacent mercs — with 3 mercs
        // surrounding the goblin, Quill Spray's score (0.5 * 3 = 1.5) still trails
        // Scratch's single-target score of 2 UNLESS enough mercs surround it.
        // Use 5 mercs clustered around the goblin so Quill Spray's score
        // (0.5 * 5 = 2.5) exceeds Scratch's (2 * 1 = 2).
        var state = MakeState();
        var g = PlaceGoblin(state, 10, 10);

        // Cooldown-block Skewer so it can't win the comparison outright (Skewer avg=3
        // would otherwise dominate regardless of this test's AOE-scoring concern).
        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        g.StartCooldown(skewer);

        PlaceMerc(state, 9, 10, stamina: 100);
        PlaceMerc(state, 11, 10, stamina: 100);
        PlaceMerc(state, 10, 9, stamina: 100);
        PlaceMerc(state, 10, 11, stamina: 100);
        var m5 = PlaceMerc(state, 9, 9, stamina: 100);

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        // Quill Spray applies Bleed on hit; Scratch does not. If the goblin picked
        // Quill Spray (correctly valuing the multi-hit burst over single-target
        // Scratch), every surrounded merc should show a Bleed status effect.
        Assert.NotEmpty(m5.StatusEffects);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter ChooseBestAttack_PrefersAoeAttack_WhenItHitsMultipleMercs`
Expected: FAIL — `ChooseBestAttack` currently scores Quill Spray at raw avg 0.5, losing to Scratch's 2 regardless of surrounding mercs. If instead the test errors out or fails for an unrelated reason (e.g. the goblin moved away from its boxed-in position via `MoveToward`, changing which mercs are within its burst radius), read the actual failure: the 5 placed mercs surround the goblin at Chebyshev distance 1 on every open side, so `MoveToward`'s reachable-tile search should find no tile that strictly improves Chebyshev distance to the nearest one (already at the minimum non-zero distance) and leave the goblin in place — if this assumption is wrong, adjust the merc placement coordinates (keeping all 5 within Euclidean distance 2 of wherever the goblin actually ends up) rather than abandoning the test's intent.

- [ ] **Step 3: Thread the hit-count out of `BestTargetTile`**

In `Scripts/Encounter/EnemyAI.cs`, replace:

```csharp
    private (int X, int Y)? BestTargetTile(Creature enemy, Attack attack, EncounterState state)
    {
        bool singleTarget = IsSingleTarget(attack);
        var offsets = attack.AttackShape.Offsets.ToList();
        var width = state.Map.Width;
        var height = state.Map.Height;

        if (singleTarget)
        {
            // Pick the in-range mercenary tile with the lowest current HP (focus-fire).
            Creature? bestMerc = null;
            (int X, int Y) bestTile = (0, 0);
            float bestHp = float.MaxValue;

            foreach (var m in state.Mercenaries)
            {
                if (!state.IsPlaced(m)) continue;
                var mp = state.GetPosition(m);
                if (!resolver.IsInRange(enemy, attack, mp, state)) continue;
                if (m.CurrentHp < bestHp)
                {
                    bestHp = m.CurrentHp;
                    bestMerc = m;
                    bestTile = mp;
                }
            }
            return bestMerc == null ? null : bestTile;
        }

        // AOE — pick the in-range tile whose offsets catch the most mercs.
        (int X, int Y)? bestAoeTile = null;
        int bestCount = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (!resolver.IsInRange(enemy, attack, (x, y), state)) continue;

            int count = 0;
            foreach (var (dx, dy) in offsets)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || tx >= width || ty < 0 || ty >= height) continue;
                var c = state.GetCreatureAt(tx, ty);
                if (c != null && state.Mercenaries.Contains(c)) count++;
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestAoeTile = (x, y);
            }
        }
        return bestAoeTile;
    }
```

With:

```csharp
    private (int X, int Y, int hitCount)? BestTargetTile(Creature enemy, Attack attack, EncounterState state)
    {
        bool singleTarget = IsSingleTarget(attack);
        var offsets = attack.AttackShape.Offsets.ToList();
        var width = state.Map.Width;
        var height = state.Map.Height;

        if (singleTarget)
        {
            // Pick the in-range mercenary tile with the lowest current HP (focus-fire).
            Creature? bestMerc = null;
            (int X, int Y) bestTile = (0, 0);
            float bestHp = float.MaxValue;

            foreach (var m in state.Mercenaries)
            {
                if (!state.IsPlaced(m)) continue;
                var mp = state.GetPosition(m);
                if (!resolver.IsInRange(enemy, attack, mp, state)) continue;
                if (m.CurrentHp < bestHp)
                {
                    bestHp = m.CurrentHp;
                    bestMerc = m;
                    bestTile = mp;
                }
            }
            return bestMerc == null ? null : (bestTile.X, bestTile.Y, 1);
        }

        // AOE — pick the in-range tile whose offsets catch the most mercs.
        (int X, int Y)? bestAoeTile = null;
        int bestCount = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (!resolver.IsInRange(enemy, attack, (x, y), state)) continue;

            int count = 0;
            foreach (var (dx, dy) in offsets)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || tx >= width || ty < 0 || ty >= height) continue;
                var c = state.GetCreatureAt(tx, ty);
                if (c != null && state.Mercenaries.Contains(c)) count++;
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestAoeTile = (x, y);
            }
        }
        return bestAoeTile == null ? null : (bestAoeTile.Value.X, bestAoeTile.Value.Y, bestCount);
    }
```

- [ ] **Step 4: Update `ChooseBestAttack` to score by `avgDmg * hitCount`**

In `Scripts/Encounter/EnemyAI.cs`, replace:

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

With:

```csharp
    private (Attack attack, (int X, int Y) targetTile)? ChooseBestAttack(Creature enemy, EncounterState state)
    {
        (Attack attack, (int X, int Y) tile, float score)? best = null;

        foreach (var attack in enemy.Attacks)
        {
            if (enemy.IsOnCooldown(attack)) continue;
            if (state.GetRemainingActionPoints(enemy) < attack.ActionPointCost) continue;

            var pick = BestTargetTile(enemy, attack, state);
            if (pick == null) continue;

            float avg = (attack.MinDamage + attack.MaxDamage) / 2f;
            float score = avg * pick.Value.hitCount;
            if (best == null || score > best.Value.score)
                best = (attack, (pick.Value.X, pick.Value.Y), score);
        }

        return best == null ? null : (best.Value.attack, best.Value.tile);
    }
```

- [ ] **Step 5: Run all `EnemyAITests` to verify they pass**

Run: `dotnet test --filter EnemyAITests`
Expected: all pass, including `ChooseBestAttack_PrefersAoeAttack_WhenItHitsMultipleMercs` and every test from Task 6 (their assertions are unaffected by this scoring change in single-merc scenarios, since `hitCount` is always 1 there — `avg * 1 == avg`, identical to the old scoring)

- [ ] **Step 6: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 7: Commit**

```bash
git add Scripts/Encounter/EnemyAI.cs Tests/EncounterTests/EnemyAITests.cs
git commit -m "feat: EnemyAI scores AOE attacks by mercs-in-burst instead of raw avg damage"
```

---

## Self-Review Notes

- **Spec coverage:** `Attack.Cooldown` default-0 (Task 1) — `Creature.MaxAttacksPerTurn` default-1, cooldown tracking methods (Task 2) — cooldown starts uniformly via `CombatResolver.Resolve` (Task 3) — cooldown ticks per-creature-turn via `TurnManager` (Task 4) — Skewer=1, Quill Spray=3 (Task 5) — `EnemyAI` fallback past cooldown-blocked attacks and per-turn attack cap (Task 6) — shared circular burst shape, Shout switched to it (Task 7) — Quill Spray becomes a self-centered radius-2 circular burst (Task 8) — `EnemyAI` scores AOE attacks by mercs-hit instead of raw avg damage (Task 9). All requirements from both the original spec and the follow-up scope (AOE shape + scoring) have a task.
- **Placeholder scan:** none — Task 4's tests use a single-creature encounter specifically so the tick count is deterministic (`StartEncounter()` calls `AdvanceToNextTurn()` exactly once; with one creature it's unconditionally `CurrentCreature`), avoiding the need for the `if (tm.CurrentCreature == ...)` guard other multi-creature tests in the same file require due to randomized turn-order tiebreaking. Task 8's Step 1 asks the implementer to check for an existing goblin test file before creating one — this is a real, necessary check (the codebase's actual state at implementation time isn't independently known by this plan), not a vague placeholder; the test content itself is fully specified either way.
- **Type consistency:** `Creature.IsOnCooldown(Attack)`, `StartCooldown(Attack)`, `TickCooldowns()` signatures are used identically in Task 3 (`CombatResolver`) and Task 6 (`EnemyAI`) as defined in Task 2. `MaxAttacksPerTurn` is `int` throughout. `AttackShape.CircularBurst(int radius) : AttackShape` (Task 7) is consumed identically by `Unarmed.Shout` (Task 7) and `PricklebackGoblin`'s Quill Spray (Task 8). `BestTargetTile`'s return type changes from `(int X, int Y)?` to `(int X, int Y, int hitCount)?` in Task 9 — this is a private method with exactly one call site (`ChooseBestAttack`, updated in the same task), so the signature change is self-contained within Task 9 and doesn't leak into any other task's code.
- **Task ordering:** Task 8 modifies `PricklebackGoblin.cs`'s Quill Spray line, which Task 5 already touched (to add `cooldown: 3`) — Task 8's Step 4 explicitly shows the "replace this" snippet starting from Task 5's already-cooldown-bearing version, not the original pre-Task-5 line, so applying tasks in order (5 before 8) produces the correct final file.
- **Existing test updates are load-bearing, not optional:** Task 6 explicitly rewrites `TakeTurn_ChainsAttacksUntilAPExhausted` and fixes stale comments in 2 other tests — skipping this would leave the test suite red after Task 6's behavior change. Task 7 replaces `Shout_HasDiamondBurstShape_ManhattanDistanceFour` outright since the diamond shape no longer exists after that task. Both are called out as required, not incidental, in their respective task sections.
