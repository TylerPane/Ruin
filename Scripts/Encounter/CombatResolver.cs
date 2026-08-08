using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Encounter;

public class CombatResolver(Func<int, int, int> roll)
{
    public List<CombatLogEntry> Resolve(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        var entries = new List<CombatLogEntry>();
        state.SpendActionPoints(attacker, attack.ActionPointCost);
        attacker.StartCooldown(attack);
        int hitCount = roll(attack.MinHits, attack.MaxHits + 1);

        foreach (var (dx, dy) in attack.AttackShape.Offsets)
        {
            int tx = targetTile.X + dx;
            int ty = targetTile.Y + dy;
            var defender = state.GetCreatureAt(tx, ty);
            if (defender == null) continue;

            bool isBuff = attack.MaxDamage == 0;
            if (isBuff && !state.IsAlliedWith(attacker, defender)) continue;

            for (int i = 0; i < hitCount; i++)
            {
                int hitThreshold = 100 - attack.Accuracy + (int)defender.CombatStats.Evasion;
                int hitRoll = roll(1, 101);
                if (!isBuff && hitRoll < hitThreshold)
                {
                    entries.Add(new CombatLogEntry(attacker.Name, attack.Name, defender.Name, WasHit: false, Damage: 0, EffectsApplied: Array.Empty<string>()));
                    continue;
                }

                bool isCrit = attack.AutoCrit;
                if (!isCrit)
                {
                    int critRoll = roll(1, 101);
                    isCrit = critRoll >= 100 - (int)attacker.CombatStats.CritChance;
                }

                int baseDmg = roll(attack.MinDamage, attack.MaxDamage + 1);
                int dmg = baseDmg + (int)attacker.CombatStats.AttackPower - (int)defender.CombatStats.PhysicalDefense;
                if (attack.MaxDamage > 0)       dmg = Math.Max(1, dmg);
                else if (attack.MaxDamage == 0) dmg = 0;
                // MaxDamage < 0: negative dmg is a heal — CurrentHp clamp handles the cap
                if (isCrit) dmg *= 2;

                defender.CurrentHp -= dmg;

                var effects = new List<string>();
                if (attack.OnHit != null)
                {
                    defender.ApplyStatusEffect(attack.OnHit);
                    effects.AddRange(DescribeEffect(attack.OnHit));
                }
                if (isCrit && attack.OnCrit != null)
                {
                    defender.ApplyStatusEffect(attack.OnCrit);
                    effects.AddRange(DescribeEffect(attack.OnCrit));
                }

                entries.Add(new CombatLogEntry(attacker.Name, attack.Name, defender.Name, WasHit: true, Damage: dmg, EffectsApplied: effects));

                if (defender.CurrentHp <= 0)
                {
                    state.RemoveCreature(defender);
                    break;
                }
            }
        }

        return entries;
    }

    public static IEnumerable<string> DescribeEffect(AttackEffect effect)
    {
        foreach (var statChange in effect.Stats)
        {
            yield return effect.Type switch
            {
                AttackEffectType.StatIncrease => $"{statChange.Stat} +",
                AttackEffectType.StatReduction => $"{statChange.Stat} -",
                _ => effect.Type.ToString()
            };
        }
    }

    public bool IsInRange(Creature attacker, Attack attack, (int X, int Y) targetTile, EncounterState state)
    {
        if (!state.IsPlaced(attacker)) return false;
        var pos = state.GetPosition(attacker);
        double distance = Math.Sqrt(Math.Pow(targetTile.X - pos.X, 2) + Math.Pow(targetTile.Y - pos.Y, 2));
        return distance >= attack.MinRange && distance <= attack.Range;
    }
}
