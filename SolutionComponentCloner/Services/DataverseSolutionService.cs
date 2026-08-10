using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    internal sealed class DataverseSolutionService
    {
        private readonly MetadataNameCache _metadataCache = new MetadataNameCache();

        public List<SolutionListItem> GetSolutions(IOrganizationService service)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "ismanaged", "version"),
                Criteria = new FilterExpression(),
                Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
            };
            query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);

            var result = service.RetrieveMultiple(query);

            return result.Entities
                .Select(e => new SolutionListItem
                {
                    SolutionId = e.Id,
                    UniqueName = e.GetAttributeValue<string>("uniquename"),
                    FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                    IsManaged = e.GetAttributeValue<bool>("ismanaged"),
                    Version = e.GetAttributeValue<string>("version")
                })
                .ToList();
        }

        public List<SolutionComponentItem> GetSolutionComponents(IOrganizationService service, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            var rows = service.RetrieveMultiple(query).Entities
                .Select(e => new SolutionComponentItem
                {
                    ObjectId = e.GetAttributeValue<Guid>("objectid"),
                    ComponentType = e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0
                })
                .ToList();

            foreach (var group in rows.GroupBy(r => r.ComponentType))
            {
                ResolveDisplayNames(service, group.Key, group.ToList());
            }

            foreach (var row in rows)
            {
                row.ComponentTypeName = ComponentTypeCatalog.GetTypeName(row.ComponentType);
                if (string.IsNullOrEmpty(row.DisplayName))
                {
                    row.DisplayName = row.ObjectId.ToString();
                }
            }

            return rows
                .OrderBy(r => r.ComponentTypeName)
                .ThenBy(r => r.DisplayName)
                .ToList();
        }

        private void ResolveDisplayNames(IOrganizationService service, int componentType, List<SolutionComponentItem> items)
        {
            if (!ComponentTypeCatalog.Definitions.TryGetValue(componentType, out var definition))
            {
                return;
            }

            try
            {
                if (definition.IsMetadataBacked)
                {
                    ResolveMetadataBackedNames(service, componentType, items);
                }
                else
                {
                    ResolveTableBackedNames(service, definition, items);
                }
            }
            catch (Exception)
            {
                // Leave DisplayName unset for this group; caller falls back to the raw id.
            }
        }

        private void ResolveMetadataBackedNames(IOrganizationService service, int componentType, List<SolutionComponentItem> items)
        {
            foreach (var item in items)
            {
                switch (componentType)
                {
                    case ComponentTypeCatalog.Entity:
                        item.DisplayName = _metadataCache.GetEntityName(service, item.ObjectId);
                        break;
                    case ComponentTypeCatalog.Attribute:
                        item.DisplayName = _metadataCache.GetAttributeName(service, item.ObjectId);
                        break;
                    case ComponentTypeCatalog.Relationship:
                        item.DisplayName = _metadataCache.GetRelationshipName(service, item.ObjectId);
                        break;
                    case ComponentTypeCatalog.EntityKey:
                        item.DisplayName = _metadataCache.GetEntityKeyName(service, item.ObjectId);
                        break;
                    case ComponentTypeCatalog.OptionSet:
                        item.DisplayName = _metadataCache.GetOptionSetName(service, item.ObjectId);
                        break;
                }
            }
        }

        private static void ResolveTableBackedNames(IOrganizationService service, ComponentTypeDefinition definition, List<SolutionComponentItem> items)
        {
            var primaryKey = ComponentTypeCatalog.GetPrimaryKeyAttribute(definition);

            const int batchSize = 500;
            for (var offset = 0; offset < items.Count; offset += batchSize)
            {
                var batch = items.Skip(offset).Take(batchSize).ToList();
                var ids = batch.Select(i => (object)i.ObjectId).ToArray();

                var query = new QueryExpression(definition.EntityLogicalName)
                {
                    ColumnSet = new ColumnSet(primaryKey, definition.NameAttribute),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition(primaryKey, ConditionOperator.In, ids);

                var lookup = service.RetrieveMultiple(query).Entities
                    .ToDictionary(e => e.Id, e => e.GetAttributeValue<string>(definition.NameAttribute));

                foreach (var item in batch)
                {
                    if (lookup.TryGetValue(item.ObjectId, out var name) && !string.IsNullOrEmpty(name))
                    {
                        item.DisplayName = name;
                    }
                }
            }
        }

        public ComponentCopyResult CopyComponent(IOrganizationService service, SolutionComponentItem component, string targetSolutionUniqueName, bool addRequiredComponents)
        {
            try
            {
                var request = new AddSolutionComponentRequest
                {
                    ComponentId = component.ObjectId,
                    ComponentType = component.ComponentType,
                    SolutionUniqueName = targetSolutionUniqueName,
                    AddRequiredComponents = addRequiredComponents,
                    DoNotIncludeSubcomponents = false
                };

                service.Execute(request);

                return new ComponentCopyResult
                {
                    Component = component,
                    Outcome = CopyOutcome.Success,
                    Message = addRequiredComponents ? "Added with required components." : "Added."
                };
            }
            catch (Exception ex)
            {
                return new ComponentCopyResult
                {
                    Component = component,
                    Outcome = CopyOutcome.Failed,
                    Message = ex.Message
                };
            }
        }
    }
}
