using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "ismanaged", "version", "createdon"),
                Criteria = new FilterExpression(),
                Orders =
                {
                    new OrderExpression("createdon", OrderType.Descending),
                    new OrderExpression("friendlyname", OrderType.Ascending)
                }
            };
            query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);

            return RetrieveAll(service, query)
                .Select(e => new SolutionListItem
                {
                    SolutionId = e.Id,
                    UniqueName = e.GetAttributeValue<string>("uniquename"),
                    FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                    CreatedOn = e.GetAttributeValue<DateTime?>("createdon"),
                    IsManaged = e.GetAttributeValue<bool>("ismanaged"),
                    Version = e.GetAttributeValue<string>("version")
                })
                .ToList();
        }

        public SolutionComponentLoadResult GetSolutionComponents(IOrganizationService service, Guid solutionId, Action<string> progress = null)
        {
            var loadResult = new SolutionComponentLoadResult();
            progress?.Invoke("Reading the solution's components...");

            var rows = ReadSolutionComponentRows(service, solutionId)
                .Select(e => new SolutionComponentItem
                {
                    ObjectId = e.GetAttributeValue<Guid>("objectid"),
                    ComponentType = e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0,
                    RootComponentBehavior = e.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value
                })
                .ToList();

            loadResult.Items = ResolveItems(service, rows, loadResult.Warnings, progress);
            return loadResult;
        }

        private static List<Entity> ReadSolutionComponentRows(IOrganizationService service, Guid solutionId)
        {
            // "rootcomponentbehavior" says whether a table was added with all its subcomponents. Older
            // organizations may not have the column, so fall back to reading without it.
            foreach (var columns in new[] { new[] { "objectid", "componenttype", "rootcomponentbehavior" }, new[] { "objectid", "componenttype" } })
            {
                var query = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet(columns),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

                try
                {
                    return RetrieveAll(service, query);
                }
                catch (Exception) when (columns.Length == 3)
                {
                    // retry without the optional column
                }
            }

            return new List<Entity>();
        }

        /// <summary>Every component in a solution as (type, id) pairs, used to tell which components the target already has.</summary>
        public HashSet<(int Type, Guid Id)> GetSolutionComponentKeys(IOrganizationService service, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            return new HashSet<(int Type, Guid Id)>(RetrieveAll(service, query)
                .Select(e => (e.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0, e.GetAttributeValue<Guid>("objectid"))));
        }

        private List<SolutionComponentItem> ResolveItems(IOrganizationService service, List<SolutionComponentItem> rows, List<string> warnings, Action<string> progress)
        {
            var groups = rows.GroupBy(r => r.ComponentType).ToList();
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                progress?.Invoke($"Resolving names: {ComponentTypeCatalog.GetGroupName(group.Key, null)} ({group.Count()}), type {i + 1} of {groups.Count}...");
                ResolveDisplayNames(service, group.Key, group.ToList(), warnings);
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

            return rows
                .OrderBy(r => r.ComponentTypeName)
                .ThenBy(r => r.DisplayName)
                .ToList();
        }

        /// <summary>
        /// The components that would be pulled in by "Include required components" for the given selection,
        /// excluding anything already in the selection.
        /// </summary>
        public SolutionComponentLoadResult GetRequiredComponents(IOrganizationService service, IList<SolutionComponentItem> selected,
            Action<string> progress = null, Func<bool> isCancelled = null)
        {
            var result = new SolutionComponentLoadResult();
            var selectedKeys = new HashSet<(int, Guid)>(selected.Select(i => (i.ComponentType, i.ObjectId)));
            var requests = selected
                .Select(i => (OrganizationRequest)new RetrieveRequiredComponentsRequest { ObjectId = i.ObjectId, ComponentType = i.ComponentType })
                .ToList();

            var outcomes = ExecuteInBatches(service, requests, 20, done => progress?.Invoke($"Checking dependencies: {done} of {requests.Count}..."), isCancelled, out _);

            var found = new Dictionary<(int, Guid), SolutionComponentItem>();
            var failed = 0;
            foreach (var outcome in outcomes)
            {
                if (outcome.Error != null)
                {
                    failed++;
                    continue;
                }

                var collection = (outcome.Response as RetrieveRequiredComponentsResponse)?.EntityCollection;
                if (collection == null)
                {
                    continue;
                }

                foreach (var dependency in collection.Entities)
                {
                    var id = dependency.GetAttributeValue<Guid>("requiredcomponentobjectid");
                    var type = dependency.GetAttributeValue<OptionSetValue>("requiredcomponenttype")?.Value ?? 0;
                    if (id == Guid.Empty || selectedKeys.Contains((type, id)) || found.ContainsKey((type, id)))
                    {
                        continue;
                    }

                    found[(type, id)] = new SolutionComponentItem { ObjectId = id, ComponentType = type };
                }
            }

            if (failed > 0)
            {
                result.Warnings.Add($"{failed} component(s) could not be checked for dependencies.");
            }

            result.Items = ResolveItems(service, found.Values.ToList(), result.Warnings, progress);
            return result;
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

            // Best effort: some custom types (command bar commands, custom messages) belong to a table. If the
            // table column is not there, fall back to names only rather than losing the whole type.
            var applier = GetDynamicHierarchyApplier(primaryEntityName, out var hintColumns);
            if (applier != null)
            {
                try
                {
                    ResolveTableBackedNames(service, primaryEntityName, entityMetadata.PrimaryIdAttribute, entityMetadata.PrimaryNameAttribute, items, hintColumns, applier);
                    return;
                }
                catch (Exception)
                {
                    // fall through to the plain lookup
                }
            }

            ResolveTableBackedNames(service, primaryEntityName, entityMetadata.PrimaryIdAttribute, entityMetadata.PrimaryNameAttribute, items);
        }

        private static Action<SolutionComponentItem, Entity> GetDynamicHierarchyApplier(string primaryEntityName, out string[] extraColumns)
        {
            switch ((primaryEntityName ?? string.Empty).ToLowerInvariant())
            {
                case "customapi":
                    extraColumns = new[] { "boundentitylogicalname" };
                    return (item, e) => Place(item, e.GetAttributeValue<string>("boundentitylogicalname"), "Messages", null);
                case "appaction":
                    extraColumns = new[] { "contextentity" };
                    return (item, e) => Place(item, e.GetAttributeValue<string>("contextentity"), "Commands", null);
                default:
                    extraColumns = null;
                    return null;
            }
        }

        private void ResolveMetadataBackedNames(IOrganizationService service, int componentType, List<SolutionComponentItem> items)
        {
            // Fetch only the columns, relationships or keys this solution actually contains.
            switch (componentType)
            {
                case ComponentTypeCatalog.Attribute:
                    _metadataCache.PrefetchAttributes(service, items.Select(i => i.ObjectId));
                    break;
                case ComponentTypeCatalog.Relationship:
                    _metadataCache.PrefetchRelationships(service, items.Select(i => i.ObjectId));
                    break;
                case ComponentTypeCatalog.EntityKey:
                    _metadataCache.PrefetchKeys(service);
                    break;
            }

            foreach (var item in items)
            {
                switch (componentType)
                {
                    case ComponentTypeCatalog.Entity:
                        item.DisplayName = _metadataCache.GetEntityName(service, item.ObjectId);
                        item.TableName = _metadataCache.GetEntityLogicalName(service, item.ObjectId);
                        item.TableLabel = item.DisplayName;
                        item.IsManaged = _metadataCache.GetEntityIsManaged(service, item.ObjectId);
                        break;
                    case ComponentTypeCatalog.Attribute:
                        item.DisplayName = _metadataCache.GetAttributeName(item.ObjectId);
                        item.TableName = _metadataCache.GetAttributeTable(item.ObjectId);
                        item.Category = "Columns";
                        break;
                    case ComponentTypeCatalog.Relationship:
                        item.DisplayName = _metadataCache.GetRelationshipName(item.ObjectId);
                        item.TableName = _metadataCache.GetRelationshipTable(item.ObjectId);
                        item.Category = "Relationships";
                        break;
                    case ComponentTypeCatalog.EntityKey:
                        item.DisplayName = _metadataCache.GetEntityKeyName(item.ObjectId);
                        item.TableName = _metadataCache.GetEntityKeyTable(item.ObjectId);
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
                        var category = e.GetAttributeValue<OptionSetValue>("category")?.Value;
                        if (category == 2)
                        {
                            Place(item, e.GetAttributeValue<string>("primaryentity"), "Business rules", null);
                            return;
                        }

                        // Cloud flows, desktop flows and the like live in the workflow table; the category tells them apart.
                        var kind = WorkflowKind(category);
                        if (kind != null)
                        {
                            item.GroupName = kind.Value.Group;
                            item.ComponentTypeName = kind.Value.Name;
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

        /// <summary>Maps a workflow row's category to its maker-portal folder and type name; null keeps it under "Processes".</summary>
        private static (string Group, string Name)? WorkflowKind(int? category)
        {
            switch (category)
            {
                case 3: return ("Actions", "Action");
                case 4: return ("Business process flows", "Business Process Flow");
                case 5: return ("Cloud flows", "Cloud Flow");
                case 6: return ("Desktop flows", "Desktop Flow");
                default: return null;
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

        // Modified date, modified by and managed state are nice to have but not every table has all three, so each table
        // is tried with all of them, then without "ismanaged", then without any, and the result is remembered.
        private static readonly string[][] CommonColumnLevels =
        {
            new[] { "modifiedon", "modifiedby", "ismanaged" },
            new[] { "modifiedon", "modifiedby" },
            new string[0]
        };

        private readonly Dictionary<string, int> _commonColumnLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private void ResolveTableBackedNames(IOrganizationService service, string entityLogicalName, string primaryKey, string nameAttribute, List<SolutionComponentItem> items,
            string[] extraColumns = null, Action<SolutionComponentItem, Entity> applyExtra = null)
        {
            const int batchSize = 500;
            for (var offset = 0; offset < items.Count; offset += batchSize)
            {
                var batch = items.Skip(offset).Take(batchSize).ToList();
                var ids = batch.Select(i => (object)i.ObjectId).ToArray();

                var required = new List<string> { primaryKey, nameAttribute };
                if (extraColumns != null)
                {
                    required.AddRange(extraColumns);
                }

                var lookup = QueryByIds(service, entityLogicalName, primaryKey, required, ids).ToDictionary(e => e.Id);

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

                    ApplyCommonColumns(item, entity);
                    applyExtra?.Invoke(item, entity);
                }
            }
        }

        private IEnumerable<Entity> QueryByIds(IOrganizationService service, string entityLogicalName, string primaryKey, List<string> requiredColumns, object[] ids)
        {
            _commonColumnLevel.TryGetValue(entityLogicalName, out var level);

            for (; level < CommonColumnLevels.Length; level++)
            {
                var columns = requiredColumns.Concat(CommonColumnLevels[level]).Distinct().ToArray();
                var query = new QueryExpression(entityLogicalName)
                {
                    ColumnSet = new ColumnSet(columns),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition(primaryKey, ConditionOperator.In, ids);

                try
                {
                    var entities = service.RetrieveMultiple(query).Entities;
                    _commonColumnLevel[entityLogicalName] = level;
                    return entities;
                }
                catch (Exception) when (level < CommonColumnLevels.Length - 1)
                {
                    // This table lacks one of the optional columns; try the next, smaller set.
                }
            }

            return Enumerable.Empty<Entity>();
        }

        private static void ApplyCommonColumns(SolutionComponentItem item, Entity entity)
        {
            if (entity.Contains("modifiedon"))
            {
                item.ModifiedOn = entity.GetAttributeValue<DateTime?>("modifiedon")?.ToLocalTime();
            }

            if (entity.Contains("modifiedby"))
            {
                item.ModifiedBy = entity.FormattedValues.Contains("modifiedby")
                    ? entity.FormattedValues["modifiedby"]
                    : entity.GetAttributeValue<EntityReference>("modifiedby")?.Name;
            }

            if (entity.Contains("ismanaged"))
            {
                item.IsManaged = entity.GetAttributeValue<bool?>("ismanaged");
            }
        }

        /// <summary>Runs requests through ExecuteMultiple in batches, returning one outcome per request that was attempted.</summary>
        private static List<(OrganizationResponse Response, string Error)> ExecuteInBatches(IOrganizationService service, IList<OrganizationRequest> requests,
            int batchSize, Action<int> progress, Func<bool> isCancelled, out bool cancelled)
        {
            var outcomes = new List<(OrganizationResponse, string)>();
            cancelled = false;

            for (var offset = 0; offset < requests.Count; offset += batchSize)
            {
                if (isCancelled != null && isCancelled())
                {
                    cancelled = true;
                    break;
                }

                var batch = requests.Skip(offset).Take(batchSize).ToList();

                try
                {
                    var multiple = new ExecuteMultipleRequest
                    {
                        Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
                        Requests = new OrganizationRequestCollection()
                    };
                    multiple.Requests.AddRange(batch);

                    var response = (ExecuteMultipleResponse)service.Execute(multiple);
                    var byIndex = response.Responses.ToDictionary(r => r.RequestIndex);
                    for (var i = 0; i < batch.Count; i++)
                    {
                        if (byIndex.TryGetValue(i, out var item))
                        {
                            outcomes.Add(item.Fault != null ? ((OrganizationResponse)null, item.Fault.Message) : (item.Response, (string)null));
                        }
                        else
                        {
                            outcomes.Add((null, "No response was returned for this request."));
                        }
                    }
                }
                catch (Exception)
                {
                    // Batching is an optimisation; if the organization rejects it, run these requests one by one.
                    foreach (var request in batch)
                    {
                        try
                        {
                            outcomes.Add((service.Execute(request), null));
                        }
                        catch (Exception ex)
                        {
                            outcomes.Add((null, ex.Message));
                        }
                    }
                }

                progress?.Invoke(outcomes.Count);
            }

            return outcomes;
        }

        /// <summary>
        /// Adds the components to the target in ExecuteMultiple batches. Stops between batches when
        /// <paramref name="isCancelled"/> returns true; the results then cover only what was attempted.
        /// </summary>
        public List<ComponentCopyResult> CopyComponents(IOrganizationService service, IList<SolutionComponentItem> components, string targetSolutionUniqueName,
            bool addRequiredComponents, Action<int, int> progress, Func<bool> isCancelled, out bool cancelled)
        {
            var requests = components
                .Select(c => (OrganizationRequest)BuildAddRequest(c, targetSolutionUniqueName, addRequiredComponents))
                .ToList();

            var outcomes = ExecuteInBatches(service, requests, 20, done => progress?.Invoke(done, components.Count), isCancelled, out cancelled);

            var results = new List<ComponentCopyResult>();
            for (var i = 0; i < outcomes.Count; i++)
            {
                results.Add(new ComponentCopyResult
                {
                    Component = components[i],
                    Outcome = outcomes[i].Error == null ? CopyOutcome.Success : CopyOutcome.Failed,
                    Message = outcomes[i].Error ?? (addRequiredComponents ? "Added with required components." : "Added.")
                });
            }

            return results;
        }

        public void PublishAll(IOrganizationService service)
        {
            service.Execute(new PublishAllXmlRequest());
        }

        public List<PublisherListItem> GetPublishers(IOrganizationService service)
        {
            var query = new QueryExpression("publisher")
            {
                ColumnSet = new ColumnSet("publisherid", "friendlyname", "uniquename", "customizationprefix"),
                Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
            };

            return RetrieveAll(service, query)
                .Select(e => new PublisherListItem
                {
                    PublisherId = e.Id,
                    FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                    UniqueName = e.GetAttributeValue<string>("uniquename"),
                    Prefix = e.GetAttributeValue<string>("customizationprefix")
                })
                .ToList();
        }

        public Guid CreateSolution(IOrganizationService service, string friendlyName, string uniqueName, string version, Guid publisherId)
        {
            var solution = new Entity("solution");
            solution["friendlyname"] = friendlyName;
            solution["uniquename"] = uniqueName;
            solution["version"] = version;
            solution["publisherid"] = new EntityReference("publisher", publisherId);
            return service.Create(solution);
        }

        private static AddSolutionComponentRequest BuildAddRequest(SolutionComponentItem component, string targetSolutionUniqueName, bool addRequiredComponents)
        {
            var request = new AddSolutionComponentRequest
            {
                ComponentId = component.ObjectId,
                ComponentType = component.ComponentType,
                SolutionUniqueName = targetSolutionUniqueName,
                AddRequiredComponents = addRequiredComponents
            };

            // DoNotIncludeSubcomponents is a separate switch from AddRequiredComponents: it controls whether THIS
            // component's own children (e.g. a table's columns, forms, views) come along. Dataverse only accepts it
            // for Entity-rooted components, so it is only ever set for tables; each table can override the checkbox.
            if (component.ComponentType == ComponentTypeCatalog.Entity)
            {
                switch (component.SubcomponentMode)
                {
                    case SubcomponentMode.IncludeAll:
                        request.DoNotIncludeSubcomponents = false;
                        break;
                    case SubcomponentMode.OnlySelected:
                        request.DoNotIncludeSubcomponents = true;
                        break;
                    default:
                        request.DoNotIncludeSubcomponents = !addRequiredComponents;
                        break;
                }
            }

            return request;
        }

        public ComponentCopyResult CopyComponent(IOrganizationService service, SolutionComponentItem component, string targetSolutionUniqueName, bool addRequiredComponents)
        {
            try
            {
                service.Execute(BuildAddRequest(component, targetSolutionUniqueName, addRequiredComponents));

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
