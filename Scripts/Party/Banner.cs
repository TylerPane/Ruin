using RuinGamePDT.Creatures;

namespace RuinGamePDT.Party;

public class Banner
{
    public const int MaxSize = 4;

    public List<Mercenary> Mercenaries { get; } = [];
    public int X { get; set; }
    public int Y { get; set; }

    public bool AddMercenary(Mercenary mercenary)
    {
        if (Mercenaries.Count >= MaxSize) return false;
        Mercenaries.Add(mercenary);
        return true;
    }
}
