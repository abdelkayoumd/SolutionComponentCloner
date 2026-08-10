using System.Collections.Generic;

namespace SolutionComponentCloner.Services
{
    /// <summary>
    /// Describes how to resolve a friendly display name for a solutioncomponent row.
    /// Types with a null EntityLogicalName are resolved through metadata services instead
    /// of a plain RetrieveMultiple (entities, attributes, relationships, keys, global option sets).
    /// </summary>
    internal sealed class ComponentTypeDefinition
    {
        public ComponentTypeDefinition(string displayName, string entityLogicalName, string nameAttribute)
        {
            DisplayName = displayName;
            EntityLogicalName = entityLogicalName;
            NameAttribute = nameAttribute;
        }

        public string DisplayName { get; }
        public string EntityLogicalName { get; }
        public string NameAttribute { get; }
        public bool IsMetadataBacked => EntityLogicalName == null;
    }

    internal static class ComponentTypeCatalog
    {
        public const int Entity = 1;
        public const int Attribute = 2;
        public const int Relationship = 10;
        public const int OptionSet = 9;
        public const int EntityKey = 14;
        public const int Form = 60;

        public static readonly Dictionary<int, ComponentTypeDefinition> Definitions = new Dictionary<int, ComponentTypeDefinition>
        {
            { Entity, new ComponentTypeDefinition("Entity", null, null) },
            { Attribute, new ComponentTypeDefinition("Attribute", null, null) },
            { 3, new ComponentTypeDefinition("Relationship", null, null) },
            { OptionSet, new ComponentTypeDefinition("Option Set", null, null) },
            { Relationship, new ComponentTypeDefinition("Entity Relationship", null, null) },
            { EntityKey, new ComponentTypeDefinition("Entity Key", null, null) },
            { 18, new ComponentTypeDefinition("Queue", "queue", "name") },
            { 20, new ComponentTypeDefinition("Security Role", "role", "name") },
            { 26, new ComponentTypeDefinition("View", "savedquery", "name") },
            { 29, new ComponentTypeDefinition("Process / Workflow", "workflow", "name") },
            { 31, new ComponentTypeDefinition("Report", "report", "name") },
            { 36, new ComponentTypeDefinition("Email Template", "template", "title") },
            { 44, new ComponentTypeDefinition("Duplicate Detection Rule", "duplicaterule", "name") },
            { 59, new ComponentTypeDefinition("Chart", "savedqueryvisualization", "name") },
            { Form, new ComponentTypeDefinition("Form", "systemform", "name") },
            { 61, new ComponentTypeDefinition("Web Resource", "webresource", "name") },
            { 62, new ComponentTypeDefinition("Site Map", "sitemap", "sitemapnameunique") },
            { 63, new ComponentTypeDefinition("Connection Role", "connectionrole", "name") },
            { 65, new ComponentTypeDefinition("Field Security Profile", "fieldsecurityprofile", "name") },
            { 68, new ComponentTypeDefinition("Plug-in Type", "plugintype", "name") },
            { 69, new ComponentTypeDefinition("Plug-in Assembly", "pluginassembly", "name") },
            { 70, new ComponentTypeDefinition("SDK Message Processing Step", "sdkmessageprocessingstep", "name") },
            { 71, new ComponentTypeDefinition("SDK Message Processing Step Image", "sdkmessageprocessingstepimage", "name") },
            { 72, new ComponentTypeDefinition("Service Endpoint", "serviceendpoint", "name") },
            // A whole Model-Driven App. Its AppModuleComponents list every entity/form/view/
            // process the app is built from — copying this component always brings all of
            // those along, since they're the app's own definition, not optional dependencies
            // AddRequiredComponents can skip.
            { 80, new ComponentTypeDefinition("Model-Driven App", "appmodule", "name") },
            { 92, new ComponentTypeDefinition("SLA", "sla", "name") },
            { 150, new ComponentTypeDefinition("Mobile Offline Profile", "mobileofflineprofile", "name") },
            { 152, new ComponentTypeDefinition("Similarity Rule", "similarityrule", "name") },
            { 154, new ComponentTypeDefinition("Custom Control", "customcontrol", "name") },
            { 201, new ComponentTypeDefinition("Custom API", "customapi", "name") },
            { 202, new ComponentTypeDefinition("Custom API Request Parameter", "customapirequestparameter", "name") },
            { 203, new ComponentTypeDefinition("Custom API Response Property", "customapiresponseproperty", "name") },
        };

        // Most Dataverse tables key on "{logicalname}id", but a handful of long-standing
        // exceptions don't follow that convention.
        private static readonly Dictionary<string, string> PrimaryKeyOverrides = new Dictionary<string, string>
        {
            { "systemform", "formid" }
        };

        public static string GetTypeName(int componentType)
        {
            return Definitions.TryGetValue(componentType, out var def) ? def.DisplayName : $"Component Type {componentType}";
        }

        public static string GetPrimaryKeyAttribute(ComponentTypeDefinition definition)
        {
            if (definition.EntityLogicalName != null && PrimaryKeyOverrides.TryGetValue(definition.EntityLogicalName, out var pk))
            {
                return pk;
            }

            return definition.EntityLogicalName + "id";
        }
    }
}
