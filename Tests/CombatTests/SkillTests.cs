using RuinGamePDT.Combat;

namespace RuinGamePDT.Tests;

public class SkillTests
{
    [Fact]
    public void Skill_IsAssignableFromAttack()
    {
        Assert.True(typeof(Attack).IsAssignableFrom(typeof(Skill)));
    }
}
