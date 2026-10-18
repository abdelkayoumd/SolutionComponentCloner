using System;
using System.Collections.Generic;
using System.Linq;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    internal enum ComponentSortColumn { Name, Modified, ModifiedBy, State }

    internal sealed class ComponentSort
    {
        public ComponentSortColumn Column { get; set; }
        public bool Descending { get; set; }
    }

    /// <summary>A folder or component in the solution explorer tree.</summary>
    internal sealed class ComponentNode
    {
        private readonly Dictionary<string, ComponentNode> _index = new Dictionary<string, ComponentNode>(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; }
        public ComponentNode Parent { get; set; }
        public int Depth { get; set; }
        public List<ComponentNode> Children { get; } = new List<ComponentNode>();

        /// <summary>A path that identifies this node across rebuilds, so folders keep their open/closed state.</summary>
        public string Key { get; set; } = string.Empty;

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
                child = new ComponentNode { Name = name, Parent = this, Key = Key + "/" + key };
                _index[key] = child;
                Children.Add(child);
            }

            return child;
        }

        public ComponentNode AddLeaf(SolutionComponentItem item)
        {
            var leaf = new ComponentNode
            {
                Name = item.DisplayName,
                Item = item,
                Parent = this,
                Key = Key + "/" + item.ComponentType + ":" + item.ObjectId
            };
            Children.Add(leaf);
            return leaf;
        }
    }

    internal static class ComponentTreeBuilder
    {
        private static readonly string[] CategoryOrder =
        {
            "Columns", "Relationships", "Keys", "Forms", "Views", "Charts", "Dashboards", "Business rules", "Commands", "Messages"
        };

        public static List<ComponentNode> Build(IEnumerable<SolutionComponentItem> items, ComponentSort sort = null)
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
                    parent.AddLeaf(item);
                    continue;
                }

                var container = string.IsNullOrEmpty(item.SubGroup) ? group : group.GetOrAddChild(item.SubGroup, item.SubGroup);
                container.AddLeaf(item);
            }

            SortAndNumber(roots, -1, sort);
            return roots.Children;
        }

        private static void SortAndNumber(ComponentNode node, int depth, ComponentSort sort)
        {
            node.Depth = depth;

            var folders = node.Children
                .Where(c => c.HasChildren || c.IsTable)
                .OrderBy(c => CategoryRank(c))
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var leaves = node.Children.Where(c => !(c.HasChildren || c.IsTable)).ToList();
            leaves.Sort(new LeafComparer(sort));

            node.Children.Clear();
            node.Children.AddRange(folders);
            node.Children.AddRange(leaves);

            foreach (var child in node.Children)
            {
                SortAndNumber(child, depth + 1, sort);
            }
        }

        private sealed class LeafComparer : IComparer<ComponentNode>
        {
            private readonly ComponentSort _sort;

            public LeafComparer(ComponentSort sort)
            {
                _sort = sort ?? new ComponentSort { Column = ComponentSortColumn.Name };
            }

            public int Compare(ComponentNode a, ComponentNode b)
            {
                int result;
                switch (_sort.Column)
                {
                    case ComponentSortColumn.Modified:
                        result = Nullable.Compare(a.Item.ModifiedOn, b.Item.ModifiedOn);
                        break;
                    case ComponentSortColumn.ModifiedBy:
                        result = string.Compare(a.Item.ModifiedBy, b.Item.ModifiedBy, StringComparison.OrdinalIgnoreCase);
                        break;
                    case ComponentSortColumn.State:
                        result = Nullable.Compare(a.Item.IsManaged, b.Item.IsManaged);
                        break;
                    default:
                        result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                        break;
                }

                if (_sort.Descending)
                {
                    result = -result;
                }

                return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
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
