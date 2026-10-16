using System.Collections.Generic;
using System.Linq;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ComponentTypeCatalogTests
    {
        // componenttype values and labels from Microsoft's solutioncomponent table reference
        // (learn.microsoft.com/power-apps/developer/data-platform/reference/entities/solutioncomponent),
        // paired with the name this tool shows. 80 (app module) is not in that list and is checked separately.
        private static readonly Dictionary<int, (string Official, string Shown)> Verified = new Dictionary<int, (string, string)>
        {
            { 1, ("Entity", "Entity") }, { 2, ("Attribute", "Attribute") }, { 9, ("Option Set", "Option Set") },
            { 10, ("Entity Relationship", "Entity Relationship") }, { 14, ("Entity Key", "Entity Key") },
            { 20, ("Role", "Security Role") }, { 26, ("Saved Query", "View") }, { 29, ("Workflow", "Process / Workflow") },
            { 31, ("Report", "Report") }, { 36, ("Email Template", "Email Template") },
            { 44, ("Duplicate Rule", "Duplicate Detection Rule") }, { 59, ("Saved Query Visualization", "Chart") },
            { 60, ("System Form", "Form") }, { 61, ("Web Resource", "Web Resource") }, { 62, ("Site Map", "Site Map") },
            { 63, ("Connection Role", "Connection Role") }, { 66, ("Custom Control", "Custom Control") },
            { 70, ("Field Security Profile", "Field Security Profile") }, { 90, ("Plugin Type", "Plug-in Type") },
            { 91, ("Plugin Assembly", "Plug-in Assembly") }, { 92, ("SDK Message Processing Step", "SDK Message Processing Step") },
            { 93, ("SDK Message Processing Step Image", "SDK Message Processing Step Image") },
            { 95, ("Service Endpoint", "Service Endpoint") }, { 152, ("SLA", "SLA") },
            { 161, ("Mobile Offline Profile", "Mobile Offline Profile") }, { 165, ("Similarity Rule", "Similarity Rule") },
            { 300, ("Canvas App", "Canvas App") }, { 371, ("Connector", "Connector") }, { 372, ("Connector", "Connector") },
            { 380, ("Environment Variable Definition", "Environment Variable Definition") }
        };

        [Fact]
        public void EveryCatalogTypeNumberMatchesMicrosoftsPublishedList()
        {
            var unverified = ComponentTypeCatalog.Definitions.Keys
                .Where(k => k != ComponentTypeCatalog.ModelDrivenApp && !Verified.ContainsKey(k))
                .ToList();

            Assert.Empty(unverified);
        }

        [Fact]
        public void VerifiedTypesShowTheExpectedName()
        {
            foreach (var pair in Verified)
            {
                Assert.True(ComponentTypeCatalog.Definitions.ContainsKey(pair.Key), $"type {pair.Key} ({pair.Value.Official}) is missing");
                Assert.Equal(pair.Value.Shown, ComponentTypeCatalog.GetTypeName(pair.Key));
            }
        }

        [Theory]
        [InlineData(91, "Plug-in assemblies")]
        [InlineData(92, "Plug-in steps")]
        [InlineData(152, "SLAs")]
        [InlineData(300, "Canvas apps")]
        [InlineData(380, "Environment variables")]
        public void GetGroupName_UsesTheMakerPortalFolderNames(int type, string expected)
        {
            Assert.Equal(expected, ComponentTypeCatalog.GetGroupName(type, null));
        }

        [Theory]
        [InlineData(1, "Entity")]
        [InlineData(60, "Form")]
        [InlineData(80, "Model-Driven App")]
        public void GetTypeName_ReturnsFriendlyNameForKnownTypes(int type, string expected)
        {
            Assert.Equal(expected, ComponentTypeCatalog.GetTypeName(type));
        }

        [Fact]
        public void GetTypeName_FallsBackToRawNumberForUnknownTypes()
        {
            Assert.Equal("Component Type 9999", ComponentTypeCatalog.GetTypeName(9999));
        }

        [Fact]
        public void Type3IsNotInCatalogSoItUsesDynamicResolution()
        {
            Assert.False(ComponentTypeCatalog.Definitions.ContainsKey(3));
        }

        [Fact]
        public void GetPrimaryKeyAttribute_UsesLogicalNameIdByDefault()
        {
            var definition = ComponentTypeCatalog.Definitions[26];
            Assert.Equal("savedqueryid", ComponentTypeCatalog.GetPrimaryKeyAttribute(definition));
        }

        [Fact]
        public void GetPrimaryKeyAttribute_HonorsSystemFormOverride()
        {
            var definition = ComponentTypeCatalog.Definitions[ComponentTypeCatalog.Form];
            Assert.Equal("formid", ComponentTypeCatalog.GetPrimaryKeyAttribute(definition));
        }

        [Fact]
        public void MetadataBackedTypesHaveNoBackingTable()
        {
            Assert.True(ComponentTypeCatalog.Definitions[ComponentTypeCatalog.Entity].IsMetadataBacked);
            Assert.False(ComponentTypeCatalog.Definitions[ComponentTypeCatalog.Form].IsMetadataBacked);
        }
    }
}
