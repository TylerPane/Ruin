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
