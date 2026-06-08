using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Resources;

namespace RuinGamePDT.Weapons;

public class Unarmed : Weapon
{
    public Unarmed() : base(WeaponType.Unarmed)
    {
        Attacks.Add(new Attack(
            name: "Punch",
            minDamage: 1,
            maxDamage: 2,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 1
        ));

        Attacks.Add(new Attack(
            name: "Throw Stone",
            minDamage: 1,
            maxDamage: 2,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 5
        ));

        Attacks.Add(new Attack(
            name: "Shout",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(BurstOffsets(radius: 4)),
            range: 4,
            reaction: null,
            onHit: new AttackEffect(AttackEffectType.StatIncrease, CombatStat.PhysicalDefense, 2, 2, 3, 3),
            onCrit: null
        ));
    }

    private static IEnumerable<(int, int)> BurstOffsets(int radius)
    {
        var offsets = new List<(int, int)>();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (Math.Abs(x) + Math.Abs(y) <= radius)
                    offsets.Add((x, y));
        return offsets;
    }
}
