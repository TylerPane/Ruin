using RuinGamePDT.Combat;
using RuinGamePDT.Weapons;

namespace RuinGamePDT.Creatures;

public enum BaseStat { Agility, Focus, Mind, Strength, Stamina }

public enum CombatStat
{
    AbilityCooldown, Evasion, ActionPoints,
    Accuracy, CritChance, Perception,
    ManaPool, MagicDamage, MagicDefense,
    AttackPower, CritDamageBonus, Speed,
    HitPoints, MovementPoints, PhysicalDefense,
}

public class BaseStats(int agility, int focus, int mind, int strength, int stamina)
{
    public int Agility { get; set; } = agility;
    public int Focus { get; set; } = focus;
    public int Mind { get; set; } = mind;
    public int Strength { get; set; } = strength;
    public int Stamina { get; set; } = stamina;
}

public class CombatStats(BaseStats b)
{
    // 1 Agility = 5 AbilityCooldown, 5 Evasion; ActionPoints = Agility/2 + 1
    public int AbilityCooldown { get; set; } = b.Agility * 5;
    public int ActionPoints { get; set; } = b.Agility / 2 + 1;
    public int Evasion { get; set; } = b.Agility * 5;

    // 1 Focus = 5 Accuracy, 5 CritChance, 5 Perception
    public int Accuracy { get; set; } = b.Focus * 5;
    public int CritChance { get; set; } = b.Focus * 5;
    public int Perception { get; set; } = b.Focus * 5;

    // 1 Mind = 5 ManaPool, 5 MagicDamage, 5 MagicDefense
    public int ManaPool { get; set; } = b.Mind * 5;
    public int MagicDamage { get; set; } = b.Mind * 1;
    public int MagicDefense { get; set; } = b.Mind * 1;

    // 1 Strength = 5 AttackPower, 5 CritDamageBonus, 1 Speed
    public int AttackPower { get; set; } = b.Strength * 1;
    public int CritDamageBonus { get; set; } = b.Strength * 5;
    public int Speed { get; set; } = b.Strength * 1;

    // 1 Stamina = 2 HitPoints, 1 MovementPoints, 5 PhysicalDefense
    public int HitPoints { get; set; } = (b.Stamina * 2) + 5;
    public int MovementPoints { get; set; } = b.Stamina + 3;
    public int PhysicalDefense { get; set; }= b.Stamina * 1;
}

public abstract class Creature
{
    public string Name { get; set; } = string.Empty;

    public BaseStats BaseStats { get; set; }
    public CombatStats CombatStats { get; set; }

    public int CurrentHp { get; set; }
    public int CurrentMana { get; set; }

    public List<Attack> Attacks { get; } = [];
    public List<StatusEffect> StatusEffects { get; } = [];
    public Weapon? EquippedWeapon { get; set; }

    protected virtual int StatCap => int.MaxValue;

    protected Creature(string name, int agility, int focus, int mind, int strength, int stamina)
    {
        Name = name;
        BaseStats = new BaseStats(
            Math.Clamp(agility,   0, StatCap),
            Math.Clamp(focus,     0, StatCap),
            Math.Clamp(mind,      0, StatCap),
            Math.Clamp(strength,  0, StatCap),
            Math.Clamp(stamina,   0, StatCap)
        );
        CombatStats = new CombatStats(BaseStats);

        CurrentHp = CombatStats.HitPoints;
        CurrentMana = CombatStats.ManaPool;
    }

    public void RaiseStat(BaseStat stat, int amount)
    {
        switch (stat)
        {
            case BaseStat.Agility:
                BaseStats.Agility = Math.Clamp(BaseStats.Agility + amount, 0, StatCap);
                break;
            case BaseStat.Focus:
                BaseStats.Focus = Math.Clamp(BaseStats.Focus + amount, 0, StatCap);
                break;
            case BaseStat.Mind:
                BaseStats.Mind = Math.Clamp(BaseStats.Mind + amount, 0, StatCap);
                break;
            case BaseStat.Strength:
                BaseStats.Strength = Math.Clamp(BaseStats.Strength + amount, 0, StatCap);
                break;
            case BaseStat.Stamina:
                BaseStats.Stamina = Math.Clamp(BaseStats.Stamina + amount, 0, StatCap);
                break;
        }
        CombatStats = new CombatStats(BaseStats);
    }

    public void ApplyStatusEffect(AttackEffect effect)
    {
        var statusEffect = new StatusEffect(
            (StatusEffectType)effect.Type,
            effect.TargetStat,
            Random.Shared.Next(effect.MinAmount, effect.MaxAmount + 1),
            Random.Shared.Next(effect.MinDuration, effect.MaxDuration + 1)
        );
        StatusEffects.Add(statusEffect);
    }

    public void TickStatusEffects()
    {
        foreach (var effect in StatusEffects.ToList())
        {
            effect.Tick();
            switch (effect.Type)
            {
                case StatusEffectType.Bleed:
                case StatusEffectType.Burn:
                case StatusEffectType.Poison:
                    CurrentHp -= effect.Amount;
                    break;
                case StatusEffectType.Chill:
                    CombatStats.MovementPoints -= effect.Amount;
                    break;
                case StatusEffectType.Static:
                    break;
                case StatusEffectType.StatIncrease:
                    RaiseCombatStat(effect.TargetStat, effect.Amount);
                    break;
                case StatusEffectType.StatReduction:
                    LowerCombatStat(effect.TargetStat, effect.Amount);
                    break;
                case StatusEffectType.Stun:
                    break;
            }
            if (effect.IsExpired)
            {
                StatusEffects.Remove(effect);
            }
        }
    }

public void RaiseCombatStat(CombatStat stat, float amount)
    {
        switch (stat)
        {
            case CombatStat.CritChance:
                CombatStats.CritChance = Math.Max(0, CombatStats.CritChance + (int)MathF.Round(amount));
                break;
            case CombatStat.Evasion:
                CombatStats.Evasion = Math.Max(0, CombatStats.Evasion + (int)MathF.Round(amount));
                break;
            case CombatStat.ActionPoints:
                CombatStats.ActionPoints = Math.Max(0, CombatStats.ActionPoints + (int)MathF.Round(amount));
                break;
            case CombatStat.Accuracy:
                CombatStats.Accuracy = Math.Max(0, CombatStats.Accuracy + (int)MathF.Round(amount));
                break;
            case CombatStat.Perception:
                CombatStats.Perception = Math.Max(0, CombatStats.Perception + (int)MathF.Round(amount));
                break;
            case CombatStat.AbilityCooldown:
                CombatStats.AbilityCooldown = Math.Max(0, CombatStats.AbilityCooldown + (int)MathF.Round(amount));
                break;
            case CombatStat.ManaPool:
                CombatStats.ManaPool = Math.Max(0, CombatStats.ManaPool + (int)MathF.Round(amount));
                break;
            case CombatStat.MagicDamage:
                CombatStats.MagicDamage = Math.Max(0, CombatStats.MagicDamage + (int)MathF.Round(amount));
                break;
            case CombatStat.MagicDefense:
                CombatStats.MagicDefense = Math.Max(0, CombatStats.MagicDefense + (int)MathF.Round(amount));
                break;
            case CombatStat.AttackPower:
                CombatStats.AttackPower = Math.Max(0, CombatStats.AttackPower + (int)MathF.Round(amount));
                break;
            case CombatStat.PhysicalDefense:
                CombatStats.PhysicalDefense = Math.Max(0, CombatStats.PhysicalDefense + (int)MathF.Round(amount));
                break;
            case CombatStat.CritDamageBonus:
                CombatStats.CritDamageBonus = Math.Max(0, CombatStats.CritDamageBonus + (int)MathF.Round(amount));
                break;
            case CombatStat.HitPoints:
                CombatStats.HitPoints = Math.Max(0, CombatStats.HitPoints + (int)MathF.Round(amount));
                break;
            case CombatStat.MovementPoints:
                CombatStats.MovementPoints = Math.Max(0, CombatStats.MovementPoints + (int)MathF.Round(amount));
                break;
        }
    }

    public void LowerCombatStat(CombatStat stat, float amount)
    {
        switch (stat)
        {
            case CombatStat.CritChance:
                CombatStats.CritChance = Math.Max(0, CombatStats.CritChance - (int)MathF.Round(amount));
                break;
            case CombatStat.Evasion:
                CombatStats.Evasion = Math.Max(0, CombatStats.Evasion - (int)MathF.Round(amount));
                break;
            case CombatStat.ActionPoints:
                CombatStats.ActionPoints = Math.Max(0, CombatStats.ActionPoints - (int)MathF.Round(amount));
                break;
            case CombatStat.Accuracy:
                CombatStats.Accuracy = Math.Max(0, CombatStats.Accuracy - (int)MathF.Round(amount));
                break;
            case CombatStat.Perception:
                CombatStats.Perception = Math.Max(0, CombatStats.Perception - (int)MathF.Round(amount));
                break;
            case CombatStat.AbilityCooldown:
                CombatStats.AbilityCooldown = Math.Max(0, CombatStats.AbilityCooldown - (int)MathF.Round(amount));
                break;
            case CombatStat.ManaPool:
                CombatStats.ManaPool = Math.Max(0, CombatStats.ManaPool - (int)MathF.Round(amount));
                break;
            case CombatStat.MagicDamage:
                CombatStats.MagicDamage = Math.Max(0, CombatStats.MagicDamage - (int)MathF.Round(amount));
                break;
            case CombatStat.MagicDefense:
                CombatStats.MagicDefense = Math.Max(0, CombatStats.MagicDefense - (int)MathF.Round(amount));
                break;
            case CombatStat.AttackPower:
                CombatStats.AttackPower = Math.Max(0, CombatStats.AttackPower - (int)MathF.Round(amount));
                break;
            case CombatStat.PhysicalDefense:
                CombatStats.PhysicalDefense = Math.Max(0, CombatStats.PhysicalDefense - (int)MathF.Round(amount));
                break;
            case CombatStat.CritDamageBonus:
                CombatStats.CritDamageBonus = Math.Max(0, CombatStats.CritDamageBonus - (int)MathF.Round(amount));
                break;
            case CombatStat.HitPoints:
                CombatStats.HitPoints = Math.Max(0, CombatStats.HitPoints - (int)MathF.Round(amount));
                break;
            case CombatStat.MovementPoints:
                CombatStats.MovementPoints = Math.Max(0, CombatStats.MovementPoints - (int)MathF.Round(amount));
                break;
        }
    }
}

