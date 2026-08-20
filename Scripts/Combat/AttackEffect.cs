using RuinGamePDT.Creatures;

namespace RuinGamePDT.Combat;

public record StatChange(CombatStat Stat, int MinAmount, int MaxAmount);

public record AttackEffect(
    AttackEffectType Type,
    IReadOnlyList<StatChange> Stats,
    int MinDuration,
    int MaxDuration,
    int Chance = 100
);
