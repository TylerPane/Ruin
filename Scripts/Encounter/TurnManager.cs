using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class TurnManager(EncounterState state)
{
    private readonly List<Creature> _turnOrder = [];
    private readonly HashSet<Creature> _hasMoved = [];
    private int _currentTurnIndex = -1;

    public Creature? CurrentCreature => _currentTurnIndex >= 0 && _currentTurnIndex < _turnOrder.Count
        ? _turnOrder[_currentTurnIndex]
        : null;

    public void StartEncounter()
    {
        _hasMoved.Clear();
        _turnOrder.Clear();
        _currentTurnIndex = -1;

        BuildInitiativeOrder();
        AdvanceToNextTurn();
    }

    public bool CanMove(Creature creature) =>
        creature == CurrentCreature
        && !_hasMoved.Contains(creature)
        && state.IsPlaced(creature);

    public void EndCreatureTurn(Creature creature)
    {
        if (creature != CurrentCreature) return;

        _hasMoved.Add(creature);
        AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn()
    {
        while (true)
        {
            _currentTurnIndex++;

            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _turnOrder.Clear();
                _hasMoved.Clear();
                _currentTurnIndex = -1;
                BuildInitiativeOrder();
                _currentTurnIndex = 0;
            }

            if (_currentTurnIndex >= _turnOrder.Count) return;

            var creature = _turnOrder[_currentTurnIndex];
            if (!state.IsPlaced(creature))
            {
                continue;
            }

            state.ResetMovement(creature);
            state.ResetActionPoints(creature);
            creature.TickStatusEffects();
            break;
        }
    }

    public EncounterResult CheckEndCondition()
    {
        if (state.Enemies.Count == 0) return EncounterResult.Victory;
        if (state.Mercenaries.Count == 0) return EncounterResult.Defeat;
        return EncounterResult.Ongoing;
    }

    private void BuildInitiativeOrder()
    {
        var allCreatures = state.Mercenaries.Concat(state.Enemies).ToList();

        var initiatives = allCreatures.Select(c => (
            creature: c,
            speed: c.CombatStats.Speed,
            tiebreaker: Random.Shared.Next()
        )).ToList();

        _turnOrder.Clear();
        _turnOrder.AddRange(
            initiatives
                .OrderByDescending(x => x.speed)
                .ThenByDescending(x => x.tiebreaker)
                .Select(x => x.creature)
        );
    }
}
