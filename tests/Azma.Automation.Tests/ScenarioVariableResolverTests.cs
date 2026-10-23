using Azma.Automation;
using Xunit;

namespace Azma.Automation.Tests;

public class ScenarioVariableResolverTests
{
    [Fact]
    public void Configuration_frontend_overrides_recorded_base_url_in_ordered_steps()
    {
        var definition = new RecordedScenarioDefinition(
            "Portable",
            "{{baseUrl}}/start",
            [],
            [],
            [],
            new Dictionary<string, string> { ["baseUrl"] = "https://recorded.example" },
            Steps:
            [
                new RecordedScenarioStep(
                    "open",
                    "Open",
                    "Open page",
                    null,
                    [new RecordedScenarioAction(RecordedActionType.Navigate, "{{baseUrl}}/orders")],
                    []),
            ],
            SchemaVersion: 2);

        var resolved = ScenarioVariableResolver.Apply(
            definition,
            new AutomationRunConfiguration(Frontend: "https://test.example/"));

        Assert.Equal("https://test.example/start", resolved.StartUrl);
        Assert.Equal("https://test.example/orders", resolved.Steps![0].Actions[0].Url);
    }

    [Fact]
    public void Unknown_placeholders_are_preserved_for_diagnostics()
    {
        var definition = new RecordedScenarioDefinition(
            "Unknown",
            "{{missing}}/start",
            [new RecordedScenarioAction(RecordedActionType.Navigate, "{{missing}}/next")],
            [],
            []);

        var resolved = ScenarioVariableResolver.Apply(definition, null);

        Assert.Equal("{{missing}}/start", resolved.StartUrl);
        Assert.Equal("{{missing}}/next", resolved.Actions[0].Url);
    }
}
