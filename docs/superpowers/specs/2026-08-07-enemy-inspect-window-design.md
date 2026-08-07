# Enemy Inspect Window — Design

## Goal

Let the player right-click an enemy to see a small in-game window with its portrait and attacks, and hover an attack to see its stats (damage, accuracy, range, effects).

## Trigger and lifecycle

- **Open:** right-click on any enemy tile, in any mode (Idle/Movement/Attack). Does not change `_selected`, `_mode`, or `_activeAttack` — it's a read-only overlay layered on top of whatever else is happening.
- **Close:** Esc key, right-click anywhere else (including empty tiles or a different enemy — which instead re-opens the window for the new enemy), or left-click anywhere outside the window's bounds. If the window is open, any click (left or right) is consumed by the close/re-open logic first — it never falls through to normal movement/attack/hotbar handling for that click.

## Position and clamping

The window opens anchored near the clicked enemy's tile (offset a few pixels up-and-right of the tile), then clamps so it never draws outside the actual game window (viewport) bounds — `[0, viewportWidth]` × `[0, viewportHeight]`, the same viewport dimensions already used for centering the map. This is a harder constraint than staying within the map's own rectangle: the window is allowed to render over the log panel or hotbar area if the anchor position requires it, but never past the edge of the game window itself (no clipping into the black letterboxed edges).

## Layout

- **Portrait:** a colored square using the enemy's existing render color (`Color.Crimson`), with the enemy's name drawn below it in `logFont`.
- **Attack row:** one small colored placeholder box per entry in `enemy.Attacks`, laid out left-to-right below the portrait — same visual language as the hotbar boxes (colored square, no icon, since no enemy attack icons exist yet).

## Hover tooltip

While the inspect window is open, hovering the mouse over one of the attack boxes shows a small floating tooltip near the cursor with:
- Attack name
- Min–max damage
- Accuracy%
- Range
- Effect description line, if `OnHit`/`OnCrit` is set (reusing the same effect-description formatting already used for the combat log, e.g. `"Bleed"` or `"PhysicalDefense +"`)

The tooltip shows the attack's raw stats as defined on the `Attack` object — not a projection against any specific mercenary's defense/evasion.

## New state in `EncounterScene`

- `_inspectedEnemy` (nullable `Creature`) — which enemy's window is open; `null` when closed.
- `_inspectWindowOrigin` (`(int X, int Y)`) — computed screen position, set once when the window opens (recomputed if a different enemy is right-clicked while one is already open).
- `_hoveredAttackIndex` (nullable `int`) — which attack box in the open window the mouse currently sits over, for the tooltip. `null` when the window is closed or the mouse isn't over any box.

## Input handling

`EncounterScene` currently only reads `mouse.LeftButton`. This adds `mouse.RightButton` edge-detection (a `_prevMouse.RightButton` check, mirroring the existing left-click "just pressed" pattern) to detect right-clicks on enemy tiles.

## Data flow

No game-state mutation. This never touches `EncounterState`, `TurnManager`, or `CombatResolver` — pure rendering plus read-only input handling.

## Testing

`EncounterScene`'s existing input/draw logic has no unit tests (established pattern from prior features in this codebase — only `CombatLog` itself is unit-tested, not its `EncounterScene` wiring). This feature stays consistent: build + manual verification, no new automated tests.

## Out of scope

- Real portrait art or attack icons for enemies (colored placeholders only, matches existing creature-tile rendering style).
- Damage/hit-chance projected against a specific mercenary — tooltip shows raw attack stats only.
- Inspecting mercenaries (this is enemy-only, per the request).
