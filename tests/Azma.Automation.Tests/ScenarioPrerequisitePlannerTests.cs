using Azma.Automation;
using Xunit;

namespace Azma.Automation.Tests;

public class ScenarioPrerequisitePlannerTests
{
    private static readonly Guid ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LoginId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ScenarioId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Login_prerequisite_runs_before_the_scenario()
    {
        var login = Link(LoginId, "ورود", null, """{"name":null,"startUrl":"https://app.example/login","actions":[{"type":"Navigate","url":"https://app.example/login"}],"networkCaptures":[],"expectations":[]}""");
        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink> { [LoginId] = login };

        var plan = ScenarioPrerequisitePlanner.Create(
            ScenarioId, ProjectId, LoginId, id => links.GetValueOrDefault(id));

        Assert.True(plan.Succeeded);
        Assert.Single(plan.Definitions);
        Assert.Equal("ورود", plan.Definitions[0].Name);
        Assert.Equal("https://app.example/login", plan.Definitions[0].StartUrl);
    }

    [Fact]
    public void Chain_runs_from_the_root_prerequisite()
    {
        var setupId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var login = Link(LoginId, "ورود", null, Recorded("ورود"));
        var setup = Link(setupId, "آماده‌سازی", LoginId, Recorded("آماده‌سازی"));
        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink>
        {
            [LoginId] = login,
            [setupId] = setup,
        };

        var plan = ScenarioPrerequisitePlanner.Create(
            ScenarioId, ProjectId, setupId, id => links.GetValueOrDefault(id));

        Assert.True(plan.Succeeded);
        Assert.Equal(["ورود", "آماده‌سازی"], plan.Definitions.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void Cycle_is_rejected()
    {
        var otherId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink>
        {
            [otherId] = Link(otherId, "دیگر", ScenarioId, Recorded("دیگر")),
        };

        var plan = ScenarioPrerequisitePlanner.Create(
            ScenarioId, ProjectId, otherId, id => links.GetValueOrDefault(id));

        Assert.False(plan.Succeeded);
        Assert.Contains("حلقه", plan.Error);
    }

    [Fact]
    public void Missing_recording_is_rejected()
    {
        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink>
        {
            [LoginId] = Link(LoginId, "ورود", null, null),
        };

        var plan = ScenarioPrerequisitePlanner.Create(
            ScenarioId, ProjectId, LoginId, id => links.GetValueOrDefault(id));

        Assert.False(plan.Succeeded);
        Assert.Contains("ضبط نشده", plan.Error);
    }

    private static ScenarioPrerequisitePlanner.ScenarioLink Link(
        Guid id, string title, Guid? prerequisiteId, string? json) =>
        new(id, ProjectId, title, prerequisiteId, json, false);

    private static string Recorded(string name) =>
        $$"""{"name":"{{name}}","startUrl":"https://app.example","actions":[{"type":"Navigate","url":"https://app.example"}],"networkCaptures":[],"expectations":[]}""";
}
