namespace RuinGamePDT.Combat;

public record CombatLogEntry(
    string AttackerName,
    string AttackName,
    string TargetName,
    bool WasHit,
    int Damage,
    IReadOnlyList<string> EffectsApplied
);
