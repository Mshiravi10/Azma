using Azma.Automation;
using Xunit;

namespace Azma.Automation.Tests;

public class OAuthReplayTests
{
    [Fact]
    public void Recorded_login_fills_run_before_submit()
    {
        var actions = new[]
        {
            Action(RecordedActionType.Navigate, "https://workspace.example/"),
            Action(RecordedActionType.Navigate, "https://authenticator.example/realm/Mohaymen/login?client_id=1&response_type=code&redirect_uri=https%3A%2F%2Fworkspace.example%2Fsso%2FAM%2Fresult&state=frozen"),
            Action(RecordedActionType.Click, label: "ورود"),
            Action(RecordedActionType.Submit, label: "Submit form"),
            Action(RecordedActionType.Fill, label: "نام کاربری", value: "admin"),
            Action(RecordedActionType.Fill, label: "رمز عبور", value: "***"),
            Action(RecordedActionType.Navigate, "https://workspace.example/sso/AM/result?code=used-once&state=frozen&responseType=code"),
            Action(RecordedActionType.Navigate, "https://workspace.example/"),
        };

        var prepared = OAuthReplay.Prepare(actions);

        Assert.Equal(
            new[]
            {
                RecordedActionType.Navigate,
                RecordedActionType.Navigate,
                RecordedActionType.Fill,
                RecordedActionType.Fill,
                RecordedActionType.Click,
                RecordedActionType.Submit,
                RecordedActionType.Navigate,
                RecordedActionType.Navigate,
            },
            prepared.Select(action => action.Type).ToArray());
        Assert.Equal("admin", prepared[2].Value);
    }

    [Fact]
    public void Fills_already_before_submit_stay_in_place()
    {
        var actions = new[]
        {
            Action(RecordedActionType.Fill, value: "admin"),
            Action(RecordedActionType.Click, label: "ورود"),
            Action(RecordedActionType.Navigate, "https://workspace.example/resource/api_group/1"),
        };

        var prepared = OAuthReplay.Prepare(actions);

        Assert.Equal(
            new[] { RecordedActionType.Fill, RecordedActionType.Click, RecordedActionType.Navigate },
            prepared.Select(action => action.Type).ToArray());
    }

    [Fact]
    public void One_time_sso_addresses_are_recognized()
    {
        const string authorize = "https://authenticator.example/realm/Mohaymen/login?client_id=1&response_type=code&redirect_uri=https%3A%2F%2Fworkspace.example%2Fsso%2FAM%2Fresult&scope=openid&state=frozen";
        const string callback = "https://workspace.example/sso/AM/result?code=used-once&state=frozen&responseType=code";
        const string designer = "https://workspace.example/resource/api_group/1/application_service/2?realmId=a&branchId=b";

        Assert.True(OAuthReplay.IsAuthorizationRequest(authorize));
        Assert.True(OAuthReplay.IsLoginUrl(authorize));
        Assert.True(OAuthReplay.IsCallback(callback));
        Assert.False(OAuthReplay.IsLoginUrl(callback));
        Assert.False(OAuthReplay.IsAuthorizationRequest(designer));
        Assert.False(OAuthReplay.IsCallback(designer));
        Assert.False(OAuthReplay.IsLoginUrl(designer));
    }

    private static RecordedScenarioAction Action(
        RecordedActionType type,
        string? url = null,
        string? label = null,
        string? value = null) =>
        new(type, url, Label: label, Value: value);
}
