using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SolutionComponentCloner.Services
{
    /// <summary>
    /// Resolves component types that aren't in <see cref="ComponentTypeCatalog"/> — most often
    /// custom types registered by an ISV/managed solution (values well outside the range
    /// Microsoft documents) — via the "solutioncomponentdefinition" table, which Dataverse
    /// itself maintains for every component type, including custom ones. Falls back to an
    /// empty cache (and the caller's raw "Component Type N" label) if the table can't be read.
    /// </summary>
    internal sealed class DynamicComponentTypeCache
    {
        public readonly struct Definition
        {
            public Definition(string name, string primaryEntityName)
            {
                Name = name;
                PrimaryEntityName = primaryEntityName;
            }

            public string Name { get; }
            public string PrimaryEntityName { get; }
        }

        private readonly Dictionary<int, Definition> _definitions = new Dictionary<int, Definition>();
        private bool _loaded;

        public Definition? GetDefinition(IOrganizationService service, int componentType)
        {
            EnsureLoaded(service);
            return _definitions.TryGetValue(componentType, out var definition) ? definition : (Definition?)null;
        }

        private void EnsureLoaded(IOrganizationService service)
        {
            if (_loaded)
            {
                return;
            }

            // "primaryentityname" is the less certain of these columns (it's not consistently
            // documented), so if it turns out to be wrong, retry without it rather than losing
            // type-name resolution entirely.
            if (!TryLoad(service, "objecttypecode", "name", "primaryentityname"))
            {
                TryLoad(service, "objecttypecode", "name");
            }

            _loaded = true;
        }

        private bool TryLoad(IOrganizationService service, params string[] columns)
        {
            try
            {
                var query = new QueryExpression("solutioncomponentdefinition")
                {
                    ColumnSet = new ColumnSet(columns)
                };

                foreach (var entity in service.RetrieveMultiple(query).Entities)
                {
                    var code = entity.GetAttributeValue<int?>("objecttypecode");
                    if (!code.HasValue)
                    {
                        continue;
                    }

                    _definitions[code.Value] = new Definition(
                        entity.GetAttributeValue<string>("name"),
                        entity.Contains("primaryentityname") ? entity.GetAttributeValue<string>("primaryentityname") : null);
                }

                return true;
            }
            catch (Exception)
            {
                _definitions.Clear();
                return false;
            }
        }
    }
}
