using ClusterManagement.Client.Extensions;

namespace ClusterManagement.Client.Tests.Extensions;

public class HoverTextsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Filling")]
    [InlineData(" Filling ")]
    public void Shows_only_the_name_without_a_description_of_its_own(string? description)
        => HoverTexts.NameWithDescription("Filling", description).Should().Be("Filling");

    [Fact]
    public void Shows_the_description_below_the_name()
        => HoverTexts.NameWithDescription("Filling", " Fills the bottles of line 2 ").Should().Be("Filling\nFills the bottles of line 2");

    [Fact]
    public void Shows_only_the_name_when_the_description_repeats_it_without_its_spaces()
        => HoverTexts.NameWithDescription("Filling ", "Filling").Should().Be("Filling ");
}
