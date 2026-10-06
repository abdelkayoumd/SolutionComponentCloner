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
    /// A virtual, custom-painted grid that lists components grouped by type, with collapsible
    /// groups, tri-state group checkboxes and a Dynamics-style look.
    /// </summary>
    internal sealed class GroupedComponentGrid : DataGridView
    {
        private const int AutoCollapseThreshold = 30;
        private const int ChevronZoneWidth = 26;
        private const int CheckBoxOffset = 28;
        private const int CheckBoxSize = 14;
        private const string AppWarning = "Adds every entity, form and process the app is built from";

        private sealed class Group
        {
            public string Name;
            public List<SolutionComponentItem> Items;
            public bool Collapsed;
            public int SelectedCount => Items.Count(i => i.Selected);
        }

        private sealed class Row
        {
            public Group Group;
            public SolutionComponentItem Item;
            public bool IsGroup => Item == null;
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly List<Row> _rows = new List<Row>();

        public event EventHandler ComponentSelectionChanged;

        public int TotalCount => _groups.Sum(g => g.Items.Count);
        public int GroupCount => _groups.Count;
        public int SelectedCount => _groups.Sum(g => g.SelectedCount);
        public IEnumerable<SolutionComponentItem> SelectedComponents => _groups.SelectMany(g => g.Items).Where(i => i.Selected);

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

            Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = string.Empty,
                Width = 60,
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Component",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 46,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Note",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 54,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            CellValueNeeded += (s, e) => e.Value = string.Empty;
            CellPainting += OnCellPainting;
            CellMouseClick += OnCellMouseClick;
            ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.Button == MouseButtons.Left)
                {
                    SetAllSelected(SelectedCount != TotalCount);
                }
            };
        }

        public void SetItems(IEnumerable<SolutionComponentItem> items)
        {
            var list = items.ToList();
            _groups.Clear();
            foreach (var group in list.GroupBy(GroupKey).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                _groups.Add(new Group
                {
                    Name = group.Key,
                    Items = group.ToList(),
                    Collapsed = list.Count > AutoCollapseThreshold
                });
            }

            Rebuild();
            RaiseSelectionChanged();
        }

        public void SetAllSelected(bool selected)
        {
            foreach (var item in _groups.SelectMany(g => g.Items))
            {
                item.Selected = selected;
            }

            Invalidate();
            RaiseSelectionChanged();
        }

        public void ExpandAll() => SetAllCollapsed(false);

        public void CollapseAll() => SetAllCollapsed(true);

        private void SetAllCollapsed(bool collapsed)
        {
            foreach (var group in _groups)
            {
                group.Collapsed = collapsed;
            }

            Rebuild();
        }

        private static string GroupKey(SolutionComponentItem item)
        {
            return string.IsNullOrEmpty(item.ComponentTypeName)
                ? ComponentTypeCatalog.GetTypeName(item.ComponentType)
                : item.ComponentTypeName;
        }

        private void Rebuild()
        {
            var firstVisible = RowCount > 0 && FirstDisplayedScrollingRowIndex >= 0 ? FirstDisplayedScrollingRowIndex : 0;

            _rows.Clear();
            foreach (var group in _groups)
            {
                _rows.Add(new Row { Group = group });
                if (!group.Collapsed)
                {
                    foreach (var item in group.Items)
                    {
                        _rows.Add(new Row { Group = group, Item = item });
                    }
                }
            }

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

        private void ToggleCollapsed(Group group)
        {
            group.Collapsed = !group.Collapsed;
            Rebuild();

            var index = _rows.FindIndex(r => r.IsGroup && r.Group == group);
            if (index >= 0)
            {
                CurrentCell = this[1, index];
            }
        }

        private void ToggleGroup(Group group)
        {
            var selectAll = group.SelectedCount != group.Items.Count;
            foreach (var item in group.Items)
            {
                item.Selected = selectAll;
            }

            Invalidate();
            RaiseSelectionChanged();
        }

        private void ToggleItem(SolutionComponentItem item)
        {
            item.Selected = !item.Selected;
            Invalidate();
            RaiseSelectionChanged();
        }

        private void OnCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.Button != MouseButtons.Left)
            {
                return;
            }

            var row = _rows[e.RowIndex];
            if (row.IsGroup)
            {
                var onCheckBox = e.ColumnIndex == 0 && e.X >= ChevronZoneWidth && e.X <= CheckBoxOffset + CheckBoxSize + 4;
                if (onCheckBox)
                {
                    ToggleGroup(row.Group);
                }
                else
                {
                    ToggleCollapsed(row.Group);
                }

                return;
            }

            if (e.ColumnIndex == 0)
            {
                ToggleItem(row.Item);
            }
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (CurrentCell != null && CurrentCell.RowIndex >= 0 && CurrentCell.RowIndex < _rows.Count)
            {
                var row = _rows[CurrentCell.RowIndex];

                if (e.KeyCode == Keys.Space)
                {
                    if (row.IsGroup) { ToggleGroup(row.Group); } else { ToggleItem(row.Item); }
                    return true;
                }

                if (row.IsGroup)
                {
                    if (e.KeyCode == Keys.Enter ||
                        (e.KeyCode == Keys.Left && !row.Group.Collapsed) ||
                        (e.KeyCode == Keys.Right && row.Group.Collapsed))
                    {
                        ToggleCollapsed(row.Group);
                        return true;
                    }
                }
            }

            return base.ProcessDataGridViewKey(e);
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

            var row = _rows[e.RowIndex];
            var g = e.Graphics;
            var bounds = e.CellBounds;
            var rowSelected = (e.State & DataGridViewElementStates.Selected) != 0;

            Color background;
            if (row.IsGroup)
            {
                background = rowSelected ? Theme.Pressed : Theme.GroupBackground;
            }
            else if (row.Item.Selected)
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
                case 0:
                    PaintCheckColumn(g, bounds, row);
                    break;
                case 1:
                    PaintNameColumn(g, bounds, row);
                    break;
                case 2:
                    PaintNoteColumn(g, bounds, row);
                    break;
            }

            e.Handled = true;
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

            if (e.ColumnIndex == 0)
            {
                var state = TotalCount == 0 || SelectedCount == 0 ? CheckState.Unchecked
                    : SelectedCount == TotalCount ? CheckState.Checked
                    : CheckState.Indeterminate;
                DrawCheckBox(g, CheckBoxRect(bounds), state);
            }
            else
            {
                TextRenderer.DrawText(g, e.Value as string ?? Columns[e.ColumnIndex].HeaderText, Theme.FontBold,
                    new Rectangle(bounds.Left + 6, bounds.Top, bounds.Width - 6, bounds.Height), Theme.TextPrimary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }

            e.Handled = true;
        }

        private static Rectangle CheckBoxRect(Rectangle cell)
        {
            return new Rectangle(cell.Left + CheckBoxOffset, cell.Top + (cell.Height - CheckBoxSize) / 2, CheckBoxSize, CheckBoxSize);
        }

        private static void PaintCheckColumn(Graphics g, Rectangle bounds, Row row)
        {
            if (row.IsGroup)
            {
                TextRenderer.DrawText(g, row.Group.Collapsed ? Theme.GlyphChevronRight : Theme.GlyphChevronDown, Theme.FontIconSmall,
                    new Rectangle(bounds.Left + 8, bounds.Top, 16, bounds.Height), Theme.TextSecondary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var selected = row.Group.SelectedCount;
                var state = selected == 0 ? CheckState.Unchecked
                    : selected == row.Group.Items.Count ? CheckState.Checked
                    : CheckState.Indeterminate;
                DrawCheckBox(g, CheckBoxRect(bounds), state);
            }
            else
            {
                DrawCheckBox(g, CheckBoxRect(bounds), row.Item.Selected ? CheckState.Checked : CheckState.Unchecked);
            }
        }

        private static void PaintNameColumn(Graphics g, Rectangle bounds, Row row)
        {
            var area = new Rectangle(bounds.Left + 6, bounds.Top, Math.Max(0, bounds.Width - 6), bounds.Height);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

            if (row.IsGroup)
            {
                var nameWidth = TextRenderer.MeasureText(row.Group.Name, Theme.FontBold, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, row.Group.Name, Theme.FontBold, area, Theme.TextPrimary, flags);

                var selected = row.Group.SelectedCount;
                var count = $"  ({row.Group.Items.Count})" + (selected > 0 ? $" · {selected} selected" : string.Empty);
                var countArea = new Rectangle(area.Left + nameWidth, area.Top, Math.Max(0, area.Width - nameWidth), area.Height);
                TextRenderer.DrawText(g, count, Theme.FontRegular, countArea, Theme.TextSecondary, flags);
            }
            else
            {
                TextRenderer.DrawText(g, row.Item.DisplayName ?? string.Empty, Theme.FontRegular, area, Theme.Accent, flags);
            }
        }

        private static void PaintNoteColumn(Graphics g, Rectangle bounds, Row row)
        {
            if (row.IsGroup || row.Item.ComponentType != ComponentTypeCatalog.ModelDrivenApp)
            {
                return;
            }

            TextRenderer.DrawText(g, AppWarning, Theme.FontRegular,
                new Rectangle(bounds.Left + 6, bounds.Top, Math.Max(0, bounds.Width - 6), bounds.Height), Theme.Danger,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
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
