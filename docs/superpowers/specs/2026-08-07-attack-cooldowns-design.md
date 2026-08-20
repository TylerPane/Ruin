# Attack Cooldowns and Enemy Attack Cap — Design

## Goal

Give individual attacks a per-use cooldown (Skewer: 1 round, Quill Spray: 3 rounds, both on Prickleback Goblin), and cap how many attacks an enemy can make in one turn (default 1) instead of `EnemyAI` looping until it runs out of AP.

## Data model

**`Attack.Cooldown` (int, default `0`).** New optional constructor parameter, following the same pattern as existing default-0 params (`minRange`, `autoCrit`). `0` means no cooldown — the attack is usable any time AP allows, which is the behavior every existing attack keeps unchanged. Mercenary weapon attacks and skills are never given a nonzero cooldown; they remain limited only by AP, per existing design.

**`Creature.MaxAttacksPerTurn` (int, default `1`).** Set in each creature's constructor. This is a creature-level trait (not enemy-specific), though today only `EnemyAI` reads it — mercenaries are driven by the player's explicit clicks and AP, not a turn-loop, so this field has no effect on them yet.

**`Creature._cooldownsRemaining` (`Dictionary<Attack, int>`, private).** When an attack with `Cooldown > 0` is used, its entry is set to `Cooldown`. `Creature.IsOnCooldown(Attack attack)` returns true if the dict contains that attack with a remaining value `> 0`.

## Cooldown lifecycle

"1 round" and "3 rounds" mean this creature's own next 1 or 3 turns — matching how `StatusEffect` durations already tick per-creature-turn via `Creature.TickStatusEffects()`. A 1-round cooldown on Skewer means: use it this turn, it's unavailable next turn, available again the turn after.

Ticking happens in `TurnManager.AdvanceToNextTurn`, in the same place `TickStatusEffects()` is already called for the creature whose turn is starting. A new `Creature.TickCooldowns()` method decrements every entry in `_cooldownsRemaining` by 1 and removes any that reach 0.

Cooldowns start in `CombatResolver.Resolve` — the single place all attack usage funnels through (mercenary-triggered via `EncounterScene`, enemy-triggered via `EnemyAI`). After resolving an attack with `Cooldown > 0`, `attacker.StartCooldown(attack)` sets `_cooldownsRemaining[attack] = attack.Cooldown`. This keeps the rule uniform regardless of who used the attack, even though only enemy attacks are expected to actually carry a nonzero cooldown today.

## `EnemyAI` changes

**Attack selection respects cooldowns.** `ChooseBestAttack` filters out any attack where `enemy.IsOnCooldown(attack)` is true before picking the highest-average-damage affordable option. No special-casing for "this is the preferred attack" — the existing average-damage comparison naturally falls back to the next-best available attack (e.g. Scratch) when Skewer or Quill Spray are on cooldown.

**Attack count is capped per turn.** `TakeTurn`'s attack loop tracks `attacksThisTurn` and stops once it reaches `enemy.MaxAttacksPerTurn`, instead of looping until AP or valid targets run out. For the Prickleback Goblin (`MaxAttacksPerTurn = 1`, the default), this means it makes exactly one attack per turn regardless of remaining AP.

## Out of scope

- Cooldowns/attack caps for mercenaries (their action economy is unaffected by this feature).
- A full encounter-round counter (cooldowns tick per-creature-turn, not per-round-of-all-combatants — see design discussion above).
- Cooldown display in the UI (combat log entries and hotbar are unaffected; a future feature could show remaining cooldown visually, e.g. in the enemy inspect window, but that's not part of this work).

## Testing

- `CombatResolver`: using an attack with `Cooldown > 0` starts its cooldown on the attacker; using one with `Cooldown == 0` never does; `IsOnCooldown` reflects the started cooldown immediately after use.
- `Creature`: `TickCooldowns()` decrements and clears entries correctly over N calls; an attack becomes usable again exactly N turns after being used with cooldown N.
- `EnemyAI`: `ChooseBestAttack` skips a cooldown-blocked preferred attack and falls back to the next-best affordable one; `TakeTurn` stops after `MaxAttacksPerTurn` attacks even with AP remaining.
