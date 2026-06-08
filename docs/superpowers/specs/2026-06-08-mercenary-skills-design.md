# Mercenary Skills Design

**Date:** 2026-06-08

## Overview

Add four active skills to mercenaries: Rush, Dodge, Block, and First Aid. Skills reuse the existing `Attack` pipeline — they resolve identically to weapon attacks via `CombatResolver`. A new `Skill : Attack` subclass acts as a type marker so the UI and AI can distinguish skills from weapon attacks.

## The `Skill` Class

`Skill` subclasses `Attack` with no new fields. Its sole purpose is type identity.

```csharp
public class Skill(string name, ...) : Attack(name, ...)
```

## `Creature` Changes

`Creature` gains a `Skills` property alongside the existing `Attacks`:

```csharp
public List<Skill> Skills { get; } = [];
```

Only `Mercenary` populates this list. The base class does not.

## `Mercenary` Changes

`Mercenary`'s constructor populates `Skills` with the four skills below. Rush's movement bonus is computed once at construction time from `CombatStats.MovementPoints / 2` and stored as fixed `minAmount`/`maxAmount` values.

First Aid is only added if `BaseStats.Mind >= 3`.

### Skill Definitions

| Skill | MinDmg | MaxDmg | AP | Accuracy | Shape | Range | OnHit Effect |
|---|---|---|---|---|---|---|---|
| Rush | 0 | 0 | 1 | 100 | `(0,0)` | 0 | `StatIncrease, MovementPoints, MP/2, MP/2, 1 turn` |
| Dodge | 0 | 0 | 1 | 100 | `(0,0)` | 0 | `StatIncrease, Evasion, 5, 15, 1 turn` |
| Block | 0 | 0 | 1 | 100 | `(0,0)` | 0 | `StatIncrease, PhysicalDefense, 5, 15, 1 turn` |
| First Aid | -Mind | -1 | 2 | 100 | `(0,0)` | 1 | none |

**Rush** — costs 1 AP, targets self (`range: 0`, shape `(0,0)`), grants extra movement this turn equal to half the mercenary's total MovementPoints.

**Dodge** — costs 1 AP, targets self, raises Evasion by 5–15 for 1 turn via `StatIncrease` status effect.

**Block** — costs 1 AP, targets self, raises PhysicalDefense by 5–15 for 1 turn via `StatIncrease` status effect.

**First Aid** — costs 2 AP, targets any creature within range 1. Deals negative damage (`minDamage: -Mind, maxDamage: -1`), so `Random.Next(-Mind, 0)` heals the target for 1 to Mind HP. The existing `CurrentHp` clamp prevents healing above max HP. Requires `BaseStats.Mind >= 3` to be added to the mercenary's skill list.

## `CombatResolver` Changes

The damage branch needs a third case to let negative damage (heals) pass through:

```csharp
if (attack.MaxDamage > 0)      dmg = Math.Max(1, dmg);  // normal attack — min 1
else if (attack.MaxDamage == 0) dmg = 0;                 // utility (Rush/Dodge/Block/Shout)
// MaxDamage < 0: heal — dmg is negative, CurrentHp clamp caps at max HP
```

No other resolver changes are needed.

## Testing

- `Skill` class exists and is assignable from `Attack`
- `Mercenary` has Rush, Dodge, and Block in `Skills` regardless of stats
- `Mercenary` with `Mind < 3` does not have First Aid in `Skills`
- `Mercenary` with `Mind >= 3` has First Aid with `minDamage: -Mind, maxDamage: -1`
- Rush `OnHit` amount equals `MovementPoints / 2`
- Resolver: negative-damage attack increases `CurrentHp`, capped at max HP
- Resolver: `MaxDamage == 0` skill still deals 0 HP change
