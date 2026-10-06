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

        private List<ComponentNode> _roots = new List<ComponentNode>();
        private readonly List<ComponentNode> _rows = new List<ComponentNode>();

        public event EventHandler ComponentSelectionChanged;

        public int TotalCount => _roots.Sum(r => r.ItemCount);
        public int GroupCount => _roots.Count;
        public int SelectedCount => _roots.Sum(r => r.SelectedCount);
        public IEnumerable<SolutionComponentItem> SelectedComponents => _roots.SelectMany(r => r.SubtreeItems()).Where(i => i.Selected);

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
                HeaderText = "Component",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 64,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Note",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 36,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            CellValueNeeded += (s, e) => e.Value = string.Empty;
            CellPainting += OnCellPainting;
            CellMouseClick += OnCellMouseClick;
            ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.Button == MouseButtons.Left && e.X >= CheckBoxOffset - 4 && e.X <= CheckBoxOffset + CheckBoxSize + 4)
                {
                    SetAllSelected(SelectedCount != TotalCount);
                }
            };
        }

        public void SetItems(IEnumerable<SolutionComponentItem> items)
        {
            var list = items.ToList();
            _roots = ComponentTreeBuilder.Build(list);

            if (list.Count > AutoCollapseThreshold)
            {
                foreach (var node in ComponentTreeBuilder.AllNodes(_roots))
                {
                    node.Collapsed = true;
                }
            }

            Rebuild();
            RaiseSelectionChanged();
        }

        public void SetAllSelected(bool selected)
        {
            foreach (var item in _roots.SelectMany(r => r.SubtreeItems()))
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
            foreach (var node in ComponentTreeBuilder.AllNodes(_roots))
            {
                node.Collapsed = collapsed;
            }

            Rebuild();
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
            Rebuild();

            var index = _rows.IndexOf(node);
            if (index >= 0)
            {
                CurrentCell = this[0, index];
            }
        }

        private void ToggleSelection(ComponentNode node)
        {
            var items = node.SubtreeItems().ToList();
            var selectAll = items.Any(i => !i.Selected);
            foreach (var item in items)
            {
                item.Selected = selectAll;
            }

            Invalidate();
            RaiseSelectionChanged();
        }

        private static int ChevronX(ComponentNode node) => ChevronOffset + node.Depth * IndentStep;

        private static int CheckBoxX(ComponentNode node) => CheckBoxOffset + node.Depth * IndentStep;

        private static int LabelX(ComponentNode node) => LabelOffset + node.Depth * IndentStep;

        private void OnCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.Button != MouseButtons.Left)
            {
                return;
            }

            var node = _rows[e.RowIndex];
            var onCheckBox = e.ColumnIndex == 0 && e.X >= CheckBoxX(node) - 4 && e.X <= CheckBoxX(node) + CheckBoxSize + 4;

            if (onCheckBox)
            {
                ToggleSelection(node);
            }
            else if (node.HasChildren)
            {
                ToggleCollapsed(node);
            }
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (CurrentCell != null && CurrentCell.RowIndex >= 0 && CurrentCell.RowIndex < _rows.Count)
            {
                var node = _rows[CurrentCell.RowIndex];

                if (e.KeyCode == Keys.Space)
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
            else if (state == CheckState.Checked)
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

            if (e.ColumnIndex == 0)
            {
                PaintTreeCell(g, bounds, node, state);
            }
            else if (node.Item != null && !node.HasChildren && node.Item.ComponentType == ComponentTypeCatalog.ModelDrivenApp)
            {
                TextRenderer.DrawText(g, AppWarning, Theme.FontRegular,
                    new Rectangle(bounds.Left + 6, bounds.Top, Math.Max(0, bounds.Width - 6), bounds.Height), Theme.Danger,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            e.Handled = true;
        }

        private static void PaintTreeCell(Graphics g, Rectangle bounds, ComponentNode node, CheckState state)
        {
            if (node.HasChildren)
            {
                TextRenderer.DrawText(g, node.Collapsed ? Theme.GlyphChevronRight : Theme.GlyphChevronDown, Theme.FontIconSmall,
                    new Rectangle(bounds.Left + ChevronX(node), bounds.Top, 16, bounds.Height), Theme.TextSecondary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            var checkRect = new Rectangle(bounds.Left + CheckBoxX(node), bounds.Top + (bounds.Height - CheckBoxSize) / 2, CheckBoxSize, CheckBoxSize);
            DrawCheckBox(g, checkRect, state);

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
            if (selected > 0)
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
            if (e.ColumnIndex == 0)
            {
                var state = TotalCount == 0 || SelectedCount == 0 ? CheckState.Unchecked
                    : SelectedCount == TotalCount ? CheckState.Checked
                    : CheckState.Indeterminate;
                DrawCheckBox(g, new Rectangle(bounds.Left + CheckBoxOffset, bounds.Top + (bounds.Height - CheckBoxSize) / 2, CheckBoxSize, CheckBoxSize), state);
                textLeft = bounds.Left + LabelOffset;
            }

            TextRenderer.DrawText(g, Columns[e.ColumnIndex].HeaderText, Theme.FontBold,
                new Rectangle(textLeft, bounds.Top, Math.Max(0, bounds.Right - textLeft), bounds.Height), Theme.TextPrimary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            e.Handled = true;
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
