using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class TurnManager(EncounterState state)
{
    public int CurrentPhase { get; private set; }

    private readonly HashSet<Creature> _movedThisTurn = [];
    private List<Creature> _phaseQueue = [];
    private readonly HashSet<Creature> _phaseSet = [];

    public void StartEncounter()
    {
        CurrentPhase = 1;
        _movedThisTurn.Clear();
        //TODO: replace the below with initiative tracker that goes off creature speed.
        SetPhaseQueue(BuildPhaseQueue(1));
        RefreshPhaseQueue();
    }

    public bool CanMove(Creature creature) =>
        _phaseSet.Contains(creature)
        && !_movedThisTurn.Contains(creature)
        && state.IsPlaced(creature);

    public void EndCreatureTurn(Creature creature)
    {
        _movedThisTurn.Add(creature);
        if (_phaseQueue.All(_movedThisTurn.Contains))
            AdvancePhase();
    }

    public void AdvancePhase()
    {
        if (CurrentPhase == 0) return;
        CurrentPhase = CurrentPhase == 4 ? 1 : CurrentPhase + 1;
        _movedThisTurn.Clear();
        SetPhaseQueue(BuildPhaseQueue(CurrentPhase));
        RefreshPhaseQueue();
    }

    private void RefreshPhaseQueue()
    {
        foreach (var c in _phaseQueue)
        {
            state.ResetMovement(c);
            state.ResetActionPoints(c);
            c.TickStatusEffects();
        }
    }

    public EncounterResult CheckEndCondition()
    {
        if (state.Enemies.Count == 0) return EncounterResult.Victory;
        if (state.Mercenaries.Count == 0) return EncounterResult.Defeat;
        return EncounterResult.Ongoing;
    }

    private void SetPhaseQueue(List<Creature> queue)
    {
        _phaseQueue = queue;
        _phaseSet.Clear();
        _phaseSet.UnionWith(queue);
    }

    private List<Creature> BuildPhaseQueue(int phase)
    {
        //TODO: Replace all of this with initiaive tracker logic that sorts by creature speed.        
        int mercHalf = (int)Math.Ceiling(state.Mercenaries.Count / 2.0);
        int enemyHalf = (int)Math.Ceiling(state.Enemies.Count / 2.0);
        return phase switch
        {
            1 => state.Mercenaries.Take(mercHalf).ToList(),
            2 => state.Enemies.Take(enemyHalf).ToList(),
            3 => state.Mercenaries.Skip(mercHalf).ToList(),
            4 => state.Enemies.Skip(enemyHalf).ToList(),
            _ => []
        };
    }
}
