using System;
using SolutionComponentCloner.Models;
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

        [Fact]
        public void SettingsRememberEachConnectionSeparately()
        {
            var settings = new PluginSettings();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            settings.GetOrAdd(first).SourceUniqueName = "Sales";
            settings.GetOrAdd(second).SourceUniqueName = "Service";
            settings.GetOrAdd(first).TargetUniqueName = "Target";

            Assert.Equal(2, settings.Connections.Count);
            Assert.Equal("Sales", settings.ForConnection(first).SourceUniqueName);
            Assert.Equal("Target", settings.ForConnection(first).TargetUniqueName);
            Assert.Equal("Service", settings.ForConnection(second).SourceUniqueName);
            Assert.Null(settings.ForConnection(Guid.NewGuid()));
        }
    }
}
