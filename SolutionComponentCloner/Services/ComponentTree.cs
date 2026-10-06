using System;
using System.Collections.Generic;
using System.Linq;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    /// <summary>A folder or component in the solution explorer tree.</summary>
    internal sealed class ComponentNode
    {
        private readonly Dictionary<string, ComponentNode> _index = new Dictionary<string, ComponentNode>(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; }
        public ComponentNode Parent { get; set; }
        public int Depth { get; set; }
        public List<ComponentNode> Children { get; } = new List<ComponentNode>();

        /// <summary>The component this row stands for: a leaf's own component, or the table component on a table folder.</summary>
        public SolutionComponentItem Item { get; set; }

        public bool IsTable { get; set; }
        public bool Collapsed { get; set; }
        public bool HasChildren => Children.Count > 0;

        public IEnumerable<SolutionComponentItem> SubtreeItems()
        {
            if (Item != null)
            {
                yield return Item;
            }

            foreach (var child in Children)
            {
                foreach (var item in child.SubtreeItems())
                {
                    yield return item;
                }
            }
        }

        public int SelectedCount => SubtreeItems().Count(i => i.Selected);
        public int ItemCount => SubtreeItems().Count();

        /// <summary>Number shown next to a folder, such as "Tables (26)" or "Code (14)": tables count once, however many parts they hold.</summary>
        public int DisplayCount
        {
            get
            {
                var count = 0;
                foreach (var child in Children)
                {
                    count += child.IsTable || !child.HasChildren ? 1 : child.DisplayCount;
                }

                return count;
            }
        }

        public ComponentNode GetOrAddChild(string key, string name)
        {
            if (!_index.TryGetValue(key, out var child))
            {
                child = new ComponentNode { Name = name, Parent = this };
                _index[key] = child;
                Children.Add(child);
            }

            return child;
        }
    }

    internal static class ComponentTreeBuilder
    {
        private static readonly string[] CategoryOrder =
        {
            "Columns", "Relationships", "Keys", "Forms", "Views", "Charts", "Dashboards", "Business rules"
        };

        public static List<ComponentNode> Build(IEnumerable<SolutionComponentItem> items)
        {
            var roots = new ComponentNode();

            foreach (var item in items)
            {
                var group = roots.GetOrAddChild(item.GroupName ?? "Other", item.GroupName ?? "Other");

                if (!string.IsNullOrEmpty(item.TableName))
                {
                    var isTable = item.ComponentType == ComponentTypeCatalog.Entity;
                    var tableLabel = item.TableLabel ?? item.TableName;
                    var table = group.GetOrAddChild(item.TableName, tableLabel);
                    table.IsTable = true;

                    if (isTable)
                    {
                        table.Item = item;
                        table.Name = item.DisplayName ?? tableLabel;
                        continue;
                    }

                    var parent = string.IsNullOrEmpty(item.Category) ? table : table.GetOrAddChild(item.Category, item.Category);
                    parent.Children.Add(new ComponentNode { Name = item.DisplayName, Item = item, Parent = parent });
                    continue;
                }

                var container = string.IsNullOrEmpty(item.SubGroup) ? group : group.GetOrAddChild(item.SubGroup, item.SubGroup);
                container.Children.Add(new ComponentNode { Name = item.DisplayName, Item = item, Parent = container });
            }

            SortAndNumber(roots, -1);
            return roots.Children;
        }

        private static void SortAndNumber(ComponentNode node, int depth)
        {
            node.Depth = depth;

            var ordered = node.Children
                .OrderBy(c => c.HasChildren || c.IsTable ? 0 : 1)
                .ThenBy(c => CategoryRank(c))
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            node.Children.Clear();
            node.Children.AddRange(ordered);

            foreach (var child in node.Children)
            {
                SortAndNumber(child, depth + 1);
            }
        }

        private static int CategoryRank(ComponentNode node)
        {
            var index = Array.IndexOf(CategoryOrder, node.Name);
            return index < 0 ? CategoryOrder.Length : index;
        }

        public static IEnumerable<ComponentNode> Flatten(IEnumerable<ComponentNode> roots)
        {
            foreach (var node in roots)
            {
                yield return node;

                if (!node.Collapsed)
                {
                    foreach (var child in Flatten(node.Children))
                    {
                        yield return child;
                    }
                }
            }
        }

        public static IEnumerable<ComponentNode> AllNodes(IEnumerable<ComponentNode> roots)
        {
            foreach (var node in roots)
            {
                yield return node;
                foreach (var child in AllNodes(node.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
