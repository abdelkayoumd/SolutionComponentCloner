using System;
using System.Collections.Generic;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using SolutionComponentCloner.UI;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ViewAndExportTests
    {
        private static SolutionComponentItem Item(string name, bool inTarget = false, DateTime? modifiedOn = null, string modifiedBy = null) =>
            new SolutionComponentItem
            {
                ObjectId = Guid.NewGuid(),
                ComponentType = 60,
                ComponentTypeName = "Form",
                DisplayName = name,
                InTarget = inTarget,
                ModifiedOn = modifiedOn,
                ModifiedBy = modifiedBy
            };

        // ---- filter chips -----------------------------------------------------------------------------

        [Theory]
        [InlineData("All", true, true)]
        [InlineData("All", false, true)]
        [InlineData("NotInTarget", true, false)]
        [InlineData("NotInTarget", false, true)]
        [InlineData("InBoth", true, true)]
        [InlineData("InBoth", false, false)]
        public void MembershipFilterKeepsWhatIsOrIsNotInTheTarget(string filterName, bool inTarget, bool expected)
        {
            var filter = (MembershipFilter)Enum.Parse(typeof(MembershipFilter), filterName);

            Assert.Equal(expected, ComponentFilter.MatchesView(Item("a", inTarget), filter, null, null));
        }

        [Fact]
        public void ModifiedSinceKeepsOnlyComponentsChangedOnOrAfterTheDate()
        {
            var since = new DateTime(2026, 11, 1);

            Assert.True(ComponentFilter.MatchesView(Item("new", modifiedOn: since), MembershipFilter.All, since, null));
            Assert.False(ComponentFilter.MatchesView(Item("old", modifiedOn: since.AddSeconds(-1)), MembershipFilter.All, since, null));
            Assert.False(ComponentFilter.MatchesView(Item("undated"), MembershipFilter.All, since, null));
        }

        [Fact]
        public void ModifiedByMatchesTheNameWhateverItsCase()
        {
            Assert.True(ComponentFilter.MatchesView(Item("mine", modifiedBy: "Sam Doe"), MembershipFilter.All, null, "sam doe"));
            Assert.False(ComponentFilter.MatchesView(Item("theirs", modifiedBy: "Alex Roy"), MembershipFilter.All, null, "Sam Doe"));
            Assert.False(ComponentFilter.MatchesView(Item("unknown"), MembershipFilter.All, null, "Sam Doe"));
        }

        [Fact]
        public void FiltersCombine()
        {
            var since = new DateTime(2026, 11, 1);
            var item = Item("a", inTarget: false, modifiedOn: since.AddDays(1), modifiedBy: "Sam Doe");

            Assert.True(ComponentFilter.MatchesView(item, MembershipFilter.NotInTarget, since, "Sam Doe"));
            Assert.False(ComponentFilter.MatchesView(item, MembershipFilter.InBoth, since, "Sam Doe"));
            Assert.False(ComponentFilter.MatchesView(item, MembershipFilter.NotInTarget, since.AddDays(2), "Sam Doe"));
        }

        // ---- "Modified by" choices ------------------------------------------------------------------------

        [Fact]
        public void ModifiedByChoicesPutAnyoneThenTheConnectedUserThenTheOthers()
        {
            var choices = DatePromptDialog.ModifiedByChoices(new[] { "Alex Roy", "sam doe", "Jo Park" }, "Sam Doe");

            Assert.Equal(new[] { "Anyone", "Me (Sam Doe)", "Alex Roy", "Jo Park" }, choices);
        }

        [Fact]
        public void ModifiedByChoicesWorkWithoutAConnectedUserName()
        {
            Assert.Equal(new[] { "Anyone", "Alex Roy" }, DatePromptDialog.ModifiedByChoices(new[] { "Alex Roy" }, null));
        }

        // ---- CSV exports ------------------------------------------------------------------------------

        [Fact]
        public void ResultsCsvListsEveryRowWithItsOutcome()
        {
            var ok = Item("Main form");
            var failed = Item("Quick, \"new\" form");
            var lines = CsvExport.Results(new List<ComponentCopyResult>
            {
                new ComponentCopyResult { Component = ok, Outcome = CopyOutcome.Success, Message = "Added." },
                new ComponentCopyResult { Component = failed, Outcome = CopyOutcome.Failed, Message = "Missing dependency" }
            });

            Assert.Equal("Component,Type,Id,Result,Details", lines[0]);
            Assert.Equal($"Main form,Form,{ok.ObjectId},Success,Added.", lines[1]);
            Assert.Equal($"\"Quick, \"\"new\"\" form\",Form,{failed.ObjectId},Failed,Missing dependency", lines[2]);
        }

        [Fact]
        public void ComparisonCsvSaysWhereEachComponentIs()
        {
            var sourceOnly = Item("A");
            var both = Item("B", inTarget: true);
            var targetOnly = Item("C");
            targetOnly.TableName = "account";

            var lines = CsvExport.Comparison(new[] { sourceOnly, both }, new[] { targetOnly });

            Assert.Equal("Component,Type,Table,Id,Where", lines[0]);
            Assert.Equal($"A,Form,,{sourceOnly.ObjectId},Only in source", lines[1]);
            Assert.Equal($"B,Form,,{both.ObjectId},In both", lines[2]);
            Assert.Equal($"C,Form,account,{targetOnly.ObjectId},Only in target", lines[3]);
        }
    }
}
