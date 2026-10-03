using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ComponentTypeCatalogTests
    {
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
