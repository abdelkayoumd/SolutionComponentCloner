using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class CompletenessTests
    {
        private readonly DataverseSolutionService _service = new DataverseSolutionService();

        private static SolutionComponentItem Item(int type, string name, int? behavior = null, SubcomponentMode mode = SubcomponentMode.UseGlobalSetting) =>
            new SolutionComponentItem
            {
                ObjectId = Guid.NewGuid(), ComponentType = type, DisplayName = name,
                RootComponentBehavior = behavior, SubcomponentMode = mode
            };

        private FakeOrganizationService TableFake(Guid tableId, int? behavior, Func<QueryExpression, bool> failWhen = null)
        {
            var metadata = new EntityMetadata { LogicalName = "account", MetadataId = tableId };
            return new FakeOrganizationService
            {
                OnExecute = r => new RetrieveAllEntitiesResponse { Results = new ParameterCollection { { "EntityMetadata", new[] { metadata } } } },
                OnRetrieveMultiple = q =>
                {
                    var query = (QueryExpression)q;
                    if (failWhen != null && failWhen(query))
                    {
                        throw new InvalidOperationException("rootcomponentbehavior does not exist");
                    }

                    var row = new Entity("solutioncomponent", Guid.NewGuid());
                    row["objectid"] = tableId;
                    row["componenttype"] = new OptionSetValue(1);
                    if (behavior.HasValue && query.ColumnSet.Columns.Contains("rootcomponentbehavior"))
                    {
                        row["rootcomponentbehavior"] = new OptionSetValue(behavior.Value);
                    }

                    return new EntityCollection(new List<Entity> { row });
                }
            };
        }

        // ---- reading how a table was added --------------------------------------------------------------

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        public void ATableIsAddedCompletelyOnlyWhenItIncludesAllSubcomponents(int behavior, bool expected)
        {
            var id = Guid.NewGuid();

            var item = _service.GetSolutionComponents(TableFake(id, behavior), Guid.NewGuid()).Items.Single();

            Assert.Equal(behavior, item.RootComponentBehavior);
            Assert.Equal(expected, item.IsAddedCompletely);
        }

        [Fact]
        public void ATableWithNoBehaviorOnRecordIsNotCalledComplete()
        {
            var item = _service.GetSolutionComponents(TableFake(Guid.NewGuid(), null), Guid.NewGuid()).Items.Single();

            Assert.Null(item.RootComponentBehavior);
            Assert.False(item.IsAddedCompletely);
        }

        [Fact]
        public void ReadsComponentsWithoutTheBehaviorColumnWhenTheOrganizationHasNone()
        {
            var id = Guid.NewGuid();
            var fake = TableFake(id, 0, q => q.ColumnSet.Columns.Contains("rootcomponentbehavior"));

            var result = _service.GetSolutionComponents(fake, Guid.NewGuid());

            var item = result.Items.Single();
            Assert.Equal(id, item.ObjectId);
            Assert.Null(item.RootComponentBehavior);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void OnlyTablesCountAsAddedCompletely()
        {
            Assert.False(Item(60, "form", behavior: 0).IsAddedCompletely);
            Assert.True(Item(ComponentTypeCatalog.Entity, "account", behavior: 0).IsAddedCompletely);
        }

        // ---- the warning before copying -----------------------------------------------------------------

        [Fact]
        public void NoWarningsForOrdinaryComponents()
        {
            var selected = new[]
            {
                Item(60, "Main form"), Item(26, "Active accounts"), Item(ComponentTypeCatalog.Entity, "partial table", behavior: 1)
            };

            Assert.Empty(CopyWarnings.Build(selected, includeRequired: true));
        }

        [Fact]
        public void WarnsAboutModelDrivenApps()
        {
            var warnings = CopyWarnings.Build(new[] { Item(ComponentTypeCatalog.ModelDrivenApp, "Sales Hub") }, false);

            var warning = Assert.Single(warnings);
            Assert.Equal("Model-driven app", warning.Kind);
            Assert.Equal("Sales Hub", warning.Item.DisplayName);
        }

        [Theory]
        [InlineData(SubcomponentMode.UseGlobalSetting, true, true)]
        [InlineData(SubcomponentMode.UseGlobalSetting, false, false)]
        [InlineData(SubcomponentMode.IncludeAll, false, true)]
        [InlineData(SubcomponentMode.OnlySelected, true, false)]
        public void WarnsAboutTablesAddedCompletelyAndSaysWhatWillBeCopied(SubcomponentMode mode, bool includeRequired, bool allCopied)
        {
            var warnings = CopyWarnings.Build(new[] { Item(ComponentTypeCatalog.Entity, "account", behavior: 0, mode: mode) }, includeRequired);

            var warning = Assert.Single(warnings);
            Assert.Equal("Table added completely", warning.Kind);
            Assert.Equal(allCopied, warning.Message.Contains("All of its subcomponents"));
            Assert.Equal(!allCopied, warning.Message.Contains("Only the table"));
        }

        [Fact]
        public void ListsAppsAndTablesTogetherSortedByKindThenName()
        {
            var selected = new[]
            {
                Item(ComponentTypeCatalog.Entity, "Zebra", behavior: 0),
                Item(ComponentTypeCatalog.ModelDrivenApp, "Sales Hub"),
                Item(ComponentTypeCatalog.Entity, "Account", behavior: 0),
                Item(60, "Main form")
            };

            var warnings = CopyWarnings.Build(selected, true);

            Assert.Equal(new[] { "Sales Hub", "Account", "Zebra" }, warnings.Select(w => w.Item.DisplayName));
        }
    }
}
