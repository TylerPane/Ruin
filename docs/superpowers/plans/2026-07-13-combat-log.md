# Combat Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a scrollable combat log in a left-side panel during encounters, recording every attack/skill use: attacker, action name, target, hit/miss/heal, damage, and any status effects applied.

**Architecture:** `CombatResolver.Resolve()` changes from `void` to returning `List<CombatLogEntry>` (one entry per affected target, so AOE produces multiple entries). `EncounterScene` also builds log entries for the two paths that bypass the resolver entirely (Rush self-cast, First Aid direct-heal). A new `CombatLog` class owns the line history, scroll offset, and rendering. Text rendering needs a MonoGame Content Pipeline (MGCB) + SpriteFont, which the project does not have yet — icons currently load via raw `Texture2D.FromStream`, bypassing the pipeline.

**Tech Stack:** C# / .NET 8 / MonoGame / xUnit

## Global Constraints

- Viewport is 1280x900 (`Game1.cs:27-28`). Log panel must not overlap the existing hotbar (y=830) or AP pips (y=810).
- `CombatResolver.Resolve()` signature change affects two callers: `EncounterScene.cs:107,179` and `EnemyAI.cs:26`. Both must be updated in the same task as the signature change to keep the build green.
- Log line format (from spec):
  - Hit: `"{Attacker} uses {Attack} on {Target} — HIT, {Damage} dmg"`
  - Miss: `"{Attacker} uses {Attack} on {Target} — MISS"`
  - Heal (Damage < 0): `"{Attacker} uses {Attack} on {Target} — HEAL, {-Damage} hp"`
  - Effect (own line, one per effect): `"{Target}: {Effect} applied"`

---

## File Map

| File | Change |
|------|--------|
| `Content/Content.mgcb` | Create — MGCB project file referencing the new font |
| `Content/Fonts/CombatLogFont.spritefont` | Create — SpriteFont description |
| `RuinGame.csproj` | Modify — invoke MGCB build for `Content.mgcb`, keep existing raw `ContentCopy` for icons |
| `Scripts/Combat/CombatLogEntry.cs` | Create — the log entry record |
| `Scripts/Encounter/CombatResolver.cs` | Modify — `Resolve()` returns `List<CombatLogEntry>` |
| `Scripts/Encounter/EnemyAI.cs` | Modify — consume (or explicitly discard) the new return value |
| `Scripts/Rendering/CombatLog.cs` | Create — line history, scroll state, `AddEntries`, `HandleScroll`, `Draw` |
| `Scripts/Rendering/EncounterScene.cs` | Modify — own a `CombatLog`, feed it from all three action paths (basic attacks, Defensive Stance/First-Aid-via-resolver, Rush self-cast, direct-heal), wire mouse-wheel scroll, draw panel, shift terrain draw origin right |
| `Game1.cs` | Modify — load `SpriteFont`, pass to `EncounterScene` |
| `Tests/EncounterTests/CombatResolverTests.cs` | Modify — assert `Resolve()` return value on existing tests; add new entry-content tests |
| `Tests/RenderingTests/CombatLogTests.cs` | Create — unit tests for `CombatLog` line formatting, scroll clamping |

---

## Task 1: `CombatLogEntry` record

**Files:**
- Create: `Scripts/Combat/CombatLogEntry.cs`

**Interfaces:**
- Produces: `CombatLogEntry` record with properties `AttackerName` (string), `AttackName` (string), `TargetName` (string), `WasHit` (bool), `Damage` (int), `EffectsApplied` (`IReadOnlyList<string>`). Consumed by `CombatResolver`, `CombatLog`, and `EncounterScene`.

- [ ] **Step 1: Create the file**

```csharp
namespace RuinGamePDT.Combat;

public record CombatLogEntry(
    string AttackerName,
    string AttackName,
    string TargetName,
    bool WasHit,
    int Damage,
    IReadOnlyList<string> EffectsApplied
);
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add Scripts/Combat/CombatLogEntry.cs
git commit -m "feat: add CombatLogEntry record"
```

---

## Task 2: `CombatResolver.Resolve()` returns log entries

**Files:**
- Modify: `Scripts/Encounter/CombatResolver.cs`
- Modify: `Tests/EncounterTests/CombatResolverTests.cs`

**Interfaces:**
- Consumes: `CombatLogEntry` from Task 1.
- Produces: `CombatResolver.Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state) : List<CombatLogEntry>`. Consumed by `EncounterScene` (Task 5) and `EnemyAI` (Task 3).

Effect name formatting: for each `StatChange` in an applied `AttackEffect.Stats`, produce a string via the stat's enum name and the *type* of change (`StatIncrease` → `"{Stat} increased"`, others → `AttackEffectType` name, e.g. `"Bleed"`). Concretely: if `effect.Type` is `StatIncrease` or `StatReduction`, format as `"{statChange.Stat} {sign}"` where sign is `"+"` for increase / `"-"` for reduction (e.g. `"Evasion +"`); otherwise format as the effect type's name alone (e.g. `"Bleed"`). Since the actual rolled amount varies, use the effect type name for damage-effects (Bleed/Burn/Poison/Chill/Static/Stun) and stat name + direction for buffs — this matches what `Creature.ApplyStatusEffect` produces per `StatChange`.

- [ ] **Step 1: Write failing tests for the new return value**

Add to `Tests/EncounterTests/CombatResolverTests.cs` (near the other `OnHit`/`OnCrit` tests):

```csharp
[Fact]
public void Resolve_OnHit_ReturnsEntryWithDamageAndHitFlag()
{
    var (state, attacker, defender) = MakeFight(attackerStrength: 1, defenderStamina: 0);
    var attack = BasicAttack(minDmg: 5, maxDmg: 5, accuracy: 80);

    var entries = new CombatResolver(Rolls(1, 20, 0, 5)).Resolve(attacker, attack, (1, 0), state);

    Assert.Single(entries);
    Assert.Equal("A", entries[0].AttackerName);
    Assert.Equal("Test", entries[0].AttackName);
    Assert.Equal("D", entries[0].TargetName);
    Assert.True(entries[0].WasHit);
    Assert.True(entries[0].Damage > 0);
    Assert.Empty(entries[0].EffectsApplied);
}

[Fact]
public void Resolve_Miss_ReturnsEntryWithWasHitFalse()
{
    var (state, attacker, defender) = MakeFight();
    var attack = BasicAttack(accuracy: 80);

    var entries = new CombatResolver(Rolls(1, 19)).Resolve(attacker, attack, (1, 0), state);

    Assert.Single(entries);
    Assert.False(entries[0].WasHit);
    Assert.Equal(0, entries[0].Damage);
}

[Fact]
public void Resolve_AoE_ReturnsOneEntryPerTargetHit()
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

    var entries = new CombatResolver(Rolls(1, 100, 0, 5, 100, 0, 5)).Resolve(attacker, attack, (5, 5), state);

    Assert.Equal(2, entries.Count);
    Assert.Contains(entries, e => e.TargetName == "D1");
    Assert.Contains(entries, e => e.TargetName == "D2");
}

[Fact]
public void Resolve_OnHitEffect_IncludesEffectDescriptionInEntry()
{
    var (state, attacker, defender) = MakeFight();
    var onHit = new AttackEffect(AttackEffectType.Bleed,
        new[] { new StatChange(CombatStat.HitPoints, 2, 2) },
        MinDuration: 1, MaxDuration: 1);
    var attack = BasicAttack(minDmg: 1, maxDmg: 1, accuracy: 100, onHit: onHit);

    var entries = new CombatResolver(Rolls(1, 100, 0, 1)).Resolve(attacker, attack, (1, 0), state);

    Assert.Single(entries);
    Assert.Single(entries[0].EffectsApplied);
    Assert.Equal("Bleed", entries[0].EffectsApplied[0]);
}

[Fact]
public void Resolve_NegativeDamage_EntryReportsNegativeDamageAsHeal()
{
    var (state, attacker, defender) = MakeFight(defenderStamina: 5);
    defender.CurrentHp = 10;
    var attack = BasicAttack(minDmg: -3, maxDmg: -3, accuracy: 100);

    var entries = new CombatResolver(Rolls(1, 100, 0, -3)).Resolve(attacker, attack, (1, 0), state);

    Assert.Single(entries);
    Assert.True(entries[0].Damage < 0);
}
```

Also update every existing call site in this file from:
```csharp
new CombatResolver(Rolls(...)).Resolve(attacker, attack, (1, 0), state);
```
No change needed to existing calls that don't inspect the return value — `Resolve` returning a value instead of `void` does not break statement-expression calls. Leave those as-is.

- [ ] **Step 2: Run tests to verify the new ones fail (compile error — Resolve still returns void)**

Run: `dotnet test --filter CombatResolverTests`
Expected: build error, `Resolve` does not return a value that can be assigned

- [ ] **Step 3: Implement — change `Resolve` to build and return entries**

Replace `Scripts/Encounter/CombatResolver.cs` contents:

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class CombatResolver(Func<int, int, int> roll)
{
    public List<CombatLogEntry> Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        state.SpendActionPoints(attacker, attack.ActionPointCost);
        int hitCount = roll(attack.MinHits, attack.MaxHits + 1);

        foreach (var (dx, dy) in attack.AttackShape.Offsets)
        {
            int tx = targetTile.X + dx;
            int ty = targetTile.Y + dy;
            var defender = state.GetCreatureAt(tx, ty);
            if (defender == null) continue;

            for (int i = 0; i < hitCount; i++)
            {
                int hitThreshold = 100 - attack.Accuracy + (int)defender.CombatStats.Evasion;
                int hitRoll = roll(1, 101);
                if (hitRoll < hitThreshold)
                {
                    entries.Add(new CombatLogEntry(attacker.Name, attack.Name, defender.Name, WasHit: false, Damage: 0, EffectsApplied: Array.Empty<string>()));
                    continue;
                }

                bool isCrit = attack.AutoCrit;
                if (!isCrit)
                {
                    int critRoll = roll(1, 101);
                    isCrit = critRoll >= 100 - (int)attacker.CombatStats.CritChance;
                }

                int baseDmg = roll(attack.MinDamage, attack.MaxDamage + 1);
                int dmg = baseDmg + (int)attacker.CombatStats.AttackPower - (int)defender.CombatStats.PhysicalDefense;
                if (attack.MaxDamage > 0)       dmg = Math.Max(1, dmg);
                else if (attack.MaxDamage == 0) dmg = 0;
                // MaxDamage < 0: negative dmg is a heal — CurrentHp clamp handles the cap
                if (isCrit) dmg *= 2;

                defender.CurrentHp -= dmg;

                var effects = new List<string>();
                if (attack.OnHit != null)
                {
                    defender.ApplyStatusEffect(attack.OnHit);
                    effects.AddRange(DescribeEffect(attack.OnHit));
                }
                if (isCrit && attack.OnCrit != null)
                {
                    defender.ApplyStatusEffect(attack.OnCrit);
                    effects.AddRange(DescribeEffect(attack.OnCrit));
                }

                entries.Add(new CombatLogEntry(attacker.Name, attack.Name, defender.Name, WasHit: true, Damage: dmg, EffectsApplied: effects));

                if (defender.CurrentHp <= 0)
                {
                    state.RemoveCreature(defender);
                    break;
                }
            }
        }

        return entries;
    }

    private static IEnumerable<string> DescribeEffect(AttackEffect effect)
    {
        foreach (var statChange in effect.Stats)
        {
            yield return effect.Type switch
            {
                AttackEffectType.StatIncrease => $"{statChange.Stat} +",
                AttackEffectType.StatReduction => $"{statChange.Stat} -",
                _ => effect.Type.ToString()
            };
        }
    }

    // TODO(distance): switch to Euclidean (sqrt(dx² + dy²)) for circular range
    // shape. Currently square shape — matches the v1 spec's
    // explicit placeholder; replace alongside the EncounterScene targeting
    // overlay's distance calculation.
    public bool IsInRange(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        if (!state.IsPlaced(attacker)) return false;
        var pos = state.GetPosition(attacker);
        int distance = Math.Max(Math.Abs(targetTile.X - pos.X), Math.Abs(targetTile.Y - pos.Y));
        return distance >= attack.MinRange && distance <= attack.Range;
    }
}
```

- [ ] **Step 4: Run tests to verify all pass**

Run: `dotnet test --filter CombatResolverTests`
Expected: all tests pass, including the 5 new ones

- [ ] **Step 5: Commit**

```bash
git add Scripts/Encounter/CombatResolver.cs Tests/EncounterTests/CombatResolverTests.cs
git commit -m "feat: CombatResolver.Resolve returns per-target log entries"
```

---

## Task 3: `EnemyAI` consumes the new return value

**Files:**
- Modify: `Scripts/Encounter/EnemyAI.cs`

**Interfaces:**
- Consumes: `CombatResolver.Resolve(...) : List<CombatLogEntry>` from Task 2.
- Produces: `EnemyAI.TakeTurn(Creature enemy, EncounterState state) : List<CombatLogEntry>` — accumulated entries from every `Resolve` call in the turn. Consumed by `Game1.cs` (Task 6).

`TakeTurn` currently returns `void` and loops calling `resolver.Resolve(...)` until no attack is available. Change it to accumulate and return all entries produced during the turn, so the caller can feed them into the combat log.

- [ ] **Step 1: Change `TakeTurn` signature and accumulate entries**

In `Scripts/Encounter/EnemyAI.cs`, replace:

```csharp
public void TakeTurn(Creature enemy, EncounterState state)
{
    if (!state.IsPlaced(enemy)) return;
    if (state.Mercenaries.Count == 0) return;

    var target = FindNearestMerc(enemy, state);
    if (target == null) return;

    MoveToward(enemy, target, state);

    while (true)
    {
        if (!state.IsPlaced(enemy)) return;
        if (state.Mercenaries.Count == 0) return;

        var pick = ChooseBestAttack(enemy, state);
        if (pick == null) return;

        resolver.Resolve(enemy, pick.Value.attack, pick.Value.targetTile, state);
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

Add `using RuinGamePDT.Combat;` to the top of the file if not already present (it already imports `RuinGamePDT.Combat` at line 1 — no change needed there).

- [ ] **Step 2: Update existing `EnemyAITests` call sites**

Run: `dotnet build`

If `Tests/EncounterTests/EnemyAITests.cs` calls `_ai.TakeTurn(...)` as a bare statement, it still compiles unchanged (return value ignored). Confirm with a build; no test file edits are expected. If the build reports errors in `EnemyAITests.cs`, read the failing lines and adjust only the lines the compiler flags — do not change assertions unrelated to the signature.

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 4: Commit**

```bash
git add Scripts/Encounter/EnemyAI.cs
git commit -m "feat: EnemyAI.TakeTurn returns accumulated combat log entries"
```

---

## Task 4: Content Pipeline + SpriteFont

**Files:**
- Create: `Content/Content.mgcb`
- Create: `Content/Fonts/CombatLogFont.spritefont`
- Modify: `RuinGame.csproj`

**Interfaces:**
- Produces: a built `CombatLogFont.xnb` in the output `Content/Fonts/` directory, loadable via `Content.Load<SpriteFont>("Fonts/CombatLogFont")` in `Game1.LoadContent` (Task 6).

This project has no MGCB pipeline — install the MonoGame content build tooling and wire it into the csproj.

- [ ] **Step 1: Install the MGCB tool**

Run: `dotnet tool install -g dotnet-mgcb`
Expected: tool installs (or reports already installed)

- [ ] **Step 2: Create `Content/Content.mgcb`**

```
#----------------------------- Global Properties ----------------------------#

/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:
/profile:Reach
/compress:False

#-------------------------------- References ---------------------------------#

#---------------------------------- Content -----------------------------------#

#begin Fonts/CombatLogFont.spritefont
/importer:FontDescriptionImporter
/processor:FontDescriptionProcessor
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Compressed
/build:Fonts/CombatLogFont.spritefont
```

- [ ] **Step 3: Create `Content/Fonts/CombatLogFont.spritefont`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>Arial</FontName>
    <Size>12</Size>
    <Spacing>0</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>*</DefaultCharacter>
    <CharacterRegions>
      <CharacterRegion>
        <Start>&#32;</Start>
        <End>&#126;</End>
      </CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
```

- [ ] **Step 4: Wire MGCB build into `RuinGame.csproj`**

Replace the `ContentCopy` item group:

```xml
  <ItemGroup>
    <ContentCopy Include="Content/**/*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

With (excludes the `.mgcb`-managed font source files from the raw copy, so only the icons still copy verbatim, while the font content builds via MGCB):

```xml
  <ItemGroup>
    <ContentCopy Include="Content/**/*" Exclude="Content/Content.mgcb;Content/Fonts/**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.4" />
  </ItemGroup>
```

- [ ] **Step 5: Build and confirm the font compiles**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`, and `bin/Debug/net8.0/Content/Fonts/CombatLogFont.xnb` exists after build

- [ ] **Step 6: Commit**

```bash
git add Content/Content.mgcb Content/Fonts/CombatLogFont.spritefont RuinGame.csproj
git commit -m "feat: add MGCB content pipeline and combat log SpriteFont"
```

---

## Task 5: `CombatLog` rendering class

**Files:**
- Create: `Scripts/Rendering/CombatLog.cs`
- Create: `Tests/RenderingTests/CombatLogTests.cs`

**Interfaces:**
- Consumes: `CombatLogEntry` from Task 1.
- Produces: `CombatLog` class with `AddEntries(IEnumerable<CombatLogEntry> entries)`, `AddSelfCastEntry(string casterName, string skillName, IEnumerable<string> effects)`, `HandleScroll(int scrollDelta)`, `Lines` (`IReadOnlyList<string>`, for testing), `ScrollOffset` (int, for testing), `Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)`. Consumed by `EncounterScene` (Task 6).

`CombatLog` formats `CombatLogEntry` into display lines (per the spec's line format) and keeps unbounded history + a clamped scroll offset. `AddSelfCastEntry` covers Rush/heal paths in `EncounterScene` that don't go through `Resolve` and so never produce a `CombatLogEntry` with hit/damage semantics — it just prints the caster-used-skill line plus one effect line per entry, matching the "self-cast always hits" behavior from the spec.

- [ ] **Step 1: Write failing tests**

Create `Tests/RenderingTests/CombatLogTests.cs`:

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Rendering;

namespace RuinGamePDT.Tests;

public class CombatLogTests
{
    [Fact]
    public void AddEntries_Hit_FormatsHitLine()
    {
        var log = new CombatLog();
        log.AddEntries(new[] { new CombatLogEntry("Mercenary", "Punch", "Goblin", WasHit: true, Damage: 4, EffectsApplied: Array.Empty<string>()) });

        Assert.Contains("Mercenary uses Punch on Goblin — HIT, 4 dmg", log.Lines);
    }

    [Fact]
    public void AddEntries_Miss_FormatsMissLine()
    {
        var log = new CombatLog();
        log.AddEntries(new[] { new CombatLogEntry("Mercenary", "Punch", "Goblin", WasHit: false, Damage: 0, EffectsApplied: Array.Empty<string>()) });

        Assert.Contains("Mercenary uses Punch on Goblin — MISS", log.Lines);
    }

    [Fact]
    public void AddEntries_NegativeDamage_FormatsHealLine()
    {
        var log = new CombatLog();
        log.AddEntries(new[] { new CombatLogEntry("Mercenary", "First Aid", "Mercenary2", WasHit: true, Damage: -8, EffectsApplied: Array.Empty<string>()) });

        Assert.Contains("Mercenary uses First Aid on Mercenary2 — HEAL, 8 hp", log.Lines);
    }

    [Fact]
    public void AddEntries_WithEffect_AddsSeparateEffectLine()
    {
        var log = new CombatLog();
        log.AddEntries(new[] { new CombatLogEntry("Mercenary", "Punch", "Goblin", WasHit: true, Damage: 4, EffectsApplied: new[] { "Bleed" }) });

        Assert.Contains("Mercenary uses Punch on Goblin — HIT, 4 dmg", log.Lines);
        Assert.Contains("Goblin: Bleed applied", log.Lines);
    }

    [Fact]
    public void AddSelfCastEntry_FormatsHitLineAndEffectLines()
    {
        var log = new CombatLog();
        log.AddSelfCastEntry("Mercenary", "Rush", new[] { "MovementPoints +" });

        Assert.Contains("Mercenary uses Rush on Mercenary — HIT, 0 dmg", log.Lines);
        Assert.Contains("Mercenary: MovementPoints + applied", log.Lines);
    }

    [Fact]
    public void HandleScroll_ClampsToZeroAndMaxOffset()
    {
        var log = new CombatLog();
        for (int i = 0; i < 5; i++)
            log.AddSelfCastEntry("A", "Skill" + i, Array.Empty<string>());

        log.HandleScroll(-100);
        Assert.Equal(0, log.ScrollOffset);

        log.HandleScroll(100);
        Assert.True(log.ScrollOffset <= log.Lines.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter CombatLogTests`
Expected: FAIL — `CombatLog` type does not exist

- [ ] **Step 3: Implement `CombatLog`**

Create `Scripts/Rendering/CombatLog.cs`:

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Combat;

namespace RuinGamePDT.Rendering;

public class CombatLog
{
    private readonly List<string> _lines = new();
    private int _scrollOffset;

    public IReadOnlyList<string> Lines => _lines;
    public int ScrollOffset => _scrollOffset;

    public void AddEntries(IEnumerable<CombatLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _lines.Add(FormatOutcomeLine(entry.AttackerName, entry.AttackName, entry.TargetName, entry.WasHit, entry.Damage));
            foreach (var effect in entry.EffectsApplied)
                _lines.Add($"{entry.TargetName}: {effect} applied");
        }
    }

    public void AddSelfCastEntry(string casterName, string skillName, IEnumerable<string> effects)
    {
        _lines.Add(FormatOutcomeLine(casterName, skillName, casterName, WasHit: true, Damage: 0));
        foreach (var effect in effects)
            _lines.Add($"{casterName}: {effect} applied");
    }

    private static string FormatOutcomeLine(string attacker, string action, string target, bool wasHit, int damage)
    {
        if (!wasHit)
            return $"{attacker} uses {action} on {target} — MISS";
        if (damage < 0)
            return $"{attacker} uses {action} on {target} — HEAL, {-damage} hp";
        return $"{attacker} uses {action} on {target} — HIT, {damage} dmg";
    }

    public void HandleScroll(int scrollDelta)
    {
        _scrollOffset = Math.Clamp(_scrollOffset + scrollDelta, 0, _lines.Count);
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle panelBounds)
    {
        sb.Draw(pixel, panelBounds, Color.Black * 0.6f);

        const int lineHeight = 16;
        const int padding = 6;
        int visibleLines = Math.Max(1, (panelBounds.Height - padding * 2) / lineHeight);

        int startIndex = Math.Max(0, _lines.Count - visibleLines - _scrollOffset);
        int endIndex = Math.Min(_lines.Count, startIndex + visibleLines);

        for (int i = startIndex; i < endIndex; i++)
        {
            int row = i - startIndex;
            var pos = new Vector2(panelBounds.X + padding, panelBounds.Y + padding + row * lineHeight);
            sb.DrawString(font, _lines[i], pos, Color.White);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter CombatLogTests`
Expected: all pass

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 6: Commit**

```bash
git add Scripts/Rendering/CombatLog.cs Tests/RenderingTests/CombatLogTests.cs
git commit -m "feat: add CombatLog line formatting, scroll state, and rendering"
```

---

## Task 6: Wire `CombatLog` into `EncounterScene` and `Game1`

**Files:**
- Modify: `Scripts/Rendering/EncounterScene.cs`
- Modify: `Game1.cs`

**Interfaces:**
- Consumes: `CombatLog` (Task 5), `CombatResolver.Resolve(...) : List<CombatLogEntry>` (Task 2), `EnemyAI.TakeTurn(...) : List<CombatLogEntry>` (Task 3).

This task feeds all four action paths into the log:
1. Basic attacks and resolver-backed skills (Defensive Stance, First Aid-as-targeted-attack) — already flow through `_resolver.Resolve(...)` at `EncounterScene.cs:107,179`.
2. Rush self-cast (`EncounterScene.cs:86-99`) — bypasses the resolver, must call `AddSelfCastEntry`.
3. First Aid direct-heal path (`EncounterScene.cs:166-176`) — bypasses the resolver, must build a `CombatLogEntry` manually (this one has a real target, not self, and a real negative damage amount, so it uses `AddEntries` with a one-item list, not `AddSelfCastEntry`).
4. Enemy turns in `Game1.Update` — feed `EnemyAI.TakeTurn`'s returned entries into the same log instance.

**Layout:** reserve a `260`px-wide left panel for the log. Shift `TileSize`-based terrain/creature/highlight rendering right by `LogPanelWidth` pixels so they don't overlap. `EncounterScene` already computes all draw rectangles from `x * TileSize` / `y * TileSize` — add a horizontal offset constant and apply it in every `Draw*` method that positions tiles, creatures, and highlights (NOT the action bar / AP pips, which stay anchored to the bottom-left at their current absolute coordinates, since the spec places the log on the left without disturbing the existing bottom hotbar).

- [ ] **Step 1: Add `LogPanelWidth` offset and `CombatLog` field to `EncounterScene`**

In `Scripts/Rendering/EncounterScene.cs`, change the constructor and fields. Replace:

```csharp
public class EncounterScene(EncounterState state, TurnManager turns, Texture2D pixel, CombatResolver resolver, Dictionary<string, Texture2D> skillIcons)
{
    private const int TileSize = 16;
```

With:

```csharp
public class EncounterScene(EncounterState state, TurnManager turns, Texture2D pixel, CombatResolver resolver, Dictionary<string, Texture2D> skillIcons, SpriteFont logFont)
{
    private const int TileSize = 16;
    private const int LogPanelWidth = 260;
```

Add a field for the log (near the other `readonly` fields):

```csharp
    private readonly CombatResolver _resolver = resolver;
    private readonly Dictionary<string, Texture2D> _skillIcons = skillIcons;
    private readonly CombatLog _combatLog = new();
```

Add `using RuinGamePDT.Combat;` is already present (line 4). No new using needed since `CombatLog` lives in `RuinGamePDT.Rendering`, same namespace as `EncounterScene`.

- [ ] **Step 2: Feed the resolver-backed attack/skill path**

Replace the resolver call in `HandleMouseClick`'s `Mode.Attack` case:

```csharp
                    else
                    {
                        _resolver.Resolve(_selected!, _activeAttack!, (gridX, gridY), state);
                    }
```

With:

```csharp
                    else
                    {
                        var entries = _resolver.Resolve(_selected!, _activeAttack!, (gridX, gridY), state);
                        _combatLog.AddEntries(entries);
                    }
```

Replace the Defensive Stance self-cast resolver call in `HandleKeyboard`:

```csharp
                if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
                {
                    var pos = state.GetPosition(skillUser);
                    _resolver.Resolve(skillUser, skill, pos, state);
                    EnterMovementMode();
                }
```

With:

```csharp
                if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
                {
                    var pos = state.GetPosition(skillUser);
                    var entries = _resolver.Resolve(skillUser, skill, pos, state);
                    _combatLog.AddEntries(entries);
                    EnterMovementMode();
                }
```

- [ ] **Step 3: Feed the Rush self-cast path (bypasses resolver)**

Replace:

```csharp
            if (JustPressed(kb, Keys.D8) && skillUser.Skills.Count > 0)
            {
                var skill = skillUser.Skills[0];
                if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
                {
                    state.SpendActionPoints(skillUser, skill.ActionPointCost);
                    if (skill.OnHit?.Stats is { Count: > 0 } stats)
                    {
                        var s = stats[0];
                        state.AddMovement(skillUser, Random.Shared.Next(s.MinAmount, s.MaxAmount + 1));
                    }
                    EnterMovementMode();
                }
            }
```

With:

```csharp
            if (JustPressed(kb, Keys.D8) && skillUser.Skills.Count > 0)
            {
                var skill = skillUser.Skills[0];
                if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
                {
                    state.SpendActionPoints(skillUser, skill.ActionPointCost);
                    var effectDescriptions = new List<string>();
                    if (skill.OnHit?.Stats is { Count: > 0 } stats)
                    {
                        var s = stats[0];
                        state.AddMovement(skillUser, Random.Shared.Next(s.MinAmount, s.MaxAmount + 1));
                        effectDescriptions.Add($"{s.Stat} +");
                    }
                    _combatLog.AddSelfCastEntry(skillUser.Name, skill.Name, effectDescriptions);
                    EnterMovementMode();
                }
            }
```

- [ ] **Step 4: Feed the First Aid direct-heal path (bypasses resolver)**

Replace:

```csharp
                    if (_activeAttack!.MaxDamage < 0)
                    {
                        // Heal skill: bypass resolver, apply HP directly
                        var target = state.GetCreatureAt(gridX, gridY);
                        if (target != null)
                        {
                            state.SpendActionPoints(_selected!, _activeAttack.ActionPointCost);
                            int heal = Random.Shared.Next(-_activeAttack.MaxDamage, -_activeAttack.MinDamage + 1);
                            target.CurrentHp += heal;
                        }
                    }
```

With:

```csharp
                    if (_activeAttack!.MaxDamage < 0)
                    {
                        // Heal skill: bypass resolver, apply HP directly
                        var target = state.GetCreatureAt(gridX, gridY);
                        if (target != null)
                        {
                            state.SpendActionPoints(_selected!, _activeAttack.ActionPointCost);
                            int heal = Random.Shared.Next(-_activeAttack.MaxDamage, -_activeAttack.MinDamage + 1);
                            target.CurrentHp += heal;
                            _combatLog.AddEntries(new[] { new CombatLogEntry(_selected!.Name, _activeAttack.Name, target.Name, WasHit: true, Damage: -heal, EffectsApplied: Array.Empty<string>()) });
                        }
                    }
```

- [ ] **Step 5: Wire mouse-wheel scroll and expose an `AddEnemyEntries` hook**

Add a public method to `EncounterScene` so `Game1` can feed enemy-turn entries in:

```csharp
    public void AddCombatLogEntries(IEnumerable<CombatLogEntry> entries) => _combatLog.AddEntries(entries);
```

Place it near the top of the public API (right after the constructor, before `Update`).

In `Update`, add scroll handling. Replace:

```csharp
    public void Update(MouseState mouse)
    {
        var kb = Keyboard.GetState();

        // Hover tile (clamped to map)
        int hx = Math.Clamp(mouse.X / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);

        HandleKeyboard(kb);
        HandleMouseClick(mouse);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }
```

With:

```csharp
    public void Update(MouseState mouse)
    {
        var kb = Keyboard.GetState();

        // Hover tile (clamped to map, accounting for the left log panel offset)
        int hx = Math.Clamp((mouse.X - LogPanelWidth) / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);

        int scrollDelta = (mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue) / 40;
        if (scrollDelta != 0)
            _combatLog.HandleScroll(-scrollDelta);

        HandleKeyboard(kb);
        HandleMouseClick(mouse);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }
```

`MouseState.ScrollWheelValue` increases when scrolling up; dividing by 40 (MonoGame's `WheelDelta` per notch) converts it to notch counts, negated so scrolling up moves toward newer (offset 0) and scrolling down reveals older lines (positive offset) — matching `HandleScroll`'s clamp direction from Task 5.

Also update `HandleMouseClick`'s grid coordinate calculation to account for the offset. Replace:

```csharp
        int gridX = mouse.X / TileSize;
        int gridY = mouse.Y / TileSize;

        if (gridX < 0 || gridX >= state.Map.Width || gridY < 0 || gridY >= state.Map.Height)
```

With:

```csharp
        int gridX = (mouse.X - LogPanelWidth) / TileSize;
        int gridY = mouse.Y / TileSize;

        if (mouse.X < LogPanelWidth || gridX >= state.Map.Width || gridY < 0 || gridY >= state.Map.Height)
```

- [ ] **Step 6: Shift terrain/creature/highlight rendering right by `LogPanelWidth`, draw the log panel**

Update every `Draw*` method that positions tiles using `x * TileSize` / `y * TileSize` for the X axis to add `LogPanelWidth`. The action bar and AP pips already use absolute `barX = 8` / `x0 = 8` — leave those untouched (they anchor to the true bottom-left of the screen, not the map origin).

Replace `DrawTerrain`:

```csharp
    private void DrawTerrain(SpriteBatch sb)
    {
        for (int x = 0; x < state.Map.Width; x++)
        for (int y = 0; y < state.Map.Height; y++)
        {
            var color = state.Map.GetTile(x, y) switch
            {
                EncounterTileType.Obstacle => new Color(50, 50, 50),
                EncounterTileType.Hazard   => new Color(200, 100, 0),
                _                          => new Color(90, 90, 90)
            };
            sb.Draw(pixel, new Rectangle(LogPanelWidth + x * TileSize, y * TileSize, TileSize, TileSize), color);
        }
    }
```

Replace `DrawMovementHighlights`:

```csharp
    private void DrawMovementHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Movement) return;
        foreach (var (pos, _) in _reachable)
            sb.Draw(pixel, new Rectangle(LogPanelWidth + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Yellow * 0.35f);
    }
```

Replace `DrawAttackHighlights`:

```csharp
    private void DrawAttackHighlights(SpriteBatch sb)
    {
        if (_mode != Mode.Attack) return;
        foreach (var pos in _validTargets)
            sb.Draw(pixel, new Rectangle(LogPanelWidth + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Red * 0.35f);
    }
```

Replace `DrawAoePreview`:

```csharp
    private void DrawAoePreview(SpriteBatch sb)
    {
        if (_mode != Mode.Attack || _activeAttack == null) return;
        if (!_validTargets.Contains(_hoverTile)) return;

        foreach (var (dx, dy) in _activeAttack.AttackShape.Offsets)
        {
            int x = _hoverTile.X + dx;
            int y = _hoverTile.Y + dy;
            if (x < 0 || x >= state.Map.Width || y < 0 || y >= state.Map.Height) continue;
            sb.Draw(pixel, new Rectangle(LogPanelWidth + x * TileSize, y * TileSize, TileSize, TileSize), Color.Cyan * 0.5f);
        }
    }
```

Replace `DrawCreatures`:

```csharp
    private void DrawCreatures(SpriteBatch sb)
    {
        foreach (var merc in state.Mercenaries)
        {
            var pos = state.GetPosition(merc);
            var color = merc == _selected ? Color.Cyan : Color.DodgerBlue;
            sb.Draw(pixel, new Rectangle(LogPanelWidth + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), color);
        }
        foreach (var enemy in state.Enemies)
        {
            var pos = state.GetPosition(enemy);
            sb.Draw(pixel, new Rectangle(LogPanelWidth + pos.X * TileSize, pos.Y * TileSize, TileSize, TileSize), Color.Crimson);
        }
    }
```

Replace `DrawHpBars`:

```csharp
    private void DrawHpBars(SpriteBatch sb)
    {
        foreach (var c in state.Mercenaries.Concat(state.Enemies))
        {
            var pos = state.GetPosition(c);
            float max = c.CombatStats.HitPoints;
            float cur = Math.Max(0, c.CurrentHp);
            int fillW = max <= 0 ? 0 : (int)Math.Round(TileSize * (cur / max));

            int barX = LogPanelWidth + pos.X * TileSize;
            int barY = pos.Y * TileSize - 5;
            sb.Draw(pixel, new Rectangle(barX, barY, TileSize, 3), new Color(60, 0, 0));
            if (fillW > 0)
                sb.Draw(pixel, new Rectangle(barX, barY, fillW, 3), new Color(40, 200, 40));
        }
    }
```

Add the log panel draw call to `Draw`. Replace:

```csharp
    public void Draw(SpriteBatch sb)
    {
        DrawTerrain(sb);
        DrawMovementHighlights(sb);
        DrawAttackHighlights(sb);
        DrawAoePreview(sb);
        DrawCreatures(sb);
        DrawHpBars(sb);
        DrawActionBar(sb);
        DrawApPips(sb);
    }
```

With:

```csharp
    public void Draw(SpriteBatch sb)
    {
        DrawTerrain(sb);
        DrawMovementHighlights(sb);
        DrawAttackHighlights(sb);
        DrawAoePreview(sb);
        DrawCreatures(sb);
        DrawHpBars(sb);
        DrawActionBar(sb);
        DrawApPips(sb);
        _combatLog.Draw(sb, pixel, logFont, new Rectangle(0, 0, LogPanelWidth, 900));
    }
```

- [ ] **Step 7: Update `Game1.cs` to load the font and feed enemy-turn entries**

Add font loading to `LoadContent`. Replace:

```csharp
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
```

With:

```csharp
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        var logFont = Content.Load<SpriteFont>("Fonts/CombatLogFont");
```

Update the `EncounterScene` construction. Replace:

```csharp
        var resolver = new CombatResolver(Random.Shared.Next);
        _scene = new EncounterScene(_encounterState, _turnManager, _pixel, resolver, _skillIcons);
        _ai = new EnemyAI(resolver);
```

With:

```csharp
        var resolver = new CombatResolver(Random.Shared.Next);
        _scene = new EncounterScene(_encounterState, _turnManager, _pixel, resolver, _skillIcons, logFont);
        _ai = new EnemyAI(resolver);
```

Update `Update` to feed enemy-turn entries into the scene's log. Replace:

```csharp
        if (_turnManager.CurrentCreature is not Mercenary && _turnManager.CanMove(_turnManager.CurrentCreature!))
        {
            var enemy = _turnManager.CurrentCreature!;
            _ai.TakeTurn(enemy, _encounterState);
            _turnManager.EndCreatureTurn(enemy);
        }
```

With:

```csharp
        if (_turnManager.CurrentCreature is not Mercenary && _turnManager.CanMove(_turnManager.CurrentCreature!))
        {
            var enemy = _turnManager.CurrentCreature!;
            var entries = _ai.TakeTurn(enemy, _encounterState);
            _scene.AddCombatLogEntries(entries);
            _turnManager.EndCreatureTurn(enemy);
        }
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 10: Manual verification**

Run: `dotnet run`
Expected: game launches, left panel visible (dark overlay strip, 260px wide) with no text yet (empty log). Select a mercenary, attack, confirm a line appears in the panel showing attacker/action/target/hit-or-miss/damage. Use Rush and First Aid, confirm both produce log lines. Scroll the mouse wheel over the panel, confirm older lines become visible once there are more than fit on screen.

- [ ] **Step 11: Commit**

```bash
git add Scripts/Rendering/EncounterScene.cs Game1.cs
git commit -m "feat: wire combat log into EncounterScene rendering and input, feed all four action paths"
```

---

## Self-Review Notes

- **Spec coverage:** attacker/action/target/hit-miss/damage/effects logging — Tasks 2, 5, 6. Scrollable full history — Task 5 (`HandleScroll`, unbounded `_lines`). Left-side panel, doesn't overlap hotbar — Task 6 Step 6 (`LogPanelWidth` shift, hotbar/AP pips left untouched). Line format exactly as specified — Task 5 `FormatOutcomeLine`.
- **Rush/First Aid bypass paths:** both explicitly wired in Task 6 Steps 3–4, since Task 2's resolver-return-value change alone would miss them.
- **EnemyAI:** Task 3 changes its return type; Task 6 Step 7 is where those entries actually reach the visible log.
