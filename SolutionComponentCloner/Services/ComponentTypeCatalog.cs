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
        public const int ModelDrivenApp = 80;

        public static readonly Dictionary<int, ComponentTypeDefinition> Definitions = new Dictionary<int, ComponentTypeDefinition>
        {
            { Entity, new ComponentTypeDefinition("Entity", null, null) },
            { Attribute, new ComponentTypeDefinition("Attribute", null, null) },
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
            { ModelDrivenApp, new ComponentTypeDefinition("Model-Driven App", "appmodule", "name") },
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

        public const string TablesGroup = "Tables";
        public const int WebResource = 61;
        public const int Workflow = 29;
        public const int View = 26;
        public const int Chart = 59;

        // Plural folder names, matching the Power Apps solution explorer.
        private static readonly Dictionary<int, string> GroupNames = new Dictionary<int, string>
        {
            { 1, TablesGroup }, { 2, "Columns" }, { 9, "Option sets" }, { 10, "Relationships" }, { 14, "Keys" },
            { 18, "Queues" }, { 20, "Security roles" }, { 26, "Views" }, { 29, "Processes" }, { 31, "Reports" },
            { 36, "Email templates" }, { 44, "Duplicate detection rules" }, { 59, "Charts" }, { 60, "Forms" },
            { 61, "Web resources" }, { 62, "Site maps" }, { 63, "Connection roles" }, { 65, "Field security profiles" },
            { 68, "Plug-in types" }, { 69, "Plug-in assemblies" }, { 70, "Plug-in steps" }, { 71, "Plug-in step images" },
            { 72, "Service endpoints" }, { 80, "Model-driven apps" }, { 92, "SLAs" }, { 150, "Mobile offline profiles" },
            { 152, "Similarity rules" }, { 154, "Custom controls" }, { 201, "Custom APIs" },
            { 202, "Custom API request parameters" }, { 203, "Custom API response properties" }
        };

        public static string GetTypeName(int componentType)
        {
            return Definitions.TryGetValue(componentType, out var def) ? def.DisplayName : $"Component Type {componentType}";
        }

        public static string GetGroupName(int componentType, string fallbackTypeName)
        {
            if (GroupNames.TryGetValue(componentType, out var name))
            {
                return name;
            }

            return string.IsNullOrEmpty(fallbackTypeName) ? GetTypeName(componentType) : fallbackTypeName;
        }

        /// <summary>Maps a web resource's webresourcetype value to the Code / Data / Images folders the maker portal uses.</summary>
        public static string GetWebResourceSubGroup(int webResourceType)
        {
            switch (webResourceType)
            {
                case 1: case 2: case 3: case 8:
                    return "Code";
                case 4: case 9: case 12:
                    return "Data";
                case 5: case 6: case 7: case 10: case 11:
                    return "Images";
                default:
                    return "Other";
            }
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
