using RuinGamePDT.Creatures;
using RuinGamePDT.Resources;
using RuinGamePDT.World;

namespace RuinGamePDT.Encounter;

public class EncounterState(EncounterMap map)
{
    public EncounterMap Map { get; } = map;
    public List<Creature> Mercenaries { get; } = [];
    public List<Creature> Enemies { get; } = [];

    private readonly Dictionary<(int X, int Y), Creature> _occupancy = new();
    private readonly Dictionary<Creature, (int X, int Y)> _positions = new();
    private readonly Dictionary<Creature, float> _remainingMovement = new();
    private readonly Dictionary<Creature, int> _remainingActionPoints = new();

    public bool PlaceCreature(Creature creature, int x, int y)
    {
        if (x < 0 || x >= Map.Width || y < 0 || y >= Map.Height) return false;
        if (Map.GetTile(x, y) == EncounterTileType.Obstacle) return false;
        if (_occupancy.ContainsKey((x, y))) return false;
        _occupancy[(x, y)] = creature;
        _positions[creature] = (x, y);
        _remainingMovement[creature] = creature.CombatStats.MovementPoints;
        _remainingActionPoints[creature] = creature.CombatStats.ActionPoints;
        return true;
    }

    public Creature? GetCreatureAt(int x, int y) =>
        _occupancy.TryGetValue((x, y), out var c) ? c : null;

    public bool IsOccupied(int x, int y) => _occupancy.ContainsKey((x, y));

    public bool IsAlliedWith(Creature a, Creature b) =>
        (Mercenaries.Contains(a) && Mercenaries.Contains(b)) ||
        (Enemies.Contains(a) && Enemies.Contains(b));

    public bool IsPlaced(Creature creature) => _positions.ContainsKey(creature);

    // Precondition: creature must have been placed via PlaceCreature
    public (int X, int Y) GetPosition(Creature creature) => _positions[creature];

    // Precondition: creature must have been placed via PlaceCreature
    public float GetRemainingMovement(Creature creature) => _remainingMovement[creature];

    public void ResetMovement(Creature creature)
    {
        if (!_positions.ContainsKey(creature)) return;
        _remainingMovement[creature] = creature.CombatStats.MovementPoints;
    }

    public void AddMovement(Creature creature, float amount)
    {
        if (!_remainingMovement.ContainsKey(creature)) return;
        _remainingMovement[creature] += amount;
    }

    public bool MoveCreature(Creature creature, int toX, int toY)
    {
        if (!_positions.ContainsKey(creature)) return false;
        if (toX < 0 || toX >= Map.Width || toY < 0 || toY >= Map.Height) return false;
        var reachable = MovementValidator.GetReachableTiles(creature, this);
        if (!reachable.ContainsKey((toX, toY))) return false;
        var from = _positions[creature];
        _occupancy.Remove(from);
        _occupancy[(toX, toY)] = creature;
        _positions[creature] = (toX, toY);
        _remainingMovement[creature] -= reachable[(toX, toY)];
        return true;
    }

    // Precondition: creature must have been placed via PlaceCreature
    public int GetRemainingActionPoints(Creature creature) => _remainingActionPoints[creature];

    public void SpendActionPoints(Creature creature, int amount)
    {
        if (!_remainingActionPoints.ContainsKey(creature)) return;
        _remainingActionPoints[creature] = Math.Max(0, _remainingActionPoints[creature] - amount);
    }

    public void ResetActionPoints(Creature creature)
    {
        if (!_positions.ContainsKey(creature)) return;
        _remainingActionPoints[creature] = creature.CombatStats.ActionPoints;
    }

    public void RemoveCreature(Creature creature)
    {
        if (_positions.TryGetValue(creature, out var pos))
            _occupancy.Remove(pos);
        _positions.Remove(creature);
        _remainingMovement.Remove(creature);
        _remainingActionPoints.Remove(creature);
        Mercenaries.Remove(creature);
        Enemies.Remove(creature);
    }
}
