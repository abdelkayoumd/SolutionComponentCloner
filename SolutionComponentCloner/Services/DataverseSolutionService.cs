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
        private readonly DynamicComponentTypeCache _dynamicTypeCache = new DynamicComponentTypeCache();

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
                if (string.IsNullOrEmpty(row.ComponentTypeName))
                {
                    row.ComponentTypeName = ComponentTypeCatalog.GetTypeName(row.ComponentType);
                }
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
            try
            {
                if (ComponentTypeCatalog.Definitions.TryGetValue(componentType, out var definition))
                {
                    if (definition.IsMetadataBacked)
                    {
                        ResolveMetadataBackedNames(service, componentType, items);
                    }
                    else
                    {
                        var primaryKey = ComponentTypeCatalog.GetPrimaryKeyAttribute(definition);
                        ResolveTableBackedNames(service, definition.EntityLogicalName, primaryKey, definition.NameAttribute, items);
                    }
                }
                else
                {
                    // Not one of our hand-picked types (e.g. a custom/ISV-registered component
                    // type, like the high numeric values managed solutions register). Discover
                    // it generically via solutioncomponentdefinition + entity metadata instead of
                    // just showing "Component Type N".
                    ResolveDynamicComponentNames(service, componentType, items);
                }
            }
            catch (Exception)
            {
                // Leave DisplayName/ComponentTypeName unset for this group; caller falls back to
                // the raw id / component type number.
            }
        }

        private void ResolveDynamicComponentNames(IOrganizationService service, int componentType, List<SolutionComponentItem> items)
        {
            var definition = _dynamicTypeCache.GetDefinition(service, componentType);
            if (definition == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(definition.Value.Name))
            {
                foreach (var item in items)
                {
                    item.ComponentTypeName = definition.Value.Name;
                }
            }

            var primaryEntityName = definition.Value.PrimaryEntityName;
            if (string.IsNullOrEmpty(primaryEntityName))
            {
                return;
            }

            var entityMetadata = _metadataCache.GetEntityMetadataByLogicalName(service, primaryEntityName);
            if (entityMetadata?.PrimaryIdAttribute == null || entityMetadata.PrimaryNameAttribute == null)
            {
                return;
            }

            ResolveTableBackedNames(service, primaryEntityName, entityMetadata.PrimaryIdAttribute, entityMetadata.PrimaryNameAttribute, items);
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

        private static void ResolveTableBackedNames(IOrganizationService service, string entityLogicalName, string primaryKey, string nameAttribute, List<SolutionComponentItem> items)
        {
            const int batchSize = 500;
            for (var offset = 0; offset < items.Count; offset += batchSize)
            {
                var batch = items.Skip(offset).Take(batchSize).ToList();
                var ids = batch.Select(i => (object)i.ObjectId).ToArray();

                var query = new QueryExpression(entityLogicalName)
                {
                    ColumnSet = new ColumnSet(primaryKey, nameAttribute),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition(primaryKey, ConditionOperator.In, ids);

                var lookup = service.RetrieveMultiple(query).Entities
                    .ToDictionary(e => e.Id, e => e.GetAttributeValue<string>(nameAttribute));

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
                    AddRequiredComponents = addRequiredComponents
                };

                // DoNotIncludeSubcomponents is a separate switch from AddRequiredComponents: it
                // controls whether THIS component's own children (e.g. an entity's attributes,
                // forms, views) come along for the ride. Dataverse only accepts it for Entity-
                // rooted components — passing it for any other type (a view, an app setting,
                // etc.) throws "DoNotIncludeSubcomponents can not be set to true on non Entity
                // root ...". So only tie it to the checkbox when the component actually is an
                // Entity; every other type is a leaf and AddRequiredComponents alone governs it.
                if (component.ComponentType == ComponentTypeCatalog.Entity)
                {
                    request.DoNotIncludeSubcomponents = !addRequiredComponents;
                }

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
