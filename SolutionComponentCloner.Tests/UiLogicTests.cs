using SolutionComponentCloner.UI;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class UiLogicTests
    {
        [Theory]
        [InlineData("My new solution", "MyNewSolution")]
        [InlineData("marketing toolkit 2", "MarketingToolkit2")]
        [InlineData("2024 plan", "S2024Plan")]
        [InlineData("  Sales & Service!  ", "SalesService")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void SuggestsAValidUniqueNameFromTheDisplayName(string displayName, string expected)
        {
            Assert.Equal(expected, NewSolutionDialog.SuggestUniqueName(displayName));
        }
    }
}
