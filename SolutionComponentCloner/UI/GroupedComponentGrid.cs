using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;

namespace SolutionComponentCloner.UI
{
    /// <summary>
    /// A virtual, custom-painted tree grid in the style of the Power Apps solution explorer: components are
    /// grouped by type, tables hold their columns/forms/views/etc., and every folder has a tri-state checkbox.
    /// It can be searched, sorted and filtered without losing which folders were open.
    /// </summary>
    internal sealed class GroupedComponentGrid : DataGridView
    {
        private const int AutoCollapseThreshold = 30;
        private const int IndentStep = 18;
        private const int ChevronOffset = 8;
        private const int CheckBoxOffset = 28;
        private const int CheckBoxSize = 14;
        private const int LabelOffset = 50;
        private const string AppWarning = "Adds every entity, form and process the app is built from";

        private const int ColComponent = 0;
        private const int ColNote = 1;
        private const int ColModified = 2;
        private const int ColModifiedBy = 3;
        private const int ColState = 4;

        private List<SolutionComponentItem> _allItems = new List<SolutionComponentItem>();
        private List<ComponentNode> _roots = new List<ComponentNode>();
        private readonly List<ComponentNode> _rows = new List<ComponentNode>();
        private readonly HashSet<string> _collapsedKeys = new HashSet<string>();
        private readonly HashSet<string> _expandedKeys = new HashSet<string>();
        private bool _defaultCollapsed;
        private string _searchText = string.Empty;
        private bool _hideInTarget;
        private ComponentSort _sort = new ComponentSort { Column = ComponentSortColumn.Name };
        private int _visibleCount;

        public event EventHandler ComponentSelectionChanged;
        public event EventHandler ViewChanged;

        public bool ShowCheckBoxes { get; set; } = true;

        public IReadOnlyList<SolutionComponentItem> AllItems => _allItems;
        public int TotalCount => _allItems.Count;
        public int VisibleCount => _visibleCount;
        public int GroupCount => _roots.Count;
        public int SelectedCount => _allItems.Count(i => i.Selected);
        public int InTargetCount => _allItems.Count(i => i.InTarget);
        public bool IsFiltered => !string.IsNullOrWhiteSpace(_searchText) || _hideInTarget;
        public IEnumerable<SolutionComponentItem> SelectedComponents => _allItems.Where(i => i.Selected);

        public string SearchText
        {
            get => _searchText;
            set
            {
                value = value ?? string.Empty;
                if (value == _searchText)
                {
                    return;
                }

                _searchText = value;
                ApplyView();
            }
        }

        public bool HideInTarget
        {
            get => _hideInTarget;
            set
            {
                if (value == _hideInTarget)
                {
                    return;
                }

                _hideInTarget = value;
                ApplyView();
            }
        }

        public GroupedComponentGrid()
        {
            DoubleBuffered = true;
            VirtualMode = true;
            ReadOnly = true;
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false;
            AllowUserToResizeColumns = false;
            RowHeadersVisible = false;
            MultiSelect = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.None;
            BackgroundColor = Theme.PanelBackground;
            EnableHeadersVisualStyles = false;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            ColumnHeadersHeight = 32;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            RowTemplate.Height = 30;
            Font = Theme.FontRegular;

            ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White;

            AddColumn("Component", fillWeight: 44);
            AddColumn("Note", fillWeight: 36);
            AddColumn("Modified", width: 128);
            AddColumn("Modified by", width: 140);
            AddColumn("State", width: 84);

            CellValueNeeded += (s, e) => e.Value = string.Empty;
            CellPainting += OnCellPainting;
            CellMouseClick += OnCellMouseClick;
            ColumnHeaderMouseClick += OnHeaderMouseClick;
        }

        private void AddColumn(string header, float fillWeight = 0, int width = 0)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            if (width > 0)
            {
                column.Width = width;
                column.Resizable = DataGridViewTriState.False;
            }
            else
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.FillWeight = fillWeight;
            }

            Columns.Add(column);
        }

        public void SetItems(IEnumerable<SolutionComponentItem> items)
        {
            _allItems = items.ToList();
            _collapsedKeys.Clear();
            _expandedKeys.Clear();
            _defaultCollapsed = _allItems.Count > AutoCollapseThreshold;
            ApplyView();
            RaiseSelectionChanged();
        }

        public void ApplyTargetKeys(HashSet<(int Type, Guid Id)> keys)
        {
            foreach (var item in _allItems)
            {
                item.InTarget = keys != null && keys.Contains((item.ComponentType, item.ObjectId));
            }

            ApplyView();
        }

        public void ClearTargetInfo() => ApplyTargetKeys(null);

        public void NotifySelectionChanged()
        {
            Invalidate();
            RaiseSelectionChanged();
        }

        private IEnumerable<SolutionComponentItem> VisibleItems => _roots.SelectMany(r => r.SubtreeItems());

        private void ApplyView()
        {
            var visible = _allItems
                .Where(i => ComponentFilter.Matches(i, _searchText) && !(_hideInTarget && i.InTarget))
                .ToList();
            _visibleCount = visible.Count;
            _roots = ComponentTreeBuilder.Build(visible, _sort);

            var searching = !string.IsNullOrWhiteSpace(_searchText);
            foreach (var node in ComponentTreeBuilder.AllNodes(_roots))
            {
                node.Collapsed = node.HasChildren && !searching && IsCollapsed(node.Key);
            }

            Rebuild();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool IsCollapsed(string key) => _defaultCollapsed ? !_expandedKeys.Contains(key) : _collapsedKeys.Contains(key);

        private void SetKeyCollapsed(string key, bool collapsed)
        {
            if (_defaultCollapsed)
            {
                if (collapsed) { _expandedKeys.Remove(key); } else { _expandedKeys.Add(key); }
            }
            else
            {
                if (collapsed) { _collapsedKeys.Add(key); } else { _collapsedKeys.Remove(key); }
            }
        }

        public void SelectVisible()
        {
            foreach (var item in VisibleItems)
            {
                item.Selected = true;
            }

            NotifySelectionChanged();
        }

        public void ClearComponentSelection()
        {
            foreach (var item in _allItems)
            {
                item.Selected = false;
            }

            NotifySelectionChanged();
        }

        /// <summary>Select all ticks what is shown (so a search narrows it); clearing always clears everything.</summary>
        public void SetAllSelected(bool selected)
        {
            if (selected) { SelectVisible(); } else { ClearComponentSelection(); }
        }

        private void ToggleVisibleSelection()
        {
            var visible = VisibleItems.ToList();
            var selectAll = visible.Any(i => !i.Selected);
            foreach (var item in visible)
            {
                item.Selected = selectAll;
            }

            NotifySelectionChanged();
        }

        /// <summary>Ticks every shown component modified on or after the date; returns how many.</summary>
        public int SelectModifiedSince(DateTime since)
        {
            var count = 0;
            foreach (var item in VisibleItems.Where(i => i.ModifiedOn.HasValue && i.ModifiedOn.Value >= since))
            {
                item.Selected = true;
                count++;
            }

            NotifySelectionChanged();
            return count;
        }

        public void ExpandAll() => SetAllCollapsed(false);

        public void CollapseAll() => SetAllCollapsed(true);

        private void SetAllCollapsed(bool collapsed)
        {
            foreach (var node in ComponentTreeBuilder.AllNodes(_roots).Where(n => n.HasChildren))
            {
                SetKeyCollapsed(node.Key, collapsed);
            }

            ApplyView();
        }

        private void SetSort(ComponentSortColumn column)
        {
            _sort = _sort.Column == column
                ? new ComponentSort { Column = column, Descending = !_sort.Descending }
                : new ComponentSort { Column = column };
            ApplyView();
        }

        private void Rebuild()
        {
            var firstVisible = RowCount > 0 && FirstDisplayedScrollingRowIndex >= 0 ? FirstDisplayedScrollingRowIndex : 0;

            _rows.Clear();
            _rows.AddRange(ComponentTreeBuilder.Flatten(_roots));
            RowCount = _rows.Count;

            if (firstVisible > 0 && firstVisible < RowCount)
            {
                try { FirstDisplayedScrollingRowIndex = firstVisible; }
                catch (InvalidOperationException) { }
            }

            Invalidate();
        }

        private void RaiseSelectionChanged()
        {
            ComponentSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleCollapsed(ComponentNode node)
        {
            if (!node.HasChildren)
            {
                return;
            }

            node.Collapsed = !node.Collapsed;
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                SetKeyCollapsed(node.Key, node.Collapsed);
            }

            Rebuild();

            var index = _rows.IndexOf(node);
            if (index >= 0)
            {
                CurrentCell = this[0, index];
            }
        }

        private void ToggleSelection(ComponentNode node)
        {
            if (!ShowCheckBoxes)
            {
                return;
            }

            var items = node.SubtreeItems().ToList();
            var selectAll = items.Any(i => !i.Selected);
            foreach (var item in items)
            {
                item.Selected = selectAll;
            }

            NotifySelectionChanged();
        }

        private static int ChevronX(ComponentNode node) => ChevronOffset + node.Depth * IndentStep;

        private static int CheckBoxX(ComponentNode node) => CheckBoxOffset + node.Depth * IndentStep;

        private static int LabelX(ComponentNode node) => LabelOffset + node.Depth * IndentStep;

        private void OnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            switch (e.ColumnIndex)
            {
                case ColComponent:
                    if (ShowCheckBoxes && e.X >= CheckBoxOffset - 4 && e.X <= CheckBoxOffset + CheckBoxSize + 4)
                    {
                        ToggleVisibleSelection();
                    }
                    else
                    {
                        SetSort(ComponentSortColumn.Name);
                    }
                    break;
                case ColModified:
                    SetSort(ComponentSortColumn.Modified);
                    break;
                case ColModifiedBy:
                    SetSort(ComponentSortColumn.ModifiedBy);
                    break;
                case ColState:
                    SetSort(ComponentSortColumn.State);
                    break;
            }
        }

        private void OnCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
            {
                return;
            }

            var node = _rows[e.RowIndex];

            if (e.Button == MouseButtons.Right)
            {
                if (node.IsTable && node.Item != null && ShowCheckBoxes)
                {
                    ShowSubcomponentMenu(node, e);
                }

                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var onCheckBox = e.ColumnIndex == ColComponent && e.X >= CheckBoxX(node) - 4 && e.X <= CheckBoxX(node) + CheckBoxSize + 4;

            if (onCheckBox)
            {
                ToggleSelection(node);
            }
            else if (node.HasChildren)
            {
                ToggleCollapsed(node);
            }
        }

        private void ShowSubcomponentMenu(ComponentNode node, DataGridViewCellMouseEventArgs e)
        {
            var menu = new ContextMenuStrip();
            AddModeItem(menu, node, "Follow the \"Include required components\" checkbox", SubcomponentMode.UseGlobalSetting);
            AddModeItem(menu, node, "Include all of the table's subcomponents", SubcomponentMode.IncludeAll);
            AddModeItem(menu, node, "Only the subcomponents I ticked", SubcomponentMode.OnlySelected);

            var cell = GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            menu.Show(this, new Point(cell.Left + e.X, cell.Top + e.Y));
        }

        private void AddModeItem(ContextMenuStrip menu, ComponentNode node, string text, SubcomponentMode mode)
        {
            var item = new ToolStripMenuItem(text) { Checked = node.Item.SubcomponentMode == mode };
            item.Click += (s, args) =>
            {
                node.Item.SubcomponentMode = mode;
                Invalidate();
            };
            menu.Items.Add(item);
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (CurrentCell != null && CurrentCell.RowIndex >= 0 && CurrentCell.RowIndex < _rows.Count)
            {
                var node = _rows[CurrentCell.RowIndex];

                if (e.KeyCode == Keys.Space && ShowCheckBoxes)
                {
                    ToggleSelection(node);
                    return true;
                }

                if (node.HasChildren &&
                    (e.KeyCode == Keys.Enter ||
                     (e.KeyCode == Keys.Left && !node.Collapsed) ||
                     (e.KeyCode == Keys.Right && node.Collapsed)))
                {
                    ToggleCollapsed(node);
                    return true;
                }
            }

            return base.ProcessDataGridViewKey(e);
        }

        private static CheckState StateOf(ComponentNode node)
        {
            var total = 0;
            var selected = 0;
            foreach (var item in node.SubtreeItems())
            {
                total++;
                if (item.Selected)
                {
                    selected++;
                }
            }

            return selected == 0 ? CheckState.Unchecked : selected == total ? CheckState.Checked : CheckState.Indeterminate;
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0)
            {
                return;
            }

            if (e.RowIndex == -1)
            {
                PaintHeader(e);
                return;
            }

            if (e.RowIndex >= _rows.Count)
            {
                return;
            }

            var node = _rows[e.RowIndex];
            var g = e.Graphics;
            var bounds = e.CellBounds;
            var rowSelected = (e.State & DataGridViewElementStates.Selected) != 0;
            var state = StateOf(node);

            Color background;
            if (node.Depth == 0)
            {
                background = rowSelected ? Theme.Pressed : Theme.GroupBackground;
            }
            else if (state == CheckState.Checked && ShowCheckBoxes)
            {
                background = rowSelected ? Theme.AccentSelected : Theme.AccentSoft;
            }
            else
            {
                background = rowSelected ? Theme.Hover : Theme.PanelBackground;
            }

            using (var brush = new SolidBrush(background))
            {
                g.FillRectangle(brush, bounds);
            }

            using (var pen = new Pen(Theme.Border))
            {
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }

            switch (e.ColumnIndex)
            {
                case ColComponent:
                    PaintTreeCell(g, bounds, node, state);
                    break;
                case ColNote:
                    PaintNote(g, bounds, node);
                    break;
                case ColModified:
                    PaintText(g, bounds, node.Item?.ModifiedOn?.ToString("yyyy-MM-dd HH:mm"));
                    break;
                case ColModifiedBy:
                    PaintText(g, bounds, node.Item?.ModifiedBy);
                    break;
                case ColState:
                    PaintText(g, bounds, node.Item?.IsManaged == true ? "Managed" : node.Item?.IsManaged == false ? "Unmanaged" : null);
                    break;
            }

            e.Handled = true;
        }

        private static void PaintText(Graphics g, Rectangle bounds, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            TextRenderer.DrawText(g, text, Theme.FontRegular,
                new Rectangle(bounds.Left + 6, bounds.Top, Math.Max(0, bounds.Width - 6), bounds.Height), Theme.TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static void PaintNote(Graphics g, Rectangle bounds, ComponentNode node)
        {
            var item = node.Item;
            if (item == null)
            {
                return;
            }

            var left = bounds.Left + 6;
            if (item.IsAddedCompletely)
            {
                left = PaintChip(g, bounds, left, "Added completely — all subcomponents");
            }

            var notes = new List<string>();
            var color = Theme.TextSecondary;

            if (!node.HasChildren && item.ComponentType == ComponentTypeCatalog.ModelDrivenApp)
            {
                notes.Add(AppWarning);
                color = Theme.Danger;
            }

            if (item.ComponentType == ComponentTypeCatalog.Entity && item.SubcomponentMode != SubcomponentMode.UseGlobalSetting)
            {
                notes.Add(item.SubcomponentMode == SubcomponentMode.IncludeAll ? "Includes all subcomponents" : "Only ticked subcomponents");
            }

            if (item.InTarget)
            {
                notes.Add("Already in target");
            }

            PaintTextColored(g, bounds, left, string.Join(" · ", notes), color);
        }

        /// <summary>A yellow highlighted note; returns the x position where the next note can start.</summary>
        private static int PaintChip(Graphics g, Rectangle bounds, int left, string text)
        {
            const int padding = 8;
            var textWidth = TextRenderer.MeasureText(text, Theme.FontRegular, Size.Empty, TextFormatFlags.NoPadding).Width;
            var width = Math.Min(textWidth + padding * 2, Math.Max(0, bounds.Right - 4 - left));
            if (width <= padding * 2)
            {
                return left;
            }

            var chip = new Rectangle(left, bounds.Top + (bounds.Height - 20) / 2, width, 20);
            using (var fill = new SolidBrush(Theme.WarningBackground))
            using (var border = new Pen(Theme.WarningBorder))
            {
                g.FillRectangle(fill, chip);
                g.DrawRectangle(border, chip.X, chip.Y, chip.Width - 1, chip.Height - 1);
            }

            TextRenderer.DrawText(g, text, Theme.FontRegular,
                new Rectangle(chip.Left + padding, chip.Top, chip.Width - padding * 2, chip.Height), Theme.Warning,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            return chip.Right + 8;
        }

        private static void PaintTextColored(Graphics g, Rectangle bounds, int left, string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            TextRenderer.DrawText(g, text, Theme.FontRegular,
                new Rectangle(left, bounds.Top, Math.Max(0, bounds.Right - left), bounds.Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void PaintTreeCell(Graphics g, Rectangle bounds, ComponentNode node, CheckState state)
        {
            if (node.HasChildren)
            {
                TextRenderer.DrawText(g, node.Collapsed ? Theme.GlyphChevronRight : Theme.GlyphChevronDown, Theme.FontIconSmall,
                    new Rectangle(bounds.Left + ChevronX(node), bounds.Top, 16, bounds.Height), Theme.TextSecondary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            if (ShowCheckBoxes)
            {
                var checkRect = new Rectangle(bounds.Left + CheckBoxX(node), bounds.Top + (bounds.Height - CheckBoxSize) / 2, CheckBoxSize, CheckBoxSize);
                DrawCheckBox(g, checkRect, state);
            }

            var area = new Rectangle(bounds.Left + LabelX(node), bounds.Top, Math.Max(0, bounds.Width - LabelX(node)), bounds.Height);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

            if (!node.HasChildren && !node.IsTable)
            {
                TextRenderer.DrawText(g, node.Name ?? string.Empty, Theme.FontRegular, area, Theme.Accent, flags);
                return;
            }

            var nameFont = node.Depth <= 1 || node.IsTable ? Theme.FontBold : Theme.FontRegular;
            var nameColor = node.IsTable && node.Item != null ? Theme.Accent : Theme.TextPrimary;
            var nameWidth = TextRenderer.MeasureText(node.Name ?? string.Empty, nameFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, node.Name ?? string.Empty, nameFont, area, nameColor, flags);

            var selected = node.SelectedCount;
            var count = node.IsTable ? string.Empty : $"  ({node.DisplayCount})";
            if (selected > 0 && ShowCheckBoxes)
            {
                count += (count.Length == 0 ? "  " : string.Empty) + $" · {selected} selected";
            }

            if (count.Length > 0)
            {
                var countArea = new Rectangle(area.Left + nameWidth, area.Top, Math.Max(0, area.Width - nameWidth), area.Height);
                TextRenderer.DrawText(g, count, Theme.FontRegular, countArea, Theme.TextSecondary, flags);
            }
        }

        private void PaintHeader(DataGridViewCellPaintingEventArgs e)
        {
            var g = e.Graphics;
            var bounds = e.CellBounds;
            g.FillRectangle(Brushes.White, bounds);
            using (var pen = new Pen(Theme.Border))
            {
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }

            var textLeft = bounds.Left + 6;
            if (e.ColumnIndex == ColComponent)
            {
                if (ShowCheckBoxes)
                {
                    var visible = VisibleItems.ToList();
                    var selected = visible.Count(i => i.Selected);
                    var state = selected == 0 ? CheckState.Unchecked : selected == visible.Count ? CheckState.Checked : CheckState.Indeterminate;
                    DrawCheckBox(g, new Rectangle(bounds.Left + CheckBoxOffset, bounds.Top + (bounds.Height - CheckBoxSize) / 2, CheckBoxSize, CheckBoxSize), state);
                }

                textLeft = bounds.Left + LabelOffset;
            }

            var header = Columns[e.ColumnIndex].HeaderText;
            TextRenderer.DrawText(g, header, Theme.FontBold,
                new Rectangle(textLeft, bounds.Top, Math.Max(0, bounds.Right - textLeft), bounds.Height), Theme.TextPrimary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            if (SortColumnIndex() == e.ColumnIndex)
            {
                var textWidth = TextRenderer.MeasureText(header, Theme.FontBold, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, _sort.Descending ? Theme.GlyphChevronDown : Theme.GlyphChevronUp, Theme.FontIconSmall,
                    new Rectangle(textLeft + textWidth + 6, bounds.Top, 14, bounds.Height), Theme.TextSecondary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            e.Handled = true;
        }

        private int SortColumnIndex()
        {
            switch (_sort.Column)
            {
                case ComponentSortColumn.Modified: return ColModified;
                case ComponentSortColumn.ModifiedBy: return ColModifiedBy;
                case ComponentSortColumn.State: return ColState;
                default: return ColComponent;
            }
        }

        private static void DrawCheckBox(Graphics g, Rectangle rect, CheckState state)
        {
            var previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var filled = state != CheckState.Unchecked;

            using (var fill = new SolidBrush(filled ? Theme.Accent : Color.White))
            using (var border = new Pen(filled ? Theme.Accent : Theme.CheckBorder))
            {
                g.FillRectangle(fill, rect);
                g.DrawRectangle(border, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            using (var mark = new Pen(Color.White, 1.7f))
            {
                if (state == CheckState.Checked)
                {
                    g.DrawLines(mark, new[]
                    {
                        new PointF(rect.X + 3.5f, rect.Y + 7.5f),
                        new PointF(rect.X + 6f, rect.Y + 10f),
                        new PointF(rect.X + 10.5f, rect.Y + 4.5f)
                    });
                }
                else if (state == CheckState.Indeterminate)
                {
                    g.DrawLine(mark, rect.X + 3.5f, rect.Y + 7f, rect.X + 10.5f, rect.Y + 7f);
                }
            }

            g.SmoothingMode = previous;
        }
    }
}
