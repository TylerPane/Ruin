using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.Resources;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class EncounterStateTests
{
    [Fact]
    public void PlaceCreature_ReturnsTrue_WhenTileIsEmpty()
    {
        var map = new EncounterMap(10, 10);
        var state = new EncounterState(map);
        var merc = new Mercenary();
        Assert.True(state.PlaceCreature(merc, 0, 0));
    }

    [Fact]
    public void PlaceCreature_ReturnsFalse_WhenTileIsObstacle()
    {
        var map = new EncounterMap(10, 10);
        map.SetTile(2, 2, EncounterTileType.Obstacle);
        var state = new EncounterState(map);
        Assert.False(state.PlaceCreature(new Mercenary(), 2, 2));
    }

    [Fact]
    public void PlaceCreature_ReturnsFalse_WhenTileIsOccupied()
    {
        var map = new EncounterMap(10, 10);
        var state = new EncounterState(map);
        state.PlaceCreature(new Mercenary(), 3, 3);
        Assert.False(state.PlaceCreature(new Mercenary(), 3, 3));
    }

    [Fact]
    public void GetCreatureAt_ReturnsNull_WhenTileIsEmpty()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        Assert.Null(state.GetCreatureAt(0, 0));
    }

    [Fact]
    public void GetCreatureAt_ReturnsCreature_AfterPlacement()
    {
        var map = new EncounterMap(10, 10);
        var state = new EncounterState(map);
        var merc = new Mercenary();
        state.PlaceCreature(merc, 1, 1);
        Assert.Equal(merc, state.GetCreatureAt(1, 1));
    }

    [Fact]
    public void IsOccupied_ReturnsFalse_WhenEmpty()
    {
        Assert.False(new EncounterState(new EncounterMap(10, 10)).IsOccupied(0, 0));
    }

    [Fact]
    public void IsOccupied_ReturnsTrue_AfterPlacement()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        state.PlaceCreature(new Mercenary(), 4, 4);
        Assert.True(state.IsOccupied(4, 4));
    }

    [Fact]
    public void GetPosition_ReturnsPlacedCoordinates()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary();
        state.PlaceCreature(merc, 3, 7);
        Assert.Equal((3, 7), state.GetPosition(merc));
    }

    [Fact]
    public void GetRemainingMovement_EqualsMovementPoints_AfterPlacement()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(stamina: 4); // MovementPoints = 4 + 3 = 7
        state.PlaceCreature(merc, 0, 0);
        Assert.Equal(7f, state.GetRemainingMovement(merc));
    }

    [Fact]
    public void MoveCreature_ReturnsTrue_AndUpdatesBothDictionaries()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(stamina: 4); // 5 MP
        state.PlaceCreature(merc, 0, 0);
        Assert.True(state.MoveCreature(merc, 1, 0));
        Assert.Equal(merc, state.GetCreatureAt(1, 0));
        Assert.Null(state.GetCreatureAt(0, 0));
        Assert.Equal((1, 0), state.GetPosition(merc));
    }

    [Fact]
    public void MoveCreature_DeductsCorrectMovementCost()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(stamina: 4); // MovementPoints = 4 + 3 = 7
        state.PlaceCreature(merc, 0, 0);
        state.MoveCreature(merc, 2, 0); // 2 steps
        Assert.Equal(5f, state.GetRemainingMovement(merc));
    }

    [Fact]
    public void MoveCreature_ReturnsFalse_WhenDestinationIsOccupied()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc1 = new Mercenary(stamina: 4);
        var merc2 = new Mercenary(stamina: 4);
        state.PlaceCreature(merc1, 0, 0);
        state.PlaceCreature(merc2, 1, 0);
        Assert.False(state.MoveCreature(merc1, 1, 0));
    }

    [Fact]
    public void MoveCreature_ReturnsFalse_WhenDestinationIsObstacle()
    {
        var map = new EncounterMap(10, 10);
        map.SetTile(1, 0, EncounterTileType.Obstacle);
        var state = new EncounterState(map);
        var merc = new Mercenary(stamina: 4);
        state.PlaceCreature(merc, 0, 0);
        Assert.False(state.MoveCreature(merc, 1, 0));
    }

    [Fact]
    public void MoveCreature_ReturnsFalse_WhenDestinationOutOfMovementRange()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(stamina: 0); // 3 MP
        state.PlaceCreature(merc, 0, 0);
        Assert.False(state.MoveCreature(merc, 0, 9)); // 9 steps away
    }

    [Fact]
    public void MoveCreature_ReturnsFalse_WhenCreatureNotPlaced()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary();
        Assert.False(state.MoveCreature(merc, 1, 0));
    }

    [Fact]
    public void GetRemainingActionPoints_EqualsActionPoints_AfterPlacement()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(agility: 5); // ActionPoints = 5/2 + 1 = 3
        state.PlaceCreature(merc, 0, 0);
        Assert.Equal(3, state.GetRemainingActionPoints(merc));
    }

    [Fact]
    public void SpendActionPoints_DeductsFromRemaining()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(agility: 5); // 3 AP
        state.PlaceCreature(merc, 0, 0);
        state.SpendActionPoints(merc, 2);
        Assert.Equal(1, state.GetRemainingActionPoints(merc));
    }

    [Fact]
    public void ResetActionPoints_RestoresToMax()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary(agility: 5); // 3 AP
        state.PlaceCreature(merc, 0, 0);
        state.SpendActionPoints(merc, 3);
        Assert.Equal(0, state.GetRemainingActionPoints(merc));
        state.ResetActionPoints(merc);
        Assert.Equal(3, state.GetRemainingActionPoints(merc));
    }

    [Fact]
    public void RemoveCreature_ClearsAllStateForThatCreature()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary();
        state.Mercenaries.Add(merc);
        state.PlaceCreature(merc, 4, 4);

        state.RemoveCreature(merc);

        Assert.Null(state.GetCreatureAt(4, 4));
        Assert.False(state.IsOccupied(4, 4));
        Assert.DoesNotContain(merc, state.Mercenaries);
    }

    [Fact]
    public void RemoveCreature_RemovesFromEnemyList()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var enemy = new PricklebackGoblin();
        state.Enemies.Add(enemy);
        state.PlaceCreature(enemy, 2, 2);

        state.RemoveCreature(enemy);

        Assert.DoesNotContain(enemy, state.Enemies);
        Assert.Null(state.GetCreatureAt(2, 2));
    }

    [Fact]
    public void RemoveCreature_IsSafeForUnplacedCreature()
    {
        var state = new EncounterState(new EncounterMap(10, 10));
        var merc = new Mercenary();
        // Should not throw
        state.RemoveCreature(merc);
    }
}
