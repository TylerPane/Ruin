using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class CombatResolver(Func<int, int, int> roll)
{
    public void Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        state.SpendActionPoints(attacker, attack.ActionPointCost);
        int hitCount = roll(attack.MinHits, attack.MaxHits + 1);

        foreach (var (dx, dy) in attack.AttackShape.Offsets)
        {
            int tx = targetTile.X + dx;
            int ty = targetTile.Y + dy;
            var defender = state.GetCreatureAt(tx, ty);
            if (defender == null) continue;

            for (int i = 0; i < hitCount; i++)
            {
                int hitThreshold = 100 - attack.Accuracy + (int)defender.CombatStats.Evasion;
                int hitRoll = roll(1, 101);
                if (hitRoll < hitThreshold) continue;

                bool isCrit = attack.AutoCrit;
                if (!isCrit)
                {
                    int critRoll = roll(1, 101);
                    isCrit = critRoll >= 100 - (int)attacker.CombatStats.CritChance;
                }

                int baseDmg = roll(attack.MinDamage, attack.MaxDamage + 1);
                int dmg = Math.Max(1, baseDmg + (int)attacker.CombatStats.AttackPower - (int)defender.CombatStats.PhysicalDefense);
                if (isCrit) dmg *= 2;

                defender.CurrentHp -= dmg;

                if (attack.OnHit != null) defender.ApplyStatusEffect(attack.OnHit);
                if (isCrit && attack.OnCrit != null) defender.ApplyStatusEffect(attack.OnCrit);

                if (defender.CurrentHp <= 0)
                {
                    state.RemoveCreature(defender);
                    break;
                }
            }
        }
    }

    // TODO(distance): switch to Euclidean (sqrt(dx² + dy²)) for circular range
    // shape. Currently square shape — matches the v1 spec's
    // explicit placeholder; replace alongside the EncounterScene targeting
    // overlay's distance calculation.
    public bool IsInRange(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        if (!state.IsPlaced(attacker)) return false;
        var pos = state.GetPosition(attacker);
        int distance = Math.Max(Math.Abs(targetTile.X - pos.X), Math.Abs(targetTile.Y - pos.Y));
        return distance >= attack.MinRange && distance <= attack.Range;
    }
}
