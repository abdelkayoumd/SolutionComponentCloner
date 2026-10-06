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

            return RetrieveAll(service, query)
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

        public SolutionComponentLoadResult GetSolutionComponents(IOrganizationService service, Guid solutionId)
        {
            var loadResult = new SolutionComponentLoadResult();

            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            var rows = RetrieveAll(service, query)
                .Select(e => new SolutionComponentItem
                {
                    ObjectId = e.GetAttributeValue<Guid>("objectid"),
                    ComponentType = e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0
                })
                .ToList();

            foreach (var group in rows.GroupBy(r => r.ComponentType))
            {
                ResolveDisplayNames(service, group.Key, group.ToList(), loadResult.Warnings);
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

                if (row.TableName != null)
                {
                    row.GroupName = ComponentTypeCatalog.TablesGroup;
                }
                else if (string.IsNullOrEmpty(row.GroupName))
                {
                    row.GroupName = ComponentTypeCatalog.GetGroupName(row.ComponentType, row.ComponentTypeName);
                }
            }

            loadResult.Items = rows
                .OrderBy(r => r.ComponentTypeName)
                .ThenBy(r => r.DisplayName)
                .ToList();
            return loadResult;
        }

        private static List<Entity> RetrieveAll(IOrganizationService service, QueryExpression query)
        {
            query.PageInfo = new PagingInfo { PageNumber = 1, Count = 5000 };

            var results = new List<Entity>();
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                results.AddRange(page.Entities);

                if (!page.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            return results;
        }

        private void ResolveDisplayNames(IOrganizationService service, int componentType, List<SolutionComponentItem> items, List<string> warnings)
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
                        var applier = GetHierarchyApplier(componentType, out var extraColumns);
                        ResolveTableBackedNames(service, definition.EntityLogicalName, primaryKey, definition.NameAttribute, items, extraColumns, applier);
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

                foreach (var item in items.Where(i => i.TableName != null && i.TableLabel == null))
                {
                    item.TableLabel = _metadataCache.GetEntityLabel(service, item.TableName);
                }
            }
            catch (Exception ex)
            {
                // Leave DisplayName/ComponentTypeName unset for this group; the caller falls back
                // to the raw id / component type number, and the warning tells the user why.
                warnings.Add($"{ComponentTypeCatalog.GetTypeName(componentType)} ({items.Count}): {ex.Message}");
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
                        item.TableName = _metadataCache.GetEntityLogicalName(service, item.ObjectId);
                        item.TableLabel = item.DisplayName;
                        break;
                    case ComponentTypeCatalog.Attribute:
                        item.DisplayName = _metadataCache.GetAttributeName(service, item.ObjectId);
                        item.TableName = _metadataCache.GetAttributeTable(service, item.ObjectId);
                        item.Category = "Columns";
                        break;
                    case ComponentTypeCatalog.Relationship:
                        item.DisplayName = _metadataCache.GetRelationshipName(service, item.ObjectId);
                        item.TableName = _metadataCache.GetRelationshipTable(service, item.ObjectId);
                        item.Category = "Relationships";
                        break;
                    case ComponentTypeCatalog.EntityKey:
                        item.DisplayName = _metadataCache.GetEntityKeyName(service, item.ObjectId);
                        item.TableName = _metadataCache.GetEntityKeyTable(service, item.ObjectId);
                        item.Category = "Keys";
                        break;
                    case ComponentTypeCatalog.OptionSet:
                        item.DisplayName = _metadataCache.GetOptionSetName(service, item.ObjectId);
                        break;
                }
            }
        }

        /// <summary>
        /// For component types that live inside a table (forms, views, charts, business rules) or have a
        /// sub-folder (web resources), returns the extra columns to read and how to file each item.
        /// </summary>
        private static Action<SolutionComponentItem, Entity> GetHierarchyApplier(int componentType, out string[] extraColumns)
        {
            switch (componentType)
            {
                case ComponentTypeCatalog.Form:
                    extraColumns = new[] { "objecttypecode", "type" };
                    return (item, e) =>
                    {
                        var formType = e.GetAttributeValue<OptionSetValue>("type")?.Value;
                        var isDashboard = formType == 0 || formType == 10;
                        Place(item, e.GetAttributeValue<string>("objecttypecode"), isDashboard ? "Dashboards" : "Forms", isDashboard ? "Dashboards" : null);
                    };
                case ComponentTypeCatalog.View:
                    extraColumns = new[] { "returnedtypecode" };
                    return (item, e) => Place(item, e.GetAttributeValue<string>("returnedtypecode"), "Views", null);
                case ComponentTypeCatalog.Chart:
                    extraColumns = new[] { "primaryentitytypecode" };
                    return (item, e) => Place(item, e.GetAttributeValue<string>("primaryentitytypecode"), "Charts", null);
                case ComponentTypeCatalog.Workflow:
                    extraColumns = new[] { "primaryentity", "category" };
                    return (item, e) =>
                    {
                        if (e.GetAttributeValue<OptionSetValue>("category")?.Value == 2)
                        {
                            Place(item, e.GetAttributeValue<string>("primaryentity"), "Business rules", null);
                        }
                    };
                case ComponentTypeCatalog.WebResource:
                    extraColumns = new[] { "webresourcetype" };
                    return (item, e) =>
                    {
                        var type = e.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value;
                        item.SubGroup = type.HasValue ? ComponentTypeCatalog.GetWebResourceSubGroup(type.Value) : "Other";
                    };
                default:
                    extraColumns = null;
                    return null;
            }
        }

        private static void Place(SolutionComponentItem item, string table, string category, string flatGroup)
        {
            if (!string.IsNullOrEmpty(table) && !string.Equals(table, "none", StringComparison.OrdinalIgnoreCase))
            {
                item.TableName = table;
                item.Category = category;
            }
            else if (flatGroup != null)
            {
                item.GroupName = flatGroup;
            }
        }

        private static void ResolveTableBackedNames(IOrganizationService service, string entityLogicalName, string primaryKey, string nameAttribute, List<SolutionComponentItem> items,
            string[] extraColumns = null, Action<SolutionComponentItem, Entity> applyExtra = null)
        {
            const int batchSize = 500;
            for (var offset = 0; offset < items.Count; offset += batchSize)
            {
                var batch = items.Skip(offset).Take(batchSize).ToList();
                var ids = batch.Select(i => (object)i.ObjectId).ToArray();

                var columns = new List<string> { primaryKey, nameAttribute };
                if (extraColumns != null)
                {
                    columns.AddRange(extraColumns);
                }

                var query = new QueryExpression(entityLogicalName)
                {
                    ColumnSet = new ColumnSet(columns.ToArray()),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition(primaryKey, ConditionOperator.In, ids);

                var lookup = service.RetrieveMultiple(query).Entities.ToDictionary(e => e.Id);

                foreach (var item in batch)
                {
                    if (!lookup.TryGetValue(item.ObjectId, out var entity))
                    {
                        continue;
                    }

                    var name = entity.GetAttributeValue<string>(nameAttribute);
                    if (!string.IsNullOrEmpty(name))
                    {
                        item.DisplayName = name;
                    }

                    applyExtra?.Invoke(item, entity);
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
