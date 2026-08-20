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

    }

    // Not currently added to Attacks — doesn't fit the unarmed kit right now,
    // but kept defined for future use.
    public static Attack CreateShout() => new(
        name: "Shout",
        minDamage: 0,
        maxDamage: 0,
        actionPointCost: 1,
        accuracy: 100,
        attackShape: AttackShape.CircularBurst(radius: 4),
        range: 0,
        reaction: null,
        onHit: new AttackEffect(AttackEffectType.StatIncrease, [new StatChange(CombatStat.PhysicalDefense, 2, 2)], MinDuration: 3, MaxDuration: 3),
        onCrit: null,
        minRange: 0
    );
}
