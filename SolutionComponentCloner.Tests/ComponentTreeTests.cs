using System;
using System.Linq;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class ComponentTreeTests
    {
        private static SolutionComponentItem Table(string logical, string label) => new SolutionComponentItem
        {
            ObjectId = Guid.NewGuid(), ComponentType = ComponentTypeCatalog.Entity, DisplayName = label,
            GroupName = "Tables", TableName = logical, TableLabel = label
        };

        private static SolutionComponentItem Part(string table, string category, string name, int type = 60) => new SolutionComponentItem
        {
            ObjectId = Guid.NewGuid(), ComponentType = type, DisplayName = name,
            GroupName = "Tables", TableName = table, TableLabel = table, Category = category
        };

        private static SolutionComponentItem WebResource(string subGroup, string name) => new SolutionComponentItem
        {
            ObjectId = Guid.NewGuid(), ComponentType = 61, DisplayName = name, GroupName = "Web resources", SubGroup = subGroup
        };

        [Fact]
        public void TableHoldsItsPartsInCategoryOrder()
        {
            var items = new[]
            {
                Part("account", "Views", "Active Accounts", 26),
                Table("account", "Account (account)"),
                Part("account", "Forms", "Account Main"),
                Part("account", "Columns", "Name (name)", 2)
            };

            var roots = ComponentTreeBuilder.Build(items);

            var tables = Assert.Single(roots);
            Assert.Equal("Tables", tables.Name);
            var account = Assert.Single(tables.Children);
            Assert.True(account.IsTable);
            Assert.Equal("Account (account)", account.Name);
            Assert.NotNull(account.Item);
            Assert.Equal(new[] { "Columns", "Forms", "Views" }, account.Children.Select(c => c.Name));
        }

        [Fact]
        public void TableWithoutItsOwnComponentStillGetsAFolder()
        {
            var roots = ComponentTreeBuilder.Build(new[] { Part("contact", "Forms", "Contact Main") });

            var contact = roots.Single().Children.Single();
            Assert.True(contact.IsTable);
            Assert.Null(contact.Item);
            Assert.Equal("contact", contact.Name);
        }

        [Fact]
        public void WebResourcesAreSplitIntoSubFoldersAndCountedByItem()
        {
            var items = new[]
            {
                WebResource("Code", "a.js"), WebResource("Code", "b.js"), WebResource("Code", "c.html"),
                WebResource("Data", "d.xml"), WebResource("Images", "e.png")
            };

            var group = ComponentTreeBuilder.Build(items).Single();

            Assert.Equal(new[] { "Code", "Data", "Images" }, group.Children.Select(c => c.Name));
            Assert.Equal(5, group.DisplayCount);
            Assert.Equal(3, group.Children[0].DisplayCount);
        }

        [Fact]
        public void TablesCountOnceHoweverManyPartsTheyHold()
        {
            var items = new[]
            {
                Table("account", "Account"), Part("account", "Columns", "c1", 2), Part("account", "Columns", "c2", 2),
                Table("contact", "Contact"), Part("contact", "Forms", "f1")
            };

            var tables = ComponentTreeBuilder.Build(items).Single();

            Assert.Equal(2, tables.DisplayCount);
            Assert.Equal(5, tables.ItemCount);
        }

        [Fact]
        public void ComponentsWithoutATableStayInTheirOwnTopLevelGroup()
        {
            var dashboard = new SolutionComponentItem { ObjectId = Guid.NewGuid(), ComponentType = 60, DisplayName = "Sales Dashboard", GroupName = "Dashboards" };

            var roots = ComponentTreeBuilder.Build(new[] { dashboard, WebResource("Code", "a.js") });

            Assert.Equal(new[] { "Dashboards", "Web resources" }, roots.Select(r => r.Name));
            Assert.Same(dashboard, roots[0].Children.Single().Item);
        }

        [Fact]
        public void SelectionCountsCoverTheWholeSubtree()
        {
            var items = new[] { Table("account", "Account"), Part("account", "Columns", "c1", 2), Part("account", "Forms", "f1") };
            items[0].Selected = true;
            items[2].Selected = true;

            var account = ComponentTreeBuilder.Build(items).Single().Children.Single();

            Assert.Equal(3, account.ItemCount);
            Assert.Equal(2, account.SelectedCount);
        }

        [Fact]
        public void CollapsedFoldersHideTheirChildrenWhenFlattened()
        {
            var roots = ComponentTreeBuilder.Build(new[] { Table("account", "Account"), Part("account", "Forms", "f1") });

            Assert.Equal(4, ComponentTreeBuilder.Flatten(roots).Count());

            roots[0].Collapsed = true;
            Assert.Single(ComponentTreeBuilder.Flatten(roots));
        }
    }
}
