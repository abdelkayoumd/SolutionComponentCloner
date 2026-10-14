using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class MetadataLoadingTests
    {
        private readonly DataverseSolutionService _service = new DataverseSolutionService();
        private readonly Guid _attributeId = Guid.NewGuid();
        private readonly Guid _formId = Guid.NewGuid();

        private static Entity SolutionComponent(Guid id, int type)
        {
            var e = new Entity("solutioncomponent", Guid.NewGuid());
            e["objectid"] = id;
            e["componenttype"] = new OptionSetValue(type);
            return e;
        }

        private static EntityMetadata Table(string logical, string display, Guid? id = null)
        {
            return new EntityMetadata
            {
                LogicalName = logical,
                MetadataId = id ?? Guid.NewGuid(),
                DisplayName = new Label { UserLocalizedLabel = new LocalizedLabel(display, 1033) }
            };
        }

        private static EntityMetadata WithAttribute(EntityMetadata table, Guid attributeId, string logical, string display)
        {
            var attribute = new StringAttributeMetadata
            {
                LogicalName = logical,
                MetadataId = attributeId,
                DisplayName = new Label { UserLocalizedLabel = new LocalizedLabel(display, 1033) }
            };
            typeof(EntityMetadata).GetProperty("Attributes").GetSetMethod(true).Invoke(table, new object[] { new AttributeMetadata[] { attribute } });
            return table;
        }

        private FakeOrganizationService Fake(List<Entity> components, Func<OrganizationRequest, OrganizationResponse> onExecute, Dictionary<string, List<Entity>> tables = null)
        {
            tables = tables ?? new Dictionary<string, List<Entity>>();
            tables["solutioncomponent"] = components;
            return new FakeOrganizationService
            {
                OnExecute = onExecute,
                OnRetrieveMultiple = q =>
                {
                    var collection = new EntityCollection();
                    if (tables.TryGetValue(((QueryExpression)q).EntityName, out var rows))
                    {
                        collection.Entities.AddRange(rows);
                    }
                    return collection;
                }
            };
        }

        private static OrganizationResponse TableList(params EntityMetadata[] tables)
        {
            return new RetrieveAllEntitiesResponse { Results = new ParameterCollection { { "EntityMetadata", tables } } };
        }

        private static OrganizationResponse Changes(params EntityMetadata[] tables)
        {
            var collection = new EntityMetadataCollection();
            foreach (var table in tables)
            {
                collection.Add(table);
            }

            return new RetrieveMetadataChangesResponse
            {
                Results = new ParameterCollection { { "EntityMetadata", collection }, { "ServerVersionStamp", "1" } }
            };
        }

        [Fact]
        public void SolutionWithOnlyFormsDownloadsJustTheLightTableList()
        {
            var fake = Fake(
                new List<Entity> { SolutionComponent(_formId, 60) },
                r => TableList(Table("account", "Account")),
                new Dictionary<string, List<Entity>>
                {
                    ["systemform"] = new List<Entity>
                    {
                        new Entity("systemform", _formId) { ["name"] = "Main", ["objecttypecode"] = "account", ["type"] = new OptionSetValue(2) }
                    }
                });

            var result = _service.GetSolutionComponents(fake, Guid.NewGuid());

            var downloads = fake.ExecutedRequests.OfType<RetrieveAllEntitiesRequest>().ToList();
            Assert.Single(downloads);
            Assert.Equal(EntityFilters.Entity, downloads[0].EntityFilters);
            Assert.DoesNotContain(fake.ExecutedRequests, r => r is RetrieveMetadataChangesRequest);
            Assert.Equal("Account (account)", result.Items.Single().TableLabel);
        }

        [Fact]
        public void ColumnsAreLoadedOnlyForTheIdsInTheSolution()
        {
            var table = WithAttribute(Table("account", "Account"), _attributeId, "name", "Account Name");
            var fake = Fake(
                new List<Entity> { SolutionComponent(_attributeId, 2) },
                r => r is RetrieveMetadataChangesRequest ? Changes(table) : TableList(table));

            var result = _service.GetSolutionComponents(fake, Guid.NewGuid());

            var request = fake.ExecutedRequests.OfType<RetrieveMetadataChangesRequest>().Single();
            var condition = request.Query.AttributeQuery.Criteria.Conditions.Single();
            Assert.Equal("MetadataId", condition.PropertyName);
            Assert.Equal(MetadataConditionOperator.In, condition.ConditionOperator);
            Assert.Equal(new[] { _attributeId }, (Guid[])condition.Value);
            Assert.DoesNotContain(fake.ExecutedRequests.OfType<RetrieveAllEntitiesRequest>(), r => (r.EntityFilters & EntityFilters.Attributes) != 0);

            var item = result.Items.Single();
            Assert.Equal("Account Name (name)", item.DisplayName);
            Assert.Equal("account", item.TableName);
            Assert.Equal("Columns", item.Category);
        }

        [Fact]
        public void FallsBackToTheFullDownloadWhenTheTargetedCallIsRejected()
        {
            var table = WithAttribute(Table("account", "Account"), _attributeId, "name", "Account Name");
            var fake = Fake(
                new List<Entity> { SolutionComponent(_attributeId, 2) },
                r =>
                {
                    if (r is RetrieveMetadataChangesRequest)
                    {
                        throw new InvalidOperationException("not supported");
                    }

                    return TableList(table);
                });

            var result = _service.GetSolutionComponents(fake, Guid.NewGuid());

            Assert.Contains(fake.ExecutedRequests.OfType<RetrieveAllEntitiesRequest>(), r => (r.EntityFilters & EntityFilters.Attributes) != 0);
            Assert.Equal("Account Name (name)", result.Items.Single().DisplayName);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void ReportsProgressWhileLoading()
        {
            var fake = Fake(new List<Entity> { SolutionComponent(_formId, 60) }, r => TableList());
            var messages = new List<string>();

            _service.GetSolutionComponents(fake, Guid.NewGuid(), messages.Add);

            Assert.Contains(messages, m => m.StartsWith("Reading"));
            Assert.Contains(messages, m => m.Contains("Forms") && m.Contains("1 of 1"));
        }
    }
}
