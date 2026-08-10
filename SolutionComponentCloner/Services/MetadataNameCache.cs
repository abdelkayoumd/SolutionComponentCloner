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
        private bool _entityMetadataLoaded;
        private bool _optionSetMetadataLoaded;

        public string GetEntityName(IOrganizationService service, Guid metadataId)
        {
            EnsureEntityMetadata(service);
            return _entities.TryGetValue(metadataId, out var name) ? name : null;
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
                }

                if (entity.Attributes != null)
                {
                    foreach (var attribute in entity.Attributes)
                    {
                        if (attribute.MetadataId.HasValue)
                        {
                            _attributes[attribute.MetadataId.Value] =
                                $"{entity.LogicalName}.{FormatLabel(attribute.DisplayName, attribute.LogicalName)}";
                        }
                    }
                }

                if (entity.Keys != null)
                {
                    foreach (var key in entity.Keys)
                    {
                        if (key.MetadataId.HasValue)
                        {
                            _keys[key.MetadataId.Value] = $"{entity.LogicalName}.{FormatLabel(key.DisplayName, key.LogicalName)}";
                        }
                    }
                }

                AddRelationships(entity.OneToManyRelationships);
                AddRelationships(entity.ManyToOneRelationships);
                AddRelationships(entity.ManyToManyRelationships);
            }

            _entityMetadataLoaded = true;
        }

        private void AddRelationships(RelationshipMetadataBase[] relationships)
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
