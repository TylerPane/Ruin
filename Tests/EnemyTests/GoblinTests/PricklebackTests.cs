using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;

namespace RuinGamePDT.Tests;

public class PricklebackGoblinTests
{
    private readonly PricklebackGoblin _f = new();

    [Fact] public void PricklebackGoblin_IsAMonstrosity()          => Assert.IsAssignableFrom<Monstrosity>(_f);
    [Fact] public void PricklebackGoblin_HasCorrectName()           => Assert.Equal("Prickleback Goblin", _f.Name);

    [Fact]
    public void PricklebackGoblin_HasCorrectBaseStats()
    {
        Assert.Equal(6, _f.BaseStats.Agility);
        Assert.Equal(4, _f.BaseStats.Focus);
        Assert.Equal(1, _f.BaseStats.Mind);
        Assert.Equal(3, _f.BaseStats.Strength);
        Assert.Equal(3, _f.BaseStats.Stamina);
    }

    [Fact] public void PricklebackGoblin_HasBonusMovementPoints()  => Assert.Equal(17.00f, _f.CombatStats.MovementPoints);
    [Fact] public void PricklebackGoblin_HasBonusCritChance() => Assert.Equal(30.00f, _f.CombatStats.CritChance);
    [Fact] public void PricklebackGoblin_HasThreeAttacks()         => Assert.Equal(3, _f.Attacks.Count);

    [Fact]
    public void Scratch_HasCorrectProperties()
    {
        var a = _f.Attacks.First(a => a.Name == "Scratch");
        Assert.Equal(1, a.MinDamage);
        Assert.Equal(3, a.MaxDamage);
        Assert.Equal(1, a.Range);
        Assert.Null(a.OnCrit);
        Assert.Null(a.OnHit);
    }

    [Fact]
    public void Skewer_HasCorrectProperties()
    {
        var a = _f.Attacks.First(a => a.Name == "Skewer");
        Assert.Equal(2, a.MinDamage);
        Assert.Equal(4, a.MaxDamage);
        Assert.Equal(5, a.Range);
        Assert.NotNull(a.OnHit);
        Assert.Null(a.OnCrit);
        Assert.Equal(AttackEffectType.Bleed, a.OnHit!.Type);
        Assert.Equal(5, a.OnHit.Stats[0].MinAmount);
        Assert.Equal(5, a.OnHit.Stats[0].MaxAmount);
        Assert.Equal(1, a.OnHit.MinDuration);
        Assert.Equal(3, a.OnHit.MaxDuration);
        Assert.Equal(25, a.OnHit.Chance);
    }

    [Fact]
    public void QuillSpray_HasCorrectProperties()
    {
        var a = _f.Attacks.First(a => a.Name == "Quill Spray");
        Assert.Equal(0, a.MinDamage);
        Assert.Equal(1, a.MaxDamage);
        Assert.Equal(0, a.Range);
        Assert.Equal(0, a.MinRange);
        Assert.Null(a.OnCrit);
        Assert.NotNull(a.OnHit);
        Assert.Equal(AttackEffectType.Bleed, a.OnHit!.Type);
        Assert.Equal(5, a.OnHit.Stats[0].MinAmount);
        Assert.Equal(5, a.OnHit.Stats[0].MaxAmount);
        Assert.Equal(1, a.OnHit.MinDuration);
        Assert.Equal(1, a.OnHit.MaxDuration);
        Assert.Equal(25, a.OnHit.Chance);
    }

    [Fact]
    public void QuillSpray_IsSelfCentered_RangeZero()
    {
        var quillSpray = _f.Attacks.First(a => a.Name == "Quill Spray");

        Assert.Equal(0, quillSpray.Range);
        Assert.Equal(0, quillSpray.MinRange);
    }

    [Fact]
    public void QuillSpray_HasCircularBurstShape_RadiusTwo()
    {
        var quillSpray = _f.Attacks.First(a => a.Name == "Quill Spray");
        var offsets = quillSpray.AttackShape.Offsets.ToHashSet();

        Assert.Contains((0, 0), offsets);
        Assert.Contains((2, 0), offsets);
        Assert.DoesNotContain((3, 0), offsets);
    }

    [Fact]
    public void QuillSpray_OnlyGoblinsOwnTile_IsInRange()
    {
        var state = new RuinGamePDT.Encounter.EncounterState(new RuinGamePDT.World.EncounterMap(20, 20));
        var g = new PricklebackGoblin();
        state.Enemies.Add(g);
        state.PlaceCreature(g, 5, 5);
        var quillSpray = g.Attacks.First(a => a.Name == "Quill Spray");

        var resolver = new RuinGamePDT.Encounter.CombatResolver(Random.Shared.Next);

        Assert.True(resolver.IsInRange(g, quillSpray, (5, 5), state));
        Assert.False(resolver.IsInRange(g, quillSpray, (6, 5), state));
    }
}
