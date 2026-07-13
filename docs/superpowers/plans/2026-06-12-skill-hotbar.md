# Skill Hotbar Binding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bind Mercenary skills to hotbar slots 8/9/0 — Rush (9), Defensive Stance (0), First Aid (8) — with icons, key input, and self-cast or targeting as appropriate.

**Architecture:** Two file changes only. `Game1.cs` loads three new PNG icons into `_skillIcons`. `EncounterScene.cs` gains key handlers for 8/9/0 (self-cast Rush and Defensive Stance; targeting flow for First Aid) and draws skill icons in hotbar slots 7–9.

**Tech Stack:** C# / .NET 8 / MonoGame / xUnit

---

## File Map

| File | Change |
|------|--------|
| `Game1.cs` | Add Rush, Defensive Stance, First Aid icon entries to `_skillIcons` |
| `Scripts/Rendering/EncounterScene.cs` | Handle keys 8/9/0 for skills; draw skill icons in hotbar slots 7–9 |

---

## Task 1: Load skill icons in `Game1.cs`

**Files:**
- Modify: `Game1.cs:41-46`

Icons already exist at:
- `Content/Icons/Rush.png`
- `Content/Icons/DStance.png`
- `Content/Icons/FirstAid.png`

- [ ] **Step 1: Add three entries to `_skillIcons` in `LoadContent`**

Replace this block:
```csharp
_skillIcons = new Dictionary<string, Texture2D>
{
    { "Punch", LoadTexture("Content/Icons/Punch.png") },
    { "Throw Stone", LoadTexture("Content/Icons/StoneThrow.png") },
    { "Shout", LoadTexture("Content/Icons/Shout.png") }
};
```

With:
```csharp
_skillIcons = new Dictionary<string, Texture2D>
{
    { "Punch", LoadTexture("Content/Icons/Punch.png") },
    { "Throw Stone", LoadTexture("Content/Icons/StoneThrow.png") },
    { "Shout", LoadTexture("Content/Icons/Shout.png") },
    { "Rush", LoadTexture("Content/Icons/Rush.png") },
    { "Defensive Stance", LoadTexture("Content/Icons/DStance.png") },
    { "First Aid", LoadTexture("Content/Icons/FirstAid.png") }
};
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

---

## Task 2: Handle skill keys 8/9/0 in `HandleKeyboard`

**Files:**
- Modify: `Scripts/Rendering/EncounterScene.cs:67-80`

Key mapping:
- `8` → First Aid (index 2 in `merc.Skills`, if it exists) → `EnterAttackMode`
- `9` → Rush (index 0 in `merc.Skills`) → self-cast immediately
- `0` → Defensive Stance (index 1 in `merc.Skills`) → self-cast immediately

Self-cast: resolve the skill against the selected merc's own tile, then return to Movement mode.

- [ ] **Step 1: Add skill key handling after the existing attack key block**

The existing attack key block ends at line 80. Add the following immediately after it (before the closing `}` of `HandleKeyboard`):

```csharp
// Skill keys 8/9/0: activate skills while in Movement or Attack mode.
if (_selected is Mercenary skillUser && (_mode == Mode.Movement || _mode == Mode.Attack))
{
    // Key 9 → Rush (Skills[0]): self-cast
    if (JustPressed(kb, Keys.D9) && skillUser.Skills.Count > 0)
    {
        var skill = skillUser.Skills[0];
        if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
        {
            var pos = state.GetPosition(skillUser);
            _resolver.Resolve(skillUser, skill, pos, state);
            EnterMovementMode();
        }
    }
    // Key 0 → Defensive Stance (Skills[1]): self-cast
    else if (JustPressed(kb, Keys.D0) && skillUser.Skills.Count > 1)
    {
        var skill = skillUser.Skills[1];
        if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
        {
            var pos = state.GetPosition(skillUser);
            _resolver.Resolve(skillUser, skill, pos, state);
            EnterMovementMode();
        }
    }
    // Key 8 → First Aid (Skills[2]): enter targeting mode
    else if (JustPressed(kb, Keys.D8) && skillUser.Skills.Count > 2)
    {
        var skill = skillUser.Skills[2];
        if (state.GetRemainingActionPoints(skillUser) >= skill.ActionPointCost)
        {
            EnterAttackMode(skill);
        }
    }
}
```

- [ ] **Step 2: Build to verify no errors**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`

---

## Task 3: Draw skill icons in hotbar slots 7–9

**Files:**
- Modify: `Scripts/Rendering/EncounterScene.cs:305-317` (`DrawActionBar`)

Slots are 0-indexed. Attacks occupy slots 0–N. Skills go in fixed slots:
- Slot 7 = Rush (Skills[0], key 9)
- Slot 8 = Defensive Stance (Skills[1], key 0)
- Slot 9 = First Aid (Skills[2], key 8)

- [ ] **Step 1: Replace the skill icon drawing block in `DrawActionBar`**

Find this existing block inside `DrawActionBar`:
```csharp
if (_selected is Mercenary merc)
{
    for (int i = 0; i < Math.Min(3, merc.Attacks.Count); i++)
    {
        int x = barX + i * (boxSize + boxGap);
        var rect = new Rectangle(x + borderSize, barY + borderSize, boxSize - borderSize * 2, boxSize - borderSize * 2);
        var attack = merc.Attacks[i];
        if (_skillIcons.TryGetValue(attack.Name, out var icon))
        {
            sb.Draw(icon, rect, Color.White);
        }
    }
}
```

Replace with:
```csharp
if (_selected is Mercenary merc)
{
    // Attacks in slots 0..N-1
    for (int i = 0; i < Math.Min(7, merc.Attacks.Count); i++)
    {
        int x = barX + i * (boxSize + boxGap);
        var rect = new Rectangle(x + borderSize, barY + borderSize, boxSize - borderSize * 2, boxSize - borderSize * 2);
        if (_skillIcons.TryGetValue(merc.Attacks[i].Name, out var icon))
            sb.Draw(icon, rect, Color.White);
    }

    // Skills in fixed slots 7 (Rush/key 9), 8 (Defensive Stance/key 0), 9 (First Aid/key 8)
    int[] skillSlots = { 7, 8, 9 };
    for (int i = 0; i < Math.Min(skillSlots.Length, merc.Skills.Count); i++)
    {
        int slot = skillSlots[i];
        int x = barX + slot * (boxSize + boxGap);
        var rect = new Rectangle(x + borderSize, barY + borderSize, boxSize - borderSize * 2, boxSize - borderSize * 2);
        if (_skillIcons.TryGetValue(merc.Skills[i].Name, out var icon))
            sb.Draw(icon, rect, Color.White);
    }
}
```

- [ ] **Step 2: Build and run all tests**

Run: `dotnet test`
Expected: All 153 tests pass, 0 failures

- [ ] **Step 3: Commit**

```bash
git add Game1.cs Scripts/Rendering/EncounterScene.cs
git commit -m "feat: bind mercenary skills to hotbar slots 8/9/0 with icons"
```
