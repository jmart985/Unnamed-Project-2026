using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class SkillProgressionTests
{
    [Fact]
    public void SkillTracksExperienceAndLevelsWithinBounds()
    {
        var skills = new SkillSet([new SkillDefinition("Crafting", 2, 10)]);
        Assert.Equal(SkillProgressionResult.Applied, skills.AddExperience("Crafting", 25));
        Assert.True(skills.TryGet("Crafting", out var skill));
        Assert.Equal(2, skill!.Level);
        Assert.Equal(0, skill.Experience);
    }

    [Fact]
    public void InvalidAndUnknownProgressionIsRejected()
    {
        var skills = new SkillSet([new SkillDefinition("Combat", 3)]);
        Assert.Equal(SkillProgressionResult.UnknownSkill, skills.AddExperience("Cooking", 10));
        Assert.Equal(SkillProgressionResult.InvalidExperience, skills.AddExperience("Combat", -1));
    }

    [Fact]
    public void EntityReceivesReusableDefaultSkills()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        Assert.True(entity.Skills.TryGet("Athletics", out _));
    }
}
