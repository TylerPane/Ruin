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
        // + NameLineHeight (18) + Padding * 2 (16) = 82; the first box starts at bounds.X +
        // Padding (8), spanning [8, 32) horizontally and [82, 106) vertically. (12, 90) sits
        // inside both ranges.
        int? hit = window.HotboxAt(12, 90);

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
