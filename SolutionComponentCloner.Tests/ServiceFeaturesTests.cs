using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ServiceFeaturesTests
    {
        private readonly DataverseSolutionService _service = new DataverseSolutionService();

        private static SolutionComponentItem Item(int type, string name = "n", SubcomponentMode mode = SubcomponentMode.UseGlobalSetting) =>
            new SolutionComponentItem { ObjectId = Guid.NewGuid(), ComponentType = type, DisplayName = name, SubcomponentMode = mode };

        private static Func<OrganizationRequest, OrganizationResponse> MultiAware(Func<OrganizationRequest, OrganizationResponse> inner)
        {
            return r =>
            {
                if (r is ExecuteMultipleRequest multiple)
                {
                    var items = new ExecuteMultipleResponseItemCollection();
                    for (var i = 0; i < multiple.Requests.Count; i++)
                    {
                        try
                        {
                            items.Add(new ExecuteMultipleResponseItem { RequestIndex = i, Response = inner(multiple.Requests[i]) });
                        }
                        catch (Exception ex)
                        {
                            items.Add(new ExecuteMultipleResponseItem { RequestIndex = i, Fault = new OrganizationServiceFault { Message = ex.Message } });
                        }
                    }

                    return new ExecuteMultipleResponse
                    {
                        Results = new ParameterCollection { { "Responses", items }, { "IsFaulted", items.Any(x => x.Fault != null) } }
                    };
                }

                return inner(r);
            };
        }

        private static Entity SolutionComponent(Guid id, int type)
        {
            var e = new Entity("solutioncomponent", Guid.NewGuid());
            e["objectid"] = id;
            e["componenttype"] = new OptionSetValue(type);
            return e;
        }

        private static OrganizationResponse NoTables() =>
            new RetrieveAllEntitiesResponse { Results = new ParameterCollection { { "EntityMetadata", new EntityMetadata[0] } } };

        // ---- per-table subcomponent behaviour ------------------------------------------------------------

        [Theory]
        [InlineData(SubcomponentMode.UseGlobalSetting, true, false)]
        [InlineData(SubcomponentMode.UseGlobalSetting, false, true)]
        [InlineData(SubcomponentMode.IncludeAll, false, false)]
        [InlineData(SubcomponentMode.OnlySelected, true, true)]
        public void TableSubcomponentModeOverridesTheCheckbox(SubcomponentMode mode, bool includeRequired, bool expectedDoNotInclude)
        {
            var fake = new FakeOrganizationService();

            _service.CopyComponent(fake, Item(ComponentTypeCatalog.Entity, "account", mode), "target", includeRequired);

            var request = Assert.IsType<AddSolutionComponentRequest>(fake.ExecutedRequests.Single());
            Assert.Equal(expectedDoNotInclude, request.DoNotIncludeSubcomponents);
        }

        [Fact]
        public void SubcomponentModeIsIgnoredForNonTables()
        {
            var fake = new FakeOrganizationService();

            _service.CopyComponent(fake, Item(26, "view", SubcomponentMode.OnlySelected), "target", false);

            Assert.False(((AddSolutionComponentRequest)fake.ExecutedRequests.Single()).DoNotIncludeSubcomponents);
        }

        // ---- batched copy ------------------------------------------------------------------------------

        [Fact]
        public void CopyComponentsSendsBatchesOfTwentyAndMapsEachResult()
        {
            var components = Enumerable.Range(0, 45).Select(i => Item(26, "v" + i)).ToList();
            var failing = components[7].ObjectId;
            var fake = new FakeOrganizationService
            {
                OnExecute = MultiAware(r =>
                {
                    if (((AddSolutionComponentRequest)r).ComponentId == failing)
                    {
                        throw new InvalidOperationException("already exists");
                    }

                    return new OrganizationResponse();
                })
            };
            var progress = new List<(int, int)>();

            var results = _service.CopyComponents(fake, components, "target", true, (done, total) => progress.Add((done, total)), null, out var cancelled);

            Assert.False(cancelled);
            Assert.Equal(3, fake.ExecutedRequests.OfType<ExecuteMultipleRequest>().Count());
            Assert.Equal(45, results.Count);
            Assert.Equal(CopyOutcome.Failed, results[7].Outcome);
            Assert.Equal("already exists", results[7].Message);
            Assert.Equal(44, results.Count(r => r.Outcome == CopyOutcome.Success));
            Assert.Same(components[7], results[7].Component);
            Assert.Equal(new[] { (20, 45), (40, 45), (45, 45) }, progress);
        }

        [Fact]
        public void CopyComponentsStopsBetweenBatchesWhenCancelled()
        {
            var components = Enumerable.Range(0, 45).Select(i => Item(26)).ToList();
            var fake = new FakeOrganizationService { OnExecute = MultiAware(r => new OrganizationResponse()) };
            var batchesSent = 0;

            var results = _service.CopyComponents(fake, components, "target", false, (d, t) => batchesSent++, () => batchesSent >= 1, out var cancelled);

            Assert.True(cancelled);
            Assert.Equal(20, results.Count);
            Assert.Single(fake.ExecutedRequests.OfType<ExecuteMultipleRequest>());
        }

        [Fact]
        public void CopyComponentsFallsBackToOneByOneWhenBatchingIsRejected()
        {
            var components = Enumerable.Range(0, 3).Select(i => Item(26)).ToList();
            var fake = new FakeOrganizationService
            {
                OnExecute = r =>
                {
                    if (r is ExecuteMultipleRequest)
                    {
                        throw new NotSupportedException("no batching");
                    }

                    return new OrganizationResponse();
                }
            };

            var results = _service.CopyComponents(fake, components, "target", false, null, null, out _);

            Assert.Equal(3, results.Count(r => r.Outcome == CopyOutcome.Success));
            Assert.Equal(3, fake.ExecutedRequests.OfType<AddSolutionComponentRequest>().Count());
        }

        // ---- target membership, publish, create ---------------------------------------------------------

        [Fact]
        public void ReadsTheTargetSolutionsComponentKeys()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var fake = new FakeOrganizationService
            {
                OnRetrieveMultiple = q => new EntityCollection(new List<Entity> { SolutionComponent(a, 60), SolutionComponent(b, 26) })
            };

            var keys = _service.GetSolutionComponentKeys(fake, Guid.NewGuid());

            Assert.Contains((60, a), keys);
            Assert.Contains((26, b), keys);
            Assert.DoesNotContain((26, a), keys);
        }

        [Fact]
        public void PublishAllSendsPublishAllXml()
        {
            var fake = new FakeOrganizationService();

            _service.PublishAll(fake);

            Assert.IsType<PublishAllXmlRequest>(fake.ExecutedRequests.Single());
        }

        [Fact]
        public void CreatesASolutionWithItsPublisher()
        {
            var publisher = Guid.NewGuid();
            var created = Guid.NewGuid();
            var fake = new FakeOrganizationService { OnCreate = e => created };

            var id = _service.CreateSolution(fake, "My Solution", "MySolution", "1.0.0.0", publisher);

            Assert.Equal(created, id);
            var entity = fake.Created.Single();
            Assert.Equal("solution", entity.LogicalName);
            Assert.Equal("MySolution", entity["uniquename"]);
            Assert.Equal("My Solution", entity["friendlyname"]);
            Assert.Equal("1.0.0.0", entity["version"]);
            Assert.Equal(publisher, ((EntityReference)entity["publisherid"]).Id);
        }

        [Fact]
        public void ListsPublishers()
        {
            var id = Guid.NewGuid();
            var publisher = new Entity("publisher", id);
            publisher["friendlyname"] = "Contoso";
            publisher["uniquename"] = "contoso";
            publisher["customizationprefix"] = "con";
            var fake = new FakeOrganizationService { OnRetrieveMultiple = q => new EntityCollection(new List<Entity> { publisher }) };

            var publishers = _service.GetPublishers(fake);

            var item = Assert.Single(publishers);
            Assert.Equal(id, item.PublisherId);
            Assert.Equal("Contoso (con)", item.ToString());
        }

        // ---- required components -----------------------------------------------------------------------

        [Fact]
        public void RequiredComponentsExcludeTheSelectionAndDuplicates()
        {
            var form = Item(60, "Account Main");
            var selectedView = Item(26, "Active Accounts");
            var script = Guid.NewGuid();
            var fake = new FakeOrganizationService
            {
                OnExecute = MultiAware(r =>
                {
                    if (r is RetrieveRequiredComponentsRequest)
                    {
                        var dependencies = new EntityCollection();
                        foreach (var (id, type) in new[] { (script, 61), (selectedView.ObjectId, 26), (script, 61) })
                        {
                            var dependency = new Entity("dependency", Guid.NewGuid());
                            dependency["requiredcomponentobjectid"] = id;
                            dependency["requiredcomponenttype"] = new OptionSetValue(type);
                            dependencies.Entities.Add(dependency);
                        }

                        return new RetrieveRequiredComponentsResponse { Results = new ParameterCollection { { "EntityCollection", dependencies } } };
                    }

                    return NoTables();
                }),
                OnRetrieveMultiple = q =>
                {
                    var collection = new EntityCollection();
                    if (((QueryExpression)q).EntityName == "webresource")
                    {
                        collection.Entities.Add(new Entity("webresource", script)
                        {
                            ["name"] = "new_account.js",
                            ["webresourcetype"] = new OptionSetValue(3)
                        });
                    }

                    return collection;
                }
            };

            var result = _service.GetRequiredComponents(fake, new List<SolutionComponentItem> { form, selectedView });

            var item = Assert.Single(result.Items);
            Assert.Equal(script, item.ObjectId);
            Assert.Equal("new_account.js", item.DisplayName);
            Assert.Equal("Code", item.SubGroup);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void RequiredComponentsWarnWhenSomeCannotBeChecked()
        {
            var fake = new FakeOrganizationService
            {
                OnExecute = MultiAware(r => throw new InvalidOperationException("denied"))
            };

            var result = _service.GetRequiredComponents(fake, new List<SolutionComponentItem> { Item(60) });

            Assert.Empty(result.Items);
            Assert.Contains("could not be checked", Assert.Single(result.Warnings));
        }

        // ---- modified date, modified by and managed state ------------------------------------------------

        private FakeOrganizationService FormsFake(Guid formId, Func<QueryExpression, EntityCollection> forms)
        {
            return new FakeOrganizationService
            {
                OnExecute = r => NoTables(),
                OnRetrieveMultiple = q =>
                {
                    var query = (QueryExpression)q;
                    if (query.EntityName == "solutioncomponent")
                    {
                        return new EntityCollection(new List<Entity> { SolutionComponent(formId, 60) });
                    }

                    return forms(query);
                }
            };
        }

        [Fact]
        public void ReadsModifiedDateModifiedByAndManagedState()
        {
            var formId = Guid.NewGuid();
            var when = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
            var fake = FormsFake(formId, q =>
            {
                var form = new Entity("systemform", formId) { ["name"] = "Main", ["objecttypecode"] = "account", ["type"] = new OptionSetValue(2) };
                form["modifiedon"] = when;
                form["modifiedby"] = new EntityReference("systemuser", Guid.NewGuid()) { Name = "Sam Doe" };
                form["ismanaged"] = true;
                return new EntityCollection(new List<Entity> { form });
            });

            var item = _service.GetSolutionComponents(fake, Guid.NewGuid()).Items.Single();

            Assert.Equal(when.ToLocalTime(), item.ModifiedOn);
            Assert.Equal("Sam Doe", item.ModifiedBy);
            Assert.True(item.IsManaged);
        }

        [Fact]
        public void DropsOptionalColumnsATableDoesNotHaveAndRemembersIt()
        {
            var formId = Guid.NewGuid();
            var attempts = new List<string[]>();
            var fake = FormsFake(formId, q =>
            {
                attempts.Add(q.ColumnSet.Columns.ToArray());
                if (q.ColumnSet.Columns.Contains("ismanaged"))
                {
                    throw new InvalidOperationException("ismanaged does not exist");
                }

                var form = new Entity("systemform", formId) { ["name"] = "Main", ["objecttypecode"] = "account", ["type"] = new OptionSetValue(2) };
                form["modifiedon"] = DateTime.UtcNow;
                return new EntityCollection(new List<Entity> { form });
            });

            var first = _service.GetSolutionComponents(fake, Guid.NewGuid());
            var second = _service.GetSolutionComponents(fake, Guid.NewGuid());

            Assert.Equal("Main", first.Items.Single().DisplayName);
            Assert.NotNull(first.Items.Single().ModifiedOn);
            Assert.Null(first.Items.Single().IsManaged);
            Assert.Empty(first.Warnings);
            Assert.Equal(3, attempts.Count);
            Assert.DoesNotContain("ismanaged", attempts[2]);
            Assert.Equal("Main", second.Items.Single().DisplayName);
        }

        // ---- custom messages and commands under a table ---------------------------------------------------

        private FakeOrganizationService DynamicFake(Guid id, int type, string tableName, Func<QueryExpression, EntityCollection> rows)
        {
            var metadata = new EntityMetadata { LogicalName = tableName, MetadataId = Guid.NewGuid() };
            typeof(EntityMetadata).GetProperty("PrimaryIdAttribute").GetSetMethod(true).Invoke(metadata, new object[] { tableName + "id" });
            typeof(EntityMetadata).GetProperty("PrimaryNameAttribute").GetSetMethod(true).Invoke(metadata, new object[] { "name" });
            return new FakeOrganizationService
            {
                OnExecute = r => new RetrieveAllEntitiesResponse { Results = new ParameterCollection { { "EntityMetadata", new[] { metadata } } } },
                OnRetrieveMultiple = q =>
                {
                    var query = (QueryExpression)q;
                    switch (query.EntityName)
                    {
                        case "solutioncomponent":
                            return new EntityCollection(new List<Entity> { SolutionComponent(id, type) });
                        case "solutioncomponentdefinition":
                            var definition = new Entity("solutioncomponentdefinition", Guid.NewGuid());
                            definition["objecttypecode"] = type;
                            definition["name"] = "Custom API";
                            definition["primaryentityname"] = tableName;
                            return new EntityCollection(new List<Entity> { definition });
                        default:
                            return rows(query);
                    }
                }
            };
        }

        [Fact]
        public void CustomApiBoundToATableIsFiledUnderMessages()
        {
            var id = Guid.NewGuid();
            var fake = DynamicFake(id, 10500, "customapi", q => new EntityCollection(new List<Entity>
            {
                new Entity("customapi", id) { ["name"] = "contoso_Recalculate", ["boundentitylogicalname"] = "account" }
            }));

            var item = _service.GetSolutionComponents(fake, Guid.NewGuid()).Items.Single();

            Assert.Equal("contoso_Recalculate", item.DisplayName);
            Assert.Equal("account", item.TableName);
            Assert.Equal("Messages", item.Category);
            Assert.Equal("Tables", item.GroupName);
        }

        [Fact]
        public void CustomApiStillGetsItsNameWhenTheTableColumnIsMissing()
        {
            var id = Guid.NewGuid();
            var fake = DynamicFake(id, 10500, "customapi", q =>
            {
                if (q.ColumnSet.Columns.Contains("boundentitylogicalname"))
                {
                    throw new InvalidOperationException("no such column");
                }

                return new EntityCollection(new List<Entity> { new Entity("customapi", id) { ["name"] = "contoso_Recalculate" } });
            });

            var result = _service.GetSolutionComponents(fake, Guid.NewGuid());

            var item = result.Items.Single();
            Assert.Equal("contoso_Recalculate", item.DisplayName);
            Assert.Null(item.TableName);
            Assert.Empty(result.Warnings);
        }

        // ---- search and sort ---------------------------------------------------------------------------

        [Theory]
        [InlineData("", true)]
        [InlineData("main", true)]
        [InlineData("ACCOUNT main", true)]
        [InlineData("form", true)]
        [InlineData("sam", true)]
        [InlineData("main contact", false)]
        [InlineData("zzz", false)]
        public void SearchMatchesEveryWordAgainstAnyField(string text, bool expected)
        {
            var item = new SolutionComponentItem
            {
                DisplayName = "Account Main Form", ComponentTypeName = "Form", GroupName = "Tables",
                TableLabel = "Account (account)", Category = "Forms", ModifiedBy = "Sam Doe"
            };

            Assert.Equal(expected, ComponentFilter.Matches(item, text));
        }

        [Fact]
        public void LeavesCanBeSortedByModifiedDateNewestFirst()
        {
            SolutionComponentItem Leaf(string name, int? day) => new SolutionComponentItem
            {
                ObjectId = Guid.NewGuid(), ComponentType = 61, DisplayName = name, GroupName = "Web resources",
                ModifiedOn = day.HasValue ? new DateTime(2026, 1, day.Value) : (DateTime?)null
            };
            var items = new[] { Leaf("a", 5), Leaf("b", 20), Leaf("c", null), Leaf("d", 12) };

            var roots = ComponentTreeBuilder.Build(items, new ComponentSort { Column = ComponentSortColumn.Modified, Descending = true });

            Assert.Equal(new[] { "b", "d", "a", "c" }, roots.Single().Children.Select(c => c.Name));
        }

        [Fact]
        public void NodeKeysStayTheSameWhenTheTreeIsRebuilt()
        {
            var table = new SolutionComponentItem
            {
                ObjectId = Guid.NewGuid(), ComponentType = 60, DisplayName = "Main", GroupName = "Tables",
                TableName = "account", TableLabel = "Account", Category = "Forms"
            };

            var first = ComponentTreeBuilder.AllNodes(ComponentTreeBuilder.Build(new[] { table })).Select(n => n.Key).ToList();
            var second = ComponentTreeBuilder.AllNodes(ComponentTreeBuilder.Build(new[] { table })).Select(n => n.Key).ToList();

            Assert.Equal(first, second);
            Assert.Equal(first.Count, first.Distinct().Count());
        }
    }
}
