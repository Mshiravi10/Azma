using Azma.Automation;
using Xunit;

namespace Azma.Automation.Tests;

public class RecordedScenarioValidatorTests
{
    [Fact]
    public void Stored_password_stays_available_for_replay()
    {
        var definition = new RecordedScenarioDefinition(
            "Login",
            "https://app.example/login",
            [
                new RecordedScenarioAction(
                    RecordedActionType.Fill,
                    Selector: "input[name=\"password\"]",
                    Value: "secret-value",
                    Label: "رمز عبور",
                    Sensitive: true),
            ],
            [],
            []);

        var validation = RecordedScenarioValidator.Validate(definition);

        Assert.True(validation.IsValid);
        Assert.Equal("secret-value", validation.SanitizedDefinition!.Actions[0].Value);
    }

    [Fact]
    public void Display_json_hides_the_password()
    {
        var stored = new RecordedScenarioDefinition(
            "Login",
            null,
            [
                new RecordedScenarioAction(
                    RecordedActionType.Fill,
                    Selector: "input[name=\"password\"]",
                    Value: "secret-value",
                    Label: "رمز عبور",
                    Sensitive: true),
                new RecordedScenarioAction(
                    RecordedActionType.Fill,
                    Selector: "input[name=\"username\"]",
                    Value: "admin",
                    Label: "نام کاربری"),
            ],
            [],
            []).ToJson();

        var displayed = RecordedScenarioDefinition.FromJson(RecordedScenarioValidator.MaskForDisplay(stored));

        Assert.NotNull(displayed);
        Assert.Equal("***", displayed!.Actions[0].Value);
        Assert.Equal("admin", displayed.Actions[1].Value);
    }

    [Fact]
    public void V2_steps_validate_nested_actions_and_assertions()
    {
        var definition = new RecordedScenarioDefinition(
            "Login",
            "{{baseUrl}}/login",
            [],
            [],
            [],
            new Dictionary<string, string> { ["baseUrl"] = "https://app.example" },
            Steps:
            [
                new RecordedScenarioStep(
                    "login",
                    "ورود",
                    "ورود به سامانه",
                    "داشبورد نمایش داده شود",
                    [new RecordedScenarioAction(RecordedActionType.Click, Selector: "[data-test=login]")],
                    [new RecordedExpectation(RecordedExpectationType.VisibleText, Text: "داشبورد")]),
            ],
            SchemaVersion: 2);

        var validation = RecordedScenarioValidator.Validate(definition);

        Assert.True(validation.IsValid);
        Assert.Single(validation.SanitizedDefinition!.Steps!);
    }

    [Fact]
    public void V2_step_ids_must_be_unique()
    {
        var action = new RecordedScenarioAction(RecordedActionType.Wait, DelayMs: 1);
        var definition = new RecordedScenarioDefinition(
            "Duplicate",
            null,
            [],
            [],
            [],
            Steps:
            [
                new RecordedScenarioStep("same", null, "one", null, [action], []),
                new RecordedScenarioStep("same", null, "two", null, [action], []),
            ],
            SchemaVersion: 2);

        var validation = RecordedScenarioValidator.Validate(definition);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("must be unique", StringComparison.Ordinal));
    }
}
