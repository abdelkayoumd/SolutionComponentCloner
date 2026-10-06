using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ComponentHierarchyTests
    {
        private readonly DataverseSolutionService _service = new DataverseSolutionService();

        private static Entity SolutionComponent(Guid id, int type)
        {
            var e = new Entity("solutioncomponent", Guid.NewGuid());
            e["objectid"] = id;
            e["componenttype"] = new OptionSetValue(type);
            return e;
        }

        private static Entity Row(string entityName, Guid id, string nameAttribute, string name, params (string Key, object Value)[] extra)
        {
            var e = new Entity(entityName, id);
            e[nameAttribute] = name;
            foreach (var (key, value) in extra)
            {
                e[key] = value;
            }
            return e;
        }

        private FakeOrganizationService FakeWith(Dictionary<string, List<Entity>> tables)
        {
            return new FakeOrganizationService
            {
                OnExecute = r => r is RetrieveAllEntitiesRequest
                    ? new RetrieveAllEntitiesResponse { Results = new ParameterCollection { { "EntityMetadata", new EntityMetadataCollection() } } }
                    : new OrganizationResponse(),
                OnRetrieveMultiple = q =>
                {
                    var name = ((QueryExpression)q).EntityName;
                    var collection = new EntityCollection();
                    if (tables.TryGetValue(name, out var rows))
                    {
                        collection.Entities.AddRange(rows);
                    }
                    return collection;
                }
            };
        }

        private Models.SolutionComponentItem Load(Dictionary<string, List<Entity>> tables, Guid id)
        {
            var result = _service.GetSolutionComponents(FakeWith(tables), Guid.NewGuid());
            return result.Items.Single(i => i.ObjectId == id);
        }

        [Fact]
        public void FormIsFiledUnderItsTable()
        {
            var id = Guid.NewGuid();
            var item = Load(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(id, 60) },
                ["systemform"] = new List<Entity> { Row("systemform", id, "name", "Account Main", ("objecttypecode", "account"), ("type", new OptionSetValue(2))) }
            }, id);

            Assert.Equal("Tables", item.GroupName);
            Assert.Equal("account", item.TableName);
            Assert.Equal("Forms", item.Category);
            Assert.Equal("Account Main", item.DisplayName);
        }

        [Fact]
        public void DashboardWithoutATableGetsItsOwnGroup()
        {
            var id = Guid.NewGuid();
            var item = Load(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(id, 60) },
                ["systemform"] = new List<Entity> { Row("systemform", id, "name", "Sales Dashboard", ("objecttypecode", "none"), ("type", new OptionSetValue(0))) }
            }, id);

            Assert.Equal("Dashboards", item.GroupName);
            Assert.Null(item.TableName);
        }

        [Fact]
        public void ViewAndChartAreFiledUnderTheirTables()
        {
            var viewId = Guid.NewGuid();
            var chartId = Guid.NewGuid();
            var result = _service.GetSolutionComponents(FakeWith(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(viewId, 26), SolutionComponent(chartId, 59) },
                ["savedquery"] = new List<Entity> { Row("savedquery", viewId, "name", "Active Accounts", ("returnedtypecode", "account")) },
                ["savedqueryvisualization"] = new List<Entity> { Row("savedqueryvisualization", chartId, "name", "Pipeline", ("primaryentitytypecode", "opportunity")) }
            }), Guid.NewGuid());

            var view = result.Items.Single(i => i.ObjectId == viewId);
            var chart = result.Items.Single(i => i.ObjectId == chartId);
            Assert.Equal(("account", "Views"), (view.TableName, view.Category));
            Assert.Equal(("opportunity", "Charts"), (chart.TableName, chart.Category));
        }

        [Theory]
        [InlineData(3, "Code")]
        [InlineData(4, "Data")]
        [InlineData(5, "Images")]
        public void WebResourceIsFiledByItsFileType(int webResourceType, string expectedSubGroup)
        {
            var id = Guid.NewGuid();
            var item = Load(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(id, 61) },
                ["webresource"] = new List<Entity> { Row("webresource", id, "name", "new_file", ("webresourcetype", new OptionSetValue(webResourceType))) }
            }, id);

            Assert.Equal("Web resources", item.GroupName);
            Assert.Equal(expectedSubGroup, item.SubGroup);
            Assert.Null(item.TableName);
        }

        [Fact]
        public void BusinessRuleIsFiledUnderItsTableButOtherProcessesStayFlat()
        {
            var ruleId = Guid.NewGuid();
            var processId = Guid.NewGuid();
            var result = _service.GetSolutionComponents(FakeWith(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(ruleId, 29), SolutionComponent(processId, 29) },
                ["workflow"] = new List<Entity>
                {
                    Row("workflow", ruleId, "name", "Require phone", ("primaryentity", "contact"), ("category", new OptionSetValue(2))),
                    Row("workflow", processId, "name", "Auto assign", ("primaryentity", "lead"), ("category", new OptionSetValue(0)))
                }
            }), Guid.NewGuid());

            var rule = result.Items.Single(i => i.ObjectId == ruleId);
            var process = result.Items.Single(i => i.ObjectId == processId);
            Assert.Equal(("contact", "Business rules"), (rule.TableName, rule.Category));
            Assert.Null(process.TableName);
            Assert.Equal("Processes", process.GroupName);
        }

        [Fact]
        public void ComponentTypesWithoutHierarchyUseTheirPluralGroupName()
        {
            var id = Guid.NewGuid();
            var item = Load(new Dictionary<string, List<Entity>>
            {
                ["solutioncomponent"] = new List<Entity> { SolutionComponent(id, 20) },
                ["role"] = new List<Entity> { Row("role", id, "name", "Sales Manager") }
            }, id);

            Assert.Equal("Security roles", item.GroupName);
        }
    }
}
