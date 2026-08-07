# Enemy Inspect Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Right-click an enemy to open a small read-only window showing its portrait and attacks; hovering an attack shows its stats (damage, accuracy, range, effects) in a tooltip.

**Architecture:** A new `EnemyInspectWindow` class (mirrors the existing `CombatLog` class pattern — owns its own state and a `Draw` method, driven from `EncounterScene`) renders the portrait/attack-row/tooltip. `EncounterScene` gains right-click detection and forwards clicks/hover into the window. `CombatResolver.DescribeEffect` becomes `public static` so the tooltip can reuse the exact same effect-description text already used in the combat log, instead of duplicating the format logic.

**Tech Stack:** C# / .NET 8 / MonoGame / xUnit

## Global Constraints

- Right-click opens the window on any enemy tile, in any mode (Idle/Movement/Attack) — never changes `_selected`/`_mode`/`_activeAttack`.
- Close on: Esc key, right-click elsewhere (including re-opening for a different enemy), or left-click outside the window's bounds. While open, ANY click (left or right) is consumed by the window first — it never falls through to normal movement/attack/hotbar handling for that click.
- Window position anchors near the clicked enemy's tile, then clamps to `[0, viewportWidth]` × `[0, viewportHeight]` — the actual game window (same viewport dimensions already used for map centering) — not just the map's own rectangle. It may render over the log panel or hotbar area, but never past the game window's edge.
- Portrait is a colored square using `Color.Crimson` (the same color enemies already render as) plus the enemy's name in `logFont`. Attack row is one colored placeholder box per `Attack` in `enemy.Attacks`, no icons.
- Tooltip shows raw `Attack` stats only (name, min–max damage, accuracy%, range, effect description) — never a value computed against a specific mercenary's defense.
- No `EncounterState`/`TurnManager`/`CombatResolver.Resolve` mutation anywhere in this feature — purely rendering + read-only input.
- No new automated tests for `EncounterScene`'s input/draw wiring (established codebase pattern — only `CombatLog`'s own logic is unit-tested, not its call sites in `EncounterScene`). `EnemyInspectWindow`'s own formatting logic DOES get unit tests, mirroring how `CombatLog` itself is tested today.

---

## File Map

| File | Change |
|------|--------|
| `Scripts/Encounter/CombatResolver.cs` | Change `DescribeEffect` from `private static` to `public static` |
| `Scripts/Rendering/EnemyInspectWindow.cs` | Create — owns open/closed state, layout math, tooltip text formatting, `Draw` |
| `Tests/RenderingTests/EnemyInspectWindowTests.cs` | Create — unit tests for tooltip text formatting and open/close/hover state transitions |
| `Scripts/Rendering/EncounterScene.cs` | Modify — own an `EnemyInspectWindow`, add right-click detection, route clicks/hover through it, draw it last |

---

## Task 1: Make `CombatResolver.DescribeEffect` reusable

**Files:**
- Modify: `Scripts/Encounter/CombatResolver.cs:75-86`

**Interfaces:**
- Produces: `CombatResolver.DescribeEffect(AttackEffect effect) : IEnumerable<string>` — now `public static`, callable from `EnemyInspectWindow` (Task 2) without needing a `CombatResolver` instance.

The method body doesn't change, only its accessibility.

- [ ] **Step 1: Change the method signature**

In `Scripts/Encounter/CombatResolver.cs`, replace:

```csharp
    private static IEnumerable<string> DescribeEffect(AttackEffect effect)
    {
```

With:

```csharp
    public static IEnumerable<string> DescribeEffect(AttackEffect effect)
    {
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run the existing CombatResolver tests to confirm no regression**

Run: `dotnet test --filter CombatResolverTests`
Expected: all existing tests still pass (this is a pure accessibility change, behavior is identical)

- [ ] **Step 4: Commit**

```bash
git add Scripts/Encounter/CombatResolver.cs
git commit -m "refactor: make CombatResolver.DescribeEffect public so other renderers can reuse it"
```

---

## Task 2: `EnemyInspectWindow` — state, layout, and tooltip formatting

**Files:**
- Create: `Scripts/Rendering/EnemyInspectWindow.cs`
- Create: `Tests/RenderingTests/EnemyInspectWindowTests.cs`

**Interfaces:**
- Consumes: `CombatResolver.DescribeEffect(AttackEffect) : IEnumerable<string>` (Task 1). `Creature` (`Name`, `Attacks` list) and `Attack` (`Name`, `MinDamage`, `MaxDamage`, `Accuracy`, `Range`, `OnHit`, `OnCrit`) from existing code — no changes needed to either.
- Produces (consumed by `EncounterScene` in Task 3):
  - `EnemyInspectWindow` — parameterless constructor, mirrors `CombatLog`'s pattern.
  - `bool IsOpen` — true when a window is currently showing.
  - `Creature? InspectedEnemy` — the enemy currently shown, `null` if closed.
  - `void Open(Creature enemy, (int X, int Y) anchorScreenPos, int viewportWidth, int viewportHeight)` — opens (or re-anchors, if a different enemy) the window near `anchorScreenPos`, clamped into `[0, viewportWidth]` × `[0, viewportHeight]`.
  - `void Close()` — closes the window and clears hover state.
  - `void UpdateHover(int mouseX, int mouseY)` — recomputes which attack box (if any) the mouse sits over; no-ops if closed.
  - `bool Contains(int mouseX, int mouseY)` — true if the given screen point is inside the window's current bounds; used by `EncounterScene` to decide "click was inside vs outside."
  - `int? HotboxAt(int mouseX, int mouseY)` — the attack-box index (0-based, into `InspectedEnemy.Attacks`) under the given point, or `null`.
  - `string FormatTooltip(Attack attack)` — the exact tooltip text for one attack, built from its raw stats.
  - `void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)` — draws portrait, name, attack row, and (if hovering) the tooltip. No-ops if closed.

Layout constants (all `private const int` inside the class): `WindowWidth = 180`, `WindowHeight = 140`, `PortraitSize = 48`, `AttackBoxSize = 24`, `AttackBoxGap = 4`, `Padding = 8`.

- [ ] **Step 1: Write failing tests for tooltip formatting and state transitions**

Create `Tests/RenderingTests/EnemyInspectWindowTests.cs`:

```csharp
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Rendering;

namespace RuinGamePDT.Tests;

public class EnemyInspectWindowTests
{
    private static Attack MakeAttack(string name, int minDmg, int maxDmg, int accuracy, int range, AttackEffect? onHit = null)
    {
        return new Attack(name, minDmg, maxDmg, actionPointCost: 1, accuracy,
            new AttackShape(new[] { (0, 0) }), range, onHit: onHit);
    }

    [Fact]
    public void FormatTooltip_NoEffect_ShowsNameDamageAccuracyRange()
    {
        var window = new EnemyInspectWindow();
        var attack = MakeAttack("Scratch", 1, 3, 100, 1);

        string tooltip = window.FormatTooltip(attack);

        Assert.Contains("Scratch", tooltip);
        Assert.Contains("1-3", tooltip);
        Assert.Contains("100%", tooltip);
        Assert.Contains("Range 1", tooltip);
    }

    [Fact]
    public void FormatTooltip_WithOnHitEffect_AppendsEffectDescription()
    {
        var window = new EnemyInspectWindow();
        var onHit = new AttackEffect(AttackEffectType.Bleed,
            new[] { new StatChange(CombatStat.HitPoints, 5, 5) },
            MinDuration: 1, MaxDuration: 3);
        var attack = MakeAttack("Skewer", 2, 4, 100, 5, onHit);

        string tooltip = window.FormatTooltip(attack);

        Assert.Contains("Skewer", tooltip);
        Assert.Contains("Bleed", tooltip);
    }

    [Fact]
    public void Open_SetsIsOpenAndInspectedEnemy()
    {
        var window = new EnemyInspectWindow();
        var enemy = new TestCreature("Goblin", 0, 1, 1, 0, 1);

        window.Open(enemy, (100, 100), viewportWidth: 1920, viewportHeight: 900);

        Assert.True(window.IsOpen);
        Assert.Same(enemy, window.InspectedEnemy);
    }

    [Fact]
    public void Close_ClearsIsOpenAndInspectedEnemy()
    {
        var window = new EnemyInspectWindow();
        var enemy = new TestCreature("Goblin", 0, 1, 1, 0, 1);
        window.Open(enemy, (100, 100), viewportWidth: 1920, viewportHeight: 900);

        window.Close();

        Assert.False(window.IsOpen);
        Assert.Null(window.InspectedEnemy);
    }

    [Fact]
    public void Open_NearRightEdge_ClampsWithinViewport()
    {
        var window = new EnemyInspectWindow();
        var enemy = new TestCreature("Goblin", 0, 1, 1, 0, 1);

        // Anchor far past the right/bottom edge of a 1920x900 viewport.
        window.Open(enemy, (1900, 890), viewportWidth: 1920, viewportHeight: 900);

        Assert.True(window.Contains(1919, 889));
        Assert.False(window.Contains(1921, 889)); // outside the viewport entirely
    }

    [Fact]
    public void HotboxAt_PointOverFirstAttackBox_ReturnsIndexZero()
    {
        var window = new EnemyInspectWindow();
        var enemy = new TestCreature("Goblin", 0, 1, 1, 0, 1);
        enemy.Attacks.Add(MakeAttack("Scratch", 1, 3, 100, 1));
        window.Open(enemy, (0, 0), viewportWidth: 1920, viewportHeight: 900);

        // The window opens anchored at (0,0). HotboxAt's rowY = bounds.Y + PortraitSize (48)
        // + Padding * 2 (16) = 64; the first box starts at bounds.X + Padding (8), spanning
        // [8, 32) horizontally and [64, 88) vertically. (12, 70) sits inside both ranges.
        int? hit = window.HotboxAt(12, 70);

        Assert.Equal(0, hit);
    }

    [Fact]
    public void HotboxAt_PointOutsideAnyBox_ReturnsNull()
    {
        var window = new EnemyInspectWindow();
        var enemy = new TestCreature("Goblin", 0, 1, 1, 0, 1);
        enemy.Attacks.Add(MakeAttack("Scratch", 1, 3, 100, 1));
        window.Open(enemy, (0, 0), viewportWidth: 1920, viewportHeight: 900);

        int? hit = window.HotboxAt(5000, 5000);

        Assert.Null(hit);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter EnemyInspectWindowTests`
Expected: FAIL — `EnemyInspectWindow` type does not exist

- [ ] **Step 3: Implement `EnemyInspectWindow`**

Create `Scripts/Rendering/EnemyInspectWindow.cs`:

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;

namespace RuinGamePDT.Rendering;

public class EnemyInspectWindow
{
    private const int WindowWidth = 180;
    private const int WindowHeight = 140;
    private const int PortraitSize = 48;
    private const int AttackBoxSize = 24;
    private const int AttackBoxGap = 4;
    private const int Padding = 8;

    private Rectangle _bounds;
    private int? _hoveredAttackIndex;

    public bool IsOpen { get; private set; }
    public Creature? InspectedEnemy { get; private set; }

    public void Open(Creature enemy, (int X, int Y) anchorScreenPos, int viewportWidth, int viewportHeight)
    {
        IsOpen = true;
        InspectedEnemy = enemy;
        _hoveredAttackIndex = null;

        int x = Math.Clamp(anchorScreenPos.X, 0, Math.Max(0, viewportWidth - WindowWidth));
        int y = Math.Clamp(anchorScreenPos.Y, 0, Math.Max(0, viewportHeight - WindowHeight));
        _bounds = new Rectangle(x, y, WindowWidth, WindowHeight);
    }

    public void Close()
    {
        IsOpen = false;
        InspectedEnemy = null;
        _hoveredAttackIndex = null;
    }

    public bool Contains(int mouseX, int mouseY) => IsOpen && _bounds.Contains(mouseX, mouseY);

    public void UpdateHover(int mouseX, int mouseY)
    {
        _hoveredAttackIndex = IsOpen ? HotboxAt(mouseX, mouseY) : null;
    }

    public int? HotboxAt(int mouseX, int mouseY)
    {
        if (!IsOpen || InspectedEnemy == null) return null;

        int rowY = _bounds.Y + PortraitSize + Padding * 2;
        for (int i = 0; i < InspectedEnemy.Attacks.Count; i++)
        {
            int boxX = _bounds.X + Padding + i * (AttackBoxSize + AttackBoxGap);
            var box = new Rectangle(boxX, rowY, AttackBoxSize, AttackBoxSize);
            if (box.Contains(mouseX, mouseY)) return i;
        }
        return null;
    }

    public string FormatTooltip(Attack attack)
    {
        var line = $"{attack.Name}\n{attack.MinDamage}-{attack.MaxDamage} dmg  {attack.Accuracy}%  Range {attack.Range}";

        var effects = new List<string>();
        if (attack.OnHit != null) effects.AddRange(CombatResolver.DescribeEffect(attack.OnHit));
        if (attack.OnCrit != null) effects.AddRange(CombatResolver.DescribeEffect(attack.OnCrit));

        return effects.Count == 0 ? line : $"{line}\n{string.Join(", ", effects)}";
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)
    {
        if (!IsOpen || InspectedEnemy == null) return;

        sb.Draw(pixel, _bounds, Color.Black * 0.85f);

        var portraitRect = new Rectangle(_bounds.X + Padding, _bounds.Y + Padding, PortraitSize, PortraitSize);
        sb.Draw(pixel, portraitRect, Color.Crimson);
        sb.DrawString(font, InspectedEnemy.Name, new Vector2(portraitRect.X, portraitRect.Bottom + 2), Color.White);

        int rowY = _bounds.Y + PortraitSize + Padding * 2;
        for (int i = 0; i < InspectedEnemy.Attacks.Count; i++)
        {
            int boxX = _bounds.X + Padding + i * (AttackBoxSize + AttackBoxGap);
            sb.Draw(pixel, new Rectangle(boxX, rowY, AttackBoxSize, AttackBoxSize), Color.DarkSlateGray);
        }

        if (_hoveredAttackIndex is int idx && idx < InspectedEnemy.Attacks.Count)
        {
            string tooltip = FormatTooltip(InspectedEnemy.Attacks[idx]);
            var tooltipPos = new Vector2(_bounds.X, _bounds.Bottom + 4);
            sb.Draw(pixel, new Rectangle((int)tooltipPos.X, (int)tooltipPos.Y, WindowWidth, 40), Color.Black * 0.9f);
            sb.DrawString(font, tooltip, tooltipPos + new Vector2(4, 4), Color.White);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter EnemyInspectWindowTests`
Expected: all pass

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 6: Commit**

```bash
git add Scripts/Rendering/EnemyInspectWindow.cs Tests/RenderingTests/EnemyInspectWindowTests.cs
git commit -m "feat: add EnemyInspectWindow with layout, hover hit-testing, and tooltip formatting"
```

---

## Task 3: Wire `EnemyInspectWindow` into `EncounterScene`

**Files:**
- Modify: `Scripts/Rendering/EncounterScene.cs`

**Interfaces:**
- Consumes: `EnemyInspectWindow` (Task 2) — `Open`, `Close`, `Contains`, `UpdateHover`, `IsOpen`, `Draw`.

This task adds right-click detection, routes all clicks through the window first (open/close/hover-tooltip), and draws it last so it layers on top of everything else.

- [ ] **Step 1: Add the window field**

In `Scripts/Rendering/EncounterScene.cs`, add alongside the existing `_combatLog` field:

```csharp
    private readonly CombatLog _combatLog = new();
    private readonly EnemyInspectWindow _inspectWindow = new();
```

- [ ] **Step 2: Update hover tracking in `Update`**

The current `Update` method computes `_hoverTile` and calls `HandleKeyboard`/`HandleMouseClick`. Add hover tracking for the inspect window right after the existing hover-tile computation. Replace:

```csharp
        // Hover tile (clamped to map, accounting for the centered map offset)
        int hx = Math.Clamp((mouse.X - ContentX) / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);
```

With:

```csharp
        // Hover tile (clamped to map, accounting for the centered map offset)
        int hx = Math.Clamp((mouse.X - ContentX) / TileSize, 0, state.Map.Width - 1);
        int hy = Math.Clamp(mouse.Y / TileSize, 0, state.Map.Height - 1);
        _hoverTile = (hx, hy);

        _inspectWindow.UpdateHover(mouse.X, mouse.Y);
```

- [ ] **Step 3: Add right-click and outside-click handling for the inspect window**

Add a new method and call it from `Update`. First, add the call — replace:

```csharp
        HandleKeyboard(kb);
        HandleMouseClick(mouse);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }
```

With:

```csharp
        _inspectClickConsumed = HandleInspectWindow(mouse);
        HandleKeyboard(kb);
        HandleMouseClick(mouse);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }
```

This method needs the viewport's pixel dimensions (not the map's tile dimensions) to pass into `EnemyInspectWindow.Open`, so it can clamp the window inside the actual game window. `Update` currently only receives `viewportWidth` as a parameter — add fields to store both dimensions each frame, plus a field to record whether the inspect window consumed the current frame's click (used in Step 4). Add three fields next to `_mapOffsetX`:

```csharp
    private int _mapOffsetX;
    private int _lastViewportWidth;
    private int _lastViewportHeight;
    private bool _inspectClickConsumed;
```

And change `Update`'s signature and the top of its body. Replace:

```csharp
    public void Update(MouseState mouse, int viewportWidth)
    {
        int mapWidth = state.Map.Width * TileSize;
        _mapOffsetX = Math.Max(0, (viewportWidth - mapWidth) / 2);
```

With:

```csharp
    public void Update(MouseState mouse, int viewportWidth, int viewportHeight)
    {
        int mapWidth = state.Map.Width * TileSize;
        _mapOffsetX = Math.Max(0, (viewportWidth - mapWidth) / 2);
        _lastViewportWidth = viewportWidth;
        _lastViewportHeight = viewportHeight;
```

Now add the `HandleInspectWindow` method itself. It returns `true` when it has consumed the current click (open, close, re-anchor, or a click on an already-open window) so `HandleMouseClick` can skip its own handling for that same click — this is what makes even the specific click that closes the window not fall through. Add this new method right before `HandleKeyboard`:

```csharp
    private bool HandleInspectWindow(MouseState mouse)
    {
        bool wasOpen = _inspectWindow.IsOpen;
        bool leftJustClicked = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        bool rightJustClicked = mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

        if (rightJustClicked)
        {
            int gx = (mouse.X - ContentX) / TileSize;
            int gy = mouse.Y / TileSize;
            var candidate = gx >= 0 && gx < state.Map.Width && gy >= 0 && gy < state.Map.Height
                ? state.GetCreatureAt(gx, gy)
                : null;

            if (candidate != null && state.Enemies.Contains(candidate))
                _inspectWindow.Open(candidate, (mouse.X, mouse.Y), _lastViewportWidth, _lastViewportHeight);
            else
                _inspectWindow.Close();

            return true; // right-click is always consumed, whether it opened, re-anchored, or closed
        }

        if (leftJustClicked && wasOpen)
        {
            if (!_inspectWindow.Contains(mouse.X, mouse.Y))
                _inspectWindow.Close();
            return true; // consumed whenever the window was open at the start of this click — even
                          // the click that closes it — so it never also moves/attacks/clicks the hotbar
        }

        return false;
    }
```

- [ ] **Step 4: Suppress normal click handling for any click the inspect window consumed**

Replace the start of `HandleMouseClick`:

```csharp
    private void HandleMouseClick(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _prevMouse.LeftButton != ButtonState.Released)
            return;

        int? clickedSlot = HotbarSlotAt(mouse.X, mouse.Y);
```

With:

```csharp
    private void HandleMouseClick(MouseState mouse)
    {
        if (mouse.LeftButton != ButtonState.Pressed || _prevMouse.LeftButton != ButtonState.Released)
            return;

        if (_inspectClickConsumed) return; // HandleInspectWindow already handled this click

        int? clickedSlot = HotbarSlotAt(mouse.X, mouse.Y);
```

`_inspectClickConsumed` is set once per frame from `HandleInspectWindow`'s return value in `Update` (Step 3 above) — already declared as a field in that step, no further changes needed here.

- [ ] **Step 5: Add Esc-to-close**

In `HandleKeyboard`, replace:

```csharp
        // Esc: in Attack mode, return to Movement.
        if (JustPressed(kb, Keys.Escape) && _mode == Mode.Attack)
        {
            EnterMovementMode();
            return;
        }
```

With:

```csharp
        // Esc: close the inspect window if open, otherwise (in Attack mode) return to Movement.
        if (JustPressed(kb, Keys.Escape))
        {
            if (_inspectWindow.IsOpen)
            {
                _inspectWindow.Close();
                return;
            }
            if (_mode == Mode.Attack)
            {
                EnterMovementMode();
                return;
            }
        }
```

- [ ] **Step 6: Draw the window last**

Replace:

```csharp
        DrawApPips(sb);
        _combatLog.Draw(sb, pixel, logFont, new Rectangle(LogPanelMargin, 0, LogPanelWidth, 900));
    }
```

With:

```csharp
        DrawApPips(sb);
        _combatLog.Draw(sb, pixel, logFont, new Rectangle(LogPanelMargin, 0, LogPanelWidth, 900));
        _inspectWindow.Draw(sb, pixel, logFont);
    }
```

- [ ] **Step 7: Update `Game1.cs`'s call to `Update`**

`Game1.Update` currently calls `_scene.Update(Mouse.GetState(), GraphicsDevice.Viewport.Width)`. Find and replace in `Game1.cs`:

```csharp
        _scene.Update(Mouse.GetState(), GraphicsDevice.Viewport.Width);
```

With:

```csharp
        _scene.Update(Mouse.GetState(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Run full test suite**

Run: `dotnet test`
Expected: all tests pass, 0 failures

- [ ] **Step 10: Manual verification**

Run: `dotnet run`
Expected: game launches. Right-click the enemy — a small dark window appears near it with a crimson portrait square, the enemy's name, and small boxes for each attack. Hover an attack box — a tooltip appears below the window showing name/damage/accuracy/range (and effect text for Skewer/Quill Spray, which have `Bleed` on-hit). Press Esc, or right-click empty ground, or left-click far outside the window — it closes. Right-click a different enemy while one window is open — it re-anchors to the new enemy.

- [ ] **Step 11: Commit**

```bash
git add Scripts/Rendering/EncounterScene.cs Game1.cs
git commit -m "feat: wire EnemyInspectWindow into EncounterScene right-click and hover input"
```

---

## Self-Review Notes

- **Spec coverage:** right-click open/any-mode (Task 3 Step 3) — close via Esc/right-click-elsewhere/click-outside (Task 3 Steps 3–5) — position anchored near enemy, clamped to the full viewport in pixels rather than the map's tile bounds (Task 2 `Open` takes pixel `viewportWidth`/`viewportHeight`; Task 3 Step 3 stores real pixel dimensions in `_lastViewportWidth`/`_lastViewportHeight` from `Update`'s parameters and passes those, never map tile counts) — portrait+name+attack-row layout (Task 2 `Draw`) — hover tooltip with raw stats + effects via shared `DescribeEffect` (Task 1, Task 2 `FormatTooltip`) — no game-state mutation (confirmed: `EnemyInspectWindow` never touches `EncounterState`/`TurnManager`/`Resolve`) — no new tests for `EncounterScene` wiring, but `EnemyInspectWindow`'s own logic IS tested (Task 2), matching the `CombatLog` precedent. The spec's "any click while open never falls through, even the one that closes it" requirement is satisfied by `HandleInspectWindow` returning `true` based on `wasOpen` (captured before `Close()` runs), not on `IsOpen` re-read afterward — this is the detail that makes the close-click itself get suppressed correctly (Task 3 Step 3).
- **Placeholder scan:** none.
- **Type consistency:** `EnemyInspectWindow.Open`'s third/fourth parameters are pixel dimensions (`int viewportWidth, int viewportHeight`) consistently in Task 2 and Task 3; `EncounterScene.Update`'s new second/third parameters match `Game1.cs`'s `GraphicsDevice.Viewport.Width`/`.Height` call site; `HandleInspectWindow` returns `bool`, consumed via `_inspectClickConsumed` in `HandleMouseClick`, both declared and used consistently across Task 3 Steps 3–4.
