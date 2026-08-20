using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public class Attack
{
    public string Name { get; }
    public int MinDamage { get; }
    public int MaxDamage { get; }
    public int ActionPointCost { get; }
    public int Accuracy { get; }
    public AttackShape AttackShape { get; }
    public int Range { get; }
    public int MinRange { get; }
    public bool AutoCrit { get; }
    public int MinHits { get; }
    public int MaxHits { get; }
    public int Cooldown { get; }
    public Reaction? Reaction { get; }
    public AttackEffect? OnHit { get; }
    public AttackEffect? OnCrit { get; }

    public Attack(string name, int minDamage, int maxDamage, int actionPointCost, int accuracy, AttackShape attackShape, int range,
        Reaction? reaction = null, AttackEffect? onHit = null, AttackEffect? onCrit = null,
        int minRange = 0, bool autoCrit = false, int minHits = 1, int maxHits = 1, int cooldown = 0)
    {
        Name = name;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        ActionPointCost = actionPointCost;
        Accuracy = accuracy;
        AttackShape = attackShape;
        Range = range;
        MinRange = minRange;
        AutoCrit = autoCrit;
        MinHits = minHits;
        MaxHits = maxHits;
        Cooldown = cooldown;
        Reaction = reaction;
        OnHit = onHit;
        OnCrit = onCrit;
    }
}
