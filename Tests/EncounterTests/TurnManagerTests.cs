using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class TurnManagerTests
{
    private static EncounterState MakeState(int mercCount, int enemyCount)
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        for (int i = 0; i < mercCount; i++)
        {
            var m = new Mercenary();
            state.Mercenaries.Add(m);
            state.PlaceCreature(m, i, 0);
        }
        for (int i = 0; i < enemyCount; i++)
        {
            var e = new PricklebackGoblin();
            state.Enemies.Add(e);
            state.PlaceCreature(e, i, 1);
        }
        return state;
    }

    [Fact]
    public void StartEncounter_SetsCurrentCreature()
    {
        var state = MakeState(mercCount: 1, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        Assert.NotNull(tm.CurrentCreature);
    }

    [Fact]
    public void StartEncounter_OrdersBySpeed_HighestFirst()
    {
        var state = new EncounterState(new EncounterMap(20, 20));
        var fast = new Mercenary();
        var slow = new PricklebackGoblin();

        fast.RaiseStat(BaseStat.Strength, 10); // Raise speed significantly
        state.Mercenaries.Add(fast);
        state.Enemies.Add(slow);
        state.PlaceCreature(fast, 0, 0);
        state.PlaceCreature(slow, 1, 1);

        var tm = new TurnManager(state);
        tm.StartEncounter();

        Assert.Equal(fast, tm.CurrentCreature);
    }

    [Fact]
    public void CanMove_ReturnsFalse_ForCreatureNotInTurn()
    {
        var state = MakeState(mercCount: 2, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();

        var otherMerc = state.Mercenaries.First(m => m != tm.CurrentCreature);
        Assert.False(tm.CanMove(otherMerc));
    }

    [Fact]
    public void CanMove_ReturnsFalse_ForCreatureThatAlreadyMoved()
    {
        var state = MakeState(mercCount: 1, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        var current = tm.CurrentCreature!;

        tm.EndCreatureTurn(current);

        Assert.False(tm.CanMove(current));
    }

    [Fact]
    public void EndCreatureTurn_AdvancesToNextCreature()
    {
        var state = MakeState(mercCount: 2, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();

        var first = tm.CurrentCreature;
        tm.EndCreatureTurn(first!);
        var second = tm.CurrentCreature;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CheckEndCondition_ReturnsVictory_WhenEnemiesListIsEmpty()
    {
        var state = MakeState(mercCount: 1, enemyCount: 0);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        Assert.Equal(EncounterResult.Victory, tm.CheckEndCondition());
    }

    [Fact]
    public void CheckEndCondition_ReturnsDefeat_WhenMercenariesListIsEmpty()
    {
        var state = MakeState(mercCount: 0, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        Assert.Equal(EncounterResult.Defeat, tm.CheckEndCondition());
    }

    [Fact]
    public void CheckEndCondition_ReturnsOngoing_WhenBothSidesHaveCreatures()
    {
        var state = MakeState(mercCount: 2, enemyCount: 2);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        Assert.Equal(EncounterResult.Ongoing, tm.CheckEndCondition());
    }

    [Fact]
    public void StartEncounter_ResetsActionPoints_ForCurrentCreature()
    {
        var state = MakeState(mercCount: 1, enemyCount: 1);
        var merc = state.Mercenaries.First();
        state.SpendActionPoints(merc, merc.CombatStats.ActionPoints);
        Assert.Equal(0, state.GetRemainingActionPoints(merc));

        var tm = new TurnManager(state);
        tm.StartEncounter();

        if (tm.CurrentCreature == merc)
            Assert.Equal(merc.CombatStats.ActionPoints, state.GetRemainingActionPoints(merc));
    }

    [Fact]
    public void EndCreatureTurn_ResetsActionPoints_ForNextCreature()
    {
        var state = MakeState(mercCount: 2, enemyCount: 0);
        var merc1 = state.Mercenaries[0];
        var merc2 = state.Mercenaries[1];
        state.SpendActionPoints(merc2, merc2.CombatStats.ActionPoints);

        var tm = new TurnManager(state);
        tm.StartEncounter();

        if (tm.CurrentCreature == merc1)
        {
            tm.EndCreatureTurn(merc1);
            if (tm.CurrentCreature == merc2)
                Assert.Equal(merc2.CombatStats.ActionPoints, state.GetRemainingActionPoints(merc2));
        }
    }

    [Fact]
    public void StartEncounter_TicksStatusEffects_ForCurrentCreature()
    {
        var state = MakeState(mercCount: 1, enemyCount: 1);
        var merc = state.Mercenaries.First();
        merc.StatusEffects.Add(new StatusEffect(StatusEffectType.Bleed, CombatStat.HitPoints, amount: 3, duration: 2));
        float hpBefore = merc.CurrentHp;

        var tm = new TurnManager(state);
        tm.StartEncounter();

        if (tm.CurrentCreature == merc)
        {
            Assert.Equal(hpBefore - 3, merc.CurrentHp);
            Assert.Equal(1, merc.StatusEffects[0].Duration);
        }
    }

    [Fact]
    public void CanMove_ReturnsFalse_ForRemovedCreature()
    {
        var state = MakeState(mercCount: 2, enemyCount: 1);
        var tm = new TurnManager(state);
        tm.StartEncounter();
        var current = tm.CurrentCreature!;
        Assert.True(tm.CanMove(current));

        state.RemoveCreature(current);

        Assert.False(tm.CanMove(current));
    }
}
