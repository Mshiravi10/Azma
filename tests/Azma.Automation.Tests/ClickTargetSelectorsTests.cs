using Xunit;

namespace Azma.Automation.Tests;

public class ClickTargetSelectorsTests
{
    [Fact]
    public void Icon_click_inside_a_button_targets_the_button_first()
    {
        const string recorded =
            "div:nth-of-type(1) > main:nth-of-type(1) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button:nth-of-type(1) > svg:nth-of-type(1)";

        var selectors = ClickTargetSelectors.Expand(recorded, null);

        Assert.Equal(
            [
                "div:nth-of-type(1) > main:nth-of-type(1) > div:nth-of-type(4) > div:nth-of-type(2) > div:nth-of-type(1) > button:nth-of-type(1)",
                recorded,
            ],
            selectors);
    }

    [Fact]
    public void Path_inside_an_icon_still_targets_the_button()
    {
        const string recorded = "div:nth-of-type(1) > button:nth-of-type(1) > svg:nth-of-type(1) > path:nth-of-type(1)";

        var healed = ClickTargetSelectors.PreferClickableAncestor(recorded);

        Assert.Equal("div:nth-of-type(1) > button:nth-of-type(1)", healed);
    }

    [Fact]
    public void Span_between_button_and_icon_is_skipped()
    {
        const string recorded = "button:nth-of-type(1) > span:nth-of-type(1) > svg:nth-of-type(1)";

        Assert.Equal("button:nth-of-type(1)", ClickTargetSelectors.PreferClickableAncestor(recorded));
    }

    [Fact]
    public void Named_icon_and_text_selectors_stay_unchanged()
    {
        Assert.Null(ClickTargetSelectors.PreferClickableAncestor("svg[name=\"Other/TbX\"]"));
        Assert.Null(ClickTargetSelectors.PreferClickableAncestor("text=بازگشت"));
        Assert.Equal(["[aria-label=\"بستن\"]"], ClickTargetSelectors.Expand("[aria-label=\"بستن\"]", null));
    }
}
