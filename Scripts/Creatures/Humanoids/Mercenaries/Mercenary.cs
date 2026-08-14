using RuinGamePDT.Combat;
using RuinGamePDT.Party;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Creatures;

public class Mercenary : Creature
{
    protected override int StatCap => 10;

    public Mercenary(int agility = 1, int focus = 1, int mind = 1, int strength = 1, int stamina = 1)
        : base("Mercenary", agility, focus, mind, strength, stamina)
    {
        EquippedWeapon = new Unarmed();
        Attacks.AddRange(EquippedWeapon.Attacks);

        Skills.Add(new Skill(
            name: "Defensive Stance",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 0,
            onHit: new AttackEffect(
                AttackEffectType.StatIncrease,
                new[]
                {
                    new StatChange(CombatStat.Evasion, 5, 15),
                    new StatChange(CombatStat.PhysicalDefense, 5, 15)
                },
                MinDuration: 1,
                MaxDuration: 1)
        ));

        if (BaseStats.Mind >= 3)
        {
            Skills.Add(new Skill(
                name: "First Aid",
                minDamage: -BaseStats.Mind,
                maxDamage: -1,
                actionPointCost: 2,
                accuracy: 100,
                attackShape: new AttackShape(new[] { (0, 0) }),
                range: 1
            ));
        }
    }

    protected Mercenary(string name, int agility, int focus, int mind, int strength, int stamina)
        : base(name, agility, focus, mind, strength, stamina) { }

    public void EquipWeapon(Weapon weapon, Banner banner)
    {
        if (EquippedWeapon is not null and not Unarmed)
            banner.Inventory.Add(EquippedWeapon);
        banner.Inventory.Remove(weapon);

        foreach (var attack in EquippedWeapon!.Attacks)
            Attacks.Remove(attack);

        EquippedWeapon = weapon;
        Attacks.AddRange(weapon.Attacks);
    }

    public static Mercenary CreateRandom() => new(
        agility:  Random.Shared.Next(1, 6),
        focus:    Random.Shared.Next(1, 6),
        mind:     Random.Shared.Next(1, 6),
        strength: Random.Shared.Next(1, 6),
        stamina:  Random.Shared.Next(1, 6)
    );

    // Not currently granted to mercenaries — reserved for a future bonus ability.
    public Skill CreateRush()
    {
        int mpHalf = CombatStats.MovementPoints / 2;
        return new Skill(
            name: "Rush",
            minDamage: 0,
            maxDamage: 0,
            actionPointCost: 1,
            accuracy: 100,
            attackShape: new AttackShape(new[] { (0, 0) }),
            range: 0,
            onHit: new AttackEffect(AttackEffectType.StatIncrease,
                new[] { new StatChange(CombatStat.MovementPoints, mpHalf, mpHalf) },
                MinDuration: 1, MaxDuration: 1)
        );
    }
}
