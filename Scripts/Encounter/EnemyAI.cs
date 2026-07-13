using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class EnemyAI(CombatResolver resolver)
{
    public List<CombatLogEntry> TakeTurn(Creature enemy, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        if (!state.IsPlaced(enemy)) return entries;
        if (state.Mercenaries.Count == 0) return entries;

        var target = FindNearestMerc(enemy, state);
        if (target == null) return entries;

        MoveToward(enemy, target, state);

        while (true)
        {
            if (!state.IsPlaced(enemy)) return entries;
            if (state.Mercenaries.Count == 0) return entries;

            var pick = ChooseBestAttack(enemy, state);
            if (pick == null) return entries;

            entries.AddRange(resolver.Resolve(enemy, pick.Value.attack, pick.Value.targetTile, state));
        }
    }

    private static Creature? FindNearestMerc(Creature enemy, EncounterState state)
    {
        var pos = state.GetPosition(enemy);
        Creature? best = null;
        int bestDist = int.MaxValue;
        foreach (var m in state.Mercenaries)
        {
            if (!state.IsPlaced(m)) continue;
            var mp = state.GetPosition(m);
            int d = Chebyshev(pos, mp);
            if (d < bestDist)
            {
                best = m;
                bestDist = d;
            }
        }
        return best;
    }

    private static void MoveToward(Creature enemy, Creature target, EncounterState state)
    {
        var enemyPos = state.GetPosition(enemy);
        var targetPos = state.GetPosition(target);

        var reachable = MovementValidator.GetReachableTiles(enemy, state);
        (int X, int Y) bestTile = enemyPos;
        int bestDist = Chebyshev(enemyPos, targetPos);

        foreach (var tile in reachable.Keys)
        {
            int d = Chebyshev(tile, targetPos);
            if (d < bestDist)
            {
                bestDist = d;
                bestTile = tile;
            }
        }

        if (bestTile != enemyPos)
            state.MoveCreature(enemy, bestTile.X, bestTile.Y);
    }

    private (Attack attack, (int X, int Y) targetTile)? ChooseBestAttack(Creature enemy, EncounterState state)
    {
        (Attack attack, (int X, int Y) tile, float avgDmg)? best = null;

        foreach (var attack in enemy.Attacks)
        {
            if (state.GetRemainingActionPoints(enemy) < attack.ActionPointCost) continue;

            var tile = BestTargetTile(enemy, attack, state);
            if (tile == null) continue;

            float avg = (attack.MinDamage + attack.MaxDamage) / 2f;
            if (best == null || avg > best.Value.avgDmg)
                best = (attack, tile.Value, avg);
        }

        return best == null ? null : (best.Value.attack, best.Value.tile);
    }

    private (int X, int Y)? BestTargetTile(Creature enemy, Attack attack, EncounterState state)
    {
        bool singleTarget = IsSingleTarget(attack);
        var offsets = attack.AttackShape.Offsets.ToList();
        var width = state.Map.Width;
        var height = state.Map.Height;

        if (singleTarget)
        {
            // Pick the in-range mercenary tile with the lowest current HP (focus-fire).
            Creature? bestMerc = null;
            (int X, int Y) bestTile = (0, 0);
            float bestHp = float.MaxValue;

            foreach (var m in state.Mercenaries)
            {
                if (!state.IsPlaced(m)) continue;
                var mp = state.GetPosition(m);
                if (!resolver.IsInRange(enemy, attack, mp, state)) continue;
                if (m.CurrentHp < bestHp)
                {
                    bestHp = m.CurrentHp;
                    bestMerc = m;
                    bestTile = mp;
                }
            }
            return bestMerc == null ? null : bestTile;
        }

        // AOE — pick the in-range tile whose offsets catch the most mercs.
        (int X, int Y)? bestAoeTile = null;
        int bestCount = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (!resolver.IsInRange(enemy, attack, (x, y), state)) continue;

            int count = 0;
            foreach (var (dx, dy) in offsets)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || tx >= width || ty < 0 || ty >= height) continue;
                var c = state.GetCreatureAt(tx, ty);
                if (c != null && state.Mercenaries.Contains(c)) count++;
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestAoeTile = (x, y);
            }
        }
        return bestAoeTile;
    }

    private static bool IsSingleTarget(Attack attack)
    {
        var offsets = attack.AttackShape.Offsets.ToList();
        return offsets.Count == 1 && offsets[0] == (0, 0);
    }

    private static int Chebyshev((int X, int Y) a, (int X, int Y) b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
