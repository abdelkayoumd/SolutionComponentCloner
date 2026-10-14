using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace SolutionComponentCloner.Services
{
    /// <summary>
    /// Lazily loads and caches metadata-derived names (tables, columns, relationships, keys, global option
    /// sets) for a single connection/session. Table names come from a light table-only download; columns,
    /// relationships and keys are fetched only for the ids a solution actually contains.
    /// </summary>
    internal sealed class MetadataNameCache
    {
        private readonly Dictionary<Guid, string> _entities = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _attributes = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _relationships = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _keys = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _optionSets = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _entityLogicalNames = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _attributeTables = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _keyTables = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _relationshipTables = new Dictionary<Guid, string>();
        private readonly Dictionary<string, EntityMetadata> _entitiesByLogicalName = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
        private bool _entitiesLoaded;
        private bool _fullMetadataLoaded;
        private bool _keysLoaded;
        private bool _optionSetMetadataLoaded;

        public string GetEntityName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntities(service);
            return _entities.TryGetValue(metadataId, out var name) ? name : null;
        }

        public string GetEntityLogicalName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntities(service);
            return _entityLogicalNames.TryGetValue(metadataId, out var name) ? name : null;
        }

        /// <summary>
        /// Looks up a table's metadata (primary id/name attributes, etc.) by logical name, e.g. to resolve
        /// instance names for a custom/ISV component type that isn't in <see cref="ComponentTypeCatalog"/>.
        /// </summary>
        public EntityMetadata GetEntityMetadataByLogicalName(IOrganizationService service, string logicalName)
        {
            if (string.IsNullOrEmpty(logicalName))
            {
                return null;
            }

            EnsureEntities(service);
            return _entitiesByLogicalName.TryGetValue(logicalName, out var metadata) ? metadata : null;
        }

        /// <summary>The "Display name (logicalname)" label for a table, or just the logical name if its metadata is unknown.</summary>
        public string GetEntityLabel(IOrganizationService service, string logicalName)
        {
            var metadata = GetEntityMetadataByLogicalName(service, logicalName);
            return metadata == null ? logicalName : FormatLabel(metadata.DisplayName, metadata.LogicalName);
        }

        public string GetAttributeName(Guid metadataId) => _attributes.TryGetValue(metadataId, out var name) ? name : null;

        public string GetAttributeTable(Guid metadataId) => _attributeTables.TryGetValue(metadataId, out var table) ? table : null;

        public string GetRelationshipName(Guid metadataId) => _relationships.TryGetValue(metadataId, out var name) ? name : null;

        public string GetRelationshipTable(Guid metadataId) => _relationshipTables.TryGetValue(metadataId, out var table) ? table : null;

        public string GetEntityKeyName(Guid metadataId) => _keys.TryGetValue(metadataId, out var name) ? name : null;

        public string GetEntityKeyTable(Guid metadataId) => _keyTables.TryGetValue(metadataId, out var table) ? table : null;

        public string GetOptionSetName(IOrganizationService service, Guid metadataId)
        {
            EnsureOptionSetMetadata(service);
            return _optionSets.TryGetValue(metadataId, out var name) ? name : null;
        }

        public void PrefetchAttributes(IOrganizationService service, IEnumerable<Guid> ids)
        {
            var missing = ids.Where(id => !_attributes.ContainsKey(id)).Distinct().ToArray();
            if (missing.Length > 0)
            {
                LoadDetails(service, missing, attributes: true, relationships: false, keys: false);
            }
        }

        public void PrefetchRelationships(IOrganizationService service, IEnumerable<Guid> ids)
        {
            var missing = ids.Where(id => !_relationships.ContainsKey(id)).Distinct().ToArray();
            if (missing.Length > 0)
            {
                LoadDetails(service, missing, attributes: false, relationships: true, keys: false);
            }
        }

        public void PrefetchKeys(IOrganizationService service)
        {
            if (!_keysLoaded)
            {
                LoadDetails(service, new Guid[0], attributes: false, relationships: false, keys: true);
            }
        }

        private void EnsureEntities(IOrganizationService service)
        {
            if (_entitiesLoaded)
            {
                return;
            }

            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)service.Execute(request);
            foreach (var entity in response.EntityMetadata)
            {
                AbsorbEntityBasics(entity);
            }

            _entitiesLoaded = true;
        }

        private void LoadDetails(IOrganizationService service, Guid[] ids, bool attributes, bool relationships, bool keys)
        {
            if (_fullMetadataLoaded)
            {
                return;
            }

            try
            {
                var query = new EntityQueryExpression { Properties = new MetadataPropertiesExpression("LogicalName") };

                if (attributes)
                {
                    query.Properties.PropertyNames.Add("Attributes");
                    query.AttributeQuery = new AttributeQueryExpression
                    {
                        Criteria = IdFilter(ids),
                        Properties = new MetadataPropertiesExpression("LogicalName", "DisplayName", "MetadataId")
                    };
                }

                if (relationships)
                {
                    query.Properties.PropertyNames.Add("OneToManyRelationships");
                    query.Properties.PropertyNames.Add("ManyToOneRelationships");
                    query.Properties.PropertyNames.Add("ManyToManyRelationships");
                    query.RelationshipQuery = new RelationshipQueryExpression
                    {
                        Criteria = IdFilter(ids),
                        Properties = new MetadataPropertiesExpression("SchemaName", "MetadataId", "ReferencingEntity", "Entity1LogicalName")
                    };
                }

                if (keys)
                {
                    query.Properties.PropertyNames.Add("Keys");
                }

                var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });
                foreach (var entity in response.EntityMetadata)
                {
                    AbsorbEntityDetails(entity);
                }

                if (keys)
                {
                    _keysLoaded = true;
                }
            }
            catch (Exception)
            {
                // The targeted call is an optimisation; if the organization rejects it, fall back to the full download.
                LoadFullMetadata(service);
            }
        }

        private static MetadataFilterExpression IdFilter(Guid[] ids)
        {
            var filter = new MetadataFilterExpression(LogicalOperator.And);
            filter.Conditions.Add(new MetadataConditionExpression("MetadataId", MetadataConditionOperator.In, ids));
            return filter;
        }

        private void LoadFullMetadata(IOrganizationService service)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)service.Execute(request);
            foreach (var entity in response.EntityMetadata)
            {
                AbsorbEntityBasics(entity);
                AbsorbEntityDetails(entity);
            }

            _entitiesLoaded = true;
            _fullMetadataLoaded = true;
            _keysLoaded = true;
        }

        private void AbsorbEntityBasics(EntityMetadata entity)
        {
            if (entity.MetadataId.HasValue)
            {
                _entities[entity.MetadataId.Value] = FormatLabel(entity.DisplayName, entity.LogicalName);
                _entityLogicalNames[entity.MetadataId.Value] = entity.LogicalName;
            }

            if (!string.IsNullOrEmpty(entity.LogicalName))
            {
                _entitiesByLogicalName[entity.LogicalName] = entity;
            }
        }

        private void AbsorbEntityDetails(EntityMetadata entity)
        {
            if (entity.Attributes != null)
            {
                foreach (var attribute in entity.Attributes)
                {
                    if (attribute.MetadataId.HasValue)
                    {
                        _attributes[attribute.MetadataId.Value] = FormatLabel(attribute.DisplayName, attribute.LogicalName);
                        _attributeTables[attribute.MetadataId.Value] = entity.LogicalName;
                    }
                }
            }

            if (entity.Keys != null)
            {
                foreach (var key in entity.Keys)
                {
                    if (key.MetadataId.HasValue)
                    {
                        _keys[key.MetadataId.Value] = FormatLabel(key.DisplayName, key.LogicalName);
                        _keyTables[key.MetadataId.Value] = entity.LogicalName;
                    }
                }
            }

            AddRelationships(entity.OneToManyRelationships, entity.LogicalName);
            AddRelationships(entity.ManyToOneRelationships, entity.LogicalName);
            AddRelationships(entity.ManyToManyRelationships, entity.LogicalName);
        }

        private void AddRelationships(RelationshipMetadataBase[] relationships, string fallbackTable)
        {
            if (relationships == null)
            {
                return;
            }

            foreach (var relationship in relationships)
            {
                if (relationship.MetadataId.HasValue)
                {
                    _relationships[relationship.MetadataId.Value] = relationship.SchemaName;

                    // A relationship is filed under the table that holds the lookup (1:N), or the first table of an N:N.
                    string table;
                    if (relationship is OneToManyRelationshipMetadata oneToMany)
                    {
                        table = oneToMany.ReferencingEntity;
                    }
                    else if (relationship is ManyToManyRelationshipMetadata manyToMany)
                    {
                        table = manyToMany.Entity1LogicalName;
                    }
                    else
                    {
                        table = fallbackTable;
                    }

                    _relationshipTables[relationship.MetadataId.Value] = string.IsNullOrEmpty(table) ? fallbackTable : table;
                }
            }
        }

        private void EnsureOptionSetMetadata(IOrganizationService service)
        {
            if (_optionSetMetadataLoaded)
            {
                return;
            }

            var request = new RetrieveAllOptionSetsRequest();
            var response = (RetrieveAllOptionSetsResponse)service.Execute(request);

            foreach (var optionSet in response.OptionSetMetadata)
            {
                if (optionSet.MetadataId.HasValue)
                {
                    _optionSets[optionSet.MetadataId.Value] = FormatLabel(optionSet.DisplayName, optionSet.Name);
                }
            }

            _optionSetMetadataLoaded = true;
        }

        private static string FormatLabel(Label label, string logicalName)
        {
            var localized = label?.UserLocalizedLabel?.Label;
            return string.IsNullOrEmpty(localized) ? logicalName : $"{localized} ({logicalName})";
        }
    }
}
