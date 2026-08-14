using RuinGamePDT.Combat;

namespace RuinGamePDT.Creatures;

public class PricklebackGoblin : Monstrosity
{
    public PricklebackGoblin() : base("Prickleback Goblin", 6, 4, 1, 3, 3)
    {
        CombatStats.MovementPoints += 11;
        CombatStats.CritChance += 10;

        Attacks.Add(new Attack("Scratch", 1, 3, 1, 100, new AttackShape(new[] { (0, 0) }), 1));
        Attacks.Add(new Attack("Skewer", 2, 4, 1, 100, new AttackShape(new[] { (0, 0) }), 5,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 3, Chance: 25),
            cooldown: 1));
        Attacks.Add(new Attack("Quill Spray", 0, 1, 1, 100, AttackShape.CircularBurst(radius: 2), range: 0, minRange: 0,
            onHit: new AttackEffect(AttackEffectType.Bleed, [new StatChange(CombatStat.HitPoints, 5, 5)], MinDuration: 1, MaxDuration: 1, Chance: 25),
            cooldown: 3));
    }
}