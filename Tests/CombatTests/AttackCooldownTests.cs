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
