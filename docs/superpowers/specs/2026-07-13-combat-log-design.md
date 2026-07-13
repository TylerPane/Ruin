# Combat Log — Design

## Goal

Show a visible, scrollable combat log during encounters. Every attack or skill use logs: attacker, action name, target, hit/miss, damage (or heal), and any status effect applied.

## Architecture

**`CombatResolver.Resolve(...)` changes from `void` to returning `List<CombatLogEntry>`.**

One entry per affected target — a single `Resolve` call can produce multiple entries for AOE attacks/skills that hit several tiles. Self-cast skills (Rush, Defensive Stance) produce one entry with target = self.

```csharp
public record CombatLogEntry(
    string AttackerName,
    string AttackName,
    string TargetName,
    bool WasHit,
    int Damage,                        // 0 on miss; negative = heal
    IReadOnlyList<string> EffectsApplied
);
```

`EffectsApplied` holds human-readable effect descriptions (e.g. `"Bleed"`, `"Evasion +8"`) generated from the `AttackEffect`/`StatChange` data already applied to the target this resolution.

**`EncounterScene`** appends returned entries to a running log (`List<CombatLogEntry>` or pre-formatted `List<string>`) each time it calls `_resolver.Resolve(...)`. No behavior changes to resolution logic itself — only capturing what already happens.

**`CombatLog`** — new class, owns:
- The list of log lines (full history, unbounded for a single encounter — cleared on new encounter).
- A scroll offset (int, lines scrolled from bottom).
- `Draw(SpriteBatch sb, SpriteFont font)` — renders visible lines in a fixed-width vertical panel.
- `HandleScroll(int scrollDelta)` — called from `EncounterScene.Update` on mouse wheel input, clamps offset to `[0, lines.Count - visibleLines]`.

## Layout

Left-side vertical panel, full encounter-viewport height, narrow fixed width (e.g. 260px) — does not overlap the terrain grid, hotbar (y=810+), or AP pips. Map/terrain rendering shifts right by the panel width, or panel draws over a reserved left margin (implementation detail for the plan).

Requires a `SpriteFont` — none exists in the project yet. Add one via Content Pipeline (`Content/Fonts/CombatLogFont.spritefont` or similar), load in `Game1.LoadContent`, pass reference into `EncounterScene`/`CombatLog`.

## Log line format

One line per outcome; effects get their own line directly after:

- Hit: `"{Attacker} uses {Attack} on {Target} — HIT, {Damage} dmg"`
- Miss: `"{Attacker} uses {Attack} on {Target} — MISS"`
- Heal (Damage < 0): `"{Attacker} uses {Attack} on {Target} — HEAL, {-Damage} hp"`
- Effect (per entry in EffectsApplied): `"{Target}: {Effect} applied"`

Self-cast: `{Target}` == `{Attacker}`, reads naturally (e.g. `"Mercenary uses Rush on Mercenary — HIT, 0 dmg"` then `"Mercenary: Movement +3 applied"`).

## Testing

- `CombatResolver` unit tests: assert returned `CombatLogEntry` list contents for hit, miss, crit, AOE multi-target, and effect-applying skills (Defensive Stance, Rush, First Aid). Pure logic, no rendering involved.
- Manual verification: run encounter, confirm log panel renders and scrolls, confirm entries match actual combat outcomes.

## Out of scope

- Log persistence across encounters/sessions.
- Filtering/searching log entries.
- Colorized/rich text formatting beyond plain strings (can be a follow-up).
