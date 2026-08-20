using RuinGamePDT.Combat;
using RuinGamePDT.Items;
using RuinGamePDT.Resources;

namespace RuinGamePDT.Weapons;

public abstract class Weapon : Item
{
    public WeaponType Type { get; }
    public List<Attack> Attacks { get; } = [];

    protected Weapon(WeaponType type)
    {
        Type = type;
    }
}