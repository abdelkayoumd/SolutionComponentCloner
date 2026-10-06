using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace SolutionComponentCloner.Services
{
    /// <summary>
    /// Lazily loads and caches metadata-derived display names (entities, attributes,
    /// relationships, keys, global option sets) for a single connection/session, since a
    /// full metadata pull is expensive and the same organization is queried repeatedly
    /// while the user switches between source/target solutions.
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
        private bool _entityMetadataLoaded;
        private bool _optionSetMetadataLoaded;

        public string GetEntityName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _entities.TryGetValue(metadataId, out var name) ? name : null;
        }

        /// <summary>
        /// Looks up an entity's metadata (primary id/name attributes, etc.) by logical name,
        /// e.g. to resolve instance names for a custom/ISV component type that isn't in
        /// <see cref="ComponentTypeCatalog"/>.
        /// </summary>
        public EntityMetadata GetEntityMetadataByLogicalName(IOrganizationService service, string logicalName)
        {
            if (string.IsNullOrEmpty(logicalName))
            {
                return null;
            }

            EnsureEntityMetadata(service);
            return _entitiesByLogicalName.TryGetValue(logicalName, out var metadata) ? metadata : null;
        }

        public string GetAttributeName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _attributes.TryGetValue(metadataId, out var name) ? name : null;
        }

        public string GetRelationshipName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _relationships.TryGetValue(metadataId, out var name) ? name : null;
        }

        public string GetEntityKeyName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _keys.TryGetValue(metadataId, out var name) ? name : null;
        }

        public string GetEntityLogicalName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _entityLogicalNames.TryGetValue(metadataId, out var name) ? name : null;
        }

        public string GetAttributeTable(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _attributeTables.TryGetValue(metadataId, out var table) ? table : null;
        }

        public string GetEntityKeyTable(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _keyTables.TryGetValue(metadataId, out var table) ? table : null;
        }

        public string GetRelationshipTable(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _relationshipTables.TryGetValue(metadataId, out var table) ? table : null;
        }

        /// <summary>The "Display name (logicalname)" label for a table, or just the logical name if its metadata is unknown.</summary>
        public string GetEntityLabel(IOrganizationService service, string logicalName)
        {
            var metadata = GetEntityMetadataByLogicalName(service, logicalName);
            return metadata == null ? logicalName : FormatLabel(metadata.DisplayName, metadata.LogicalName);
        }

        public string GetOptionSetName(IOrganizationService service, Guid metadataId)
        {
            EnsureOptionSetMetadata(service);
            return _optionSets.TryGetValue(metadataId, out var name) ? name : null;
        }

        private void EnsureEntityMetadata(IOrganizationService service)
        {
            if (_entityMetadataLoaded)
            {
                return;
            }

            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)service.Execute(request);

            foreach (var entity in response.EntityMetadata)
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

            _entityMetadataLoaded = true;
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
