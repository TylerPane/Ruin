using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public class Skill(
    string name,
    int minDamage,
    int maxDamage,
    int actionPointCost,
    int accuracy,
    AttackShape attackShape,
    int range,
    Reaction? reaction = null,
    AttackEffect? onHit = null,
    AttackEffect? onCrit = null,
    int minRange = 0,
    bool autoCrit = false,
    int minHits = 1,
    int maxHits = 1)
    : Attack(name, minDamage, maxDamage, actionPointCost, accuracy, attackShape, range,
             reaction, onHit, onCrit, minRange, autoCrit, minHits, maxHits);
