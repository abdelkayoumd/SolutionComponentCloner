using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace SolutionComponentCloner.UI
{
    public partial class PluginControl
    {
        #region Layout

        private void BuildLayout()
        {
            BackColor = Theme.PageBackground;
            Font = Theme.FontRegular;
            Dock = DockStyle.Fill;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme.PageBackground,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildCommandBar(), 0, 1);
            root.Controls.Add(BuildTopSection(), 0, 2);
            root.Controls.Add(BuildSplit(), 0, 3);
            root.Controls.Add(BuildFooter(), 0, 4);

            UpdateSelectionUi();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && _txtSearch != null)
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private Control BuildHeader()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Navy,
                ColumnCount = 3,
                RowCount = 1,
                Height = 40,
                Margin = new Padding(0),
                Padding = new Padding(12, 0, 12, 0)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            bar.Controls.Add(new Label
            {
                Text = Theme.GlyphWaffle,
                Font = Theme.FontIcon,
                ForeColor = Color.White,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            bar.Controls.Add(new Label
            {
                Text = "Solution Component Cloner",
                Font = Theme.FontAppTitle,
                ForeColor = Color.White,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 1, 0);

            _lblConnection = new Label
            {
                Text = "Not connected",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(200, 210, 225),
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };
            bar.Controls.Add(_lblConnection, 2, 0);
            return bar;
        }

        private Control BuildCommandBar()
        {
            _commandBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                BackColor = Theme.PanelBackground,
                Margin = new Padding(0),
                Padding = new Padding(6, 0, 6, 0)
            };
            _commandBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, _commandBar.Height - 1, _commandBar.Width, _commandBar.Height - 1);
                }
            };

            _cmdCopy = new CommandButton { Glyph = Theme.GlyphCopy, Text = "Copy to target", Strong = true };
            _cmdCopy.Click += (s, e) => ExecuteMethod(CopySelected);

            _cmdMove = new CommandButton { Glyph = Theme.GlyphMove, Text = "Move" };
            _cmdMove.Click += (s, e) => ExecuteMethod(MoveSelected);

            _cmdPreview = new CommandButton { Glyph = Theme.GlyphPreview, Text = "Preview" };
            _cmdPreview.Click += (s, e) => ExecuteMethod(PreviewRequired);

            _cmdSelectAll = new CommandButton { Glyph = Theme.GlyphSelectAll, Text = "Select all" };
            _cmdSelectAll.Click += (s, e) => _grid.SetAllSelected(true);

            _cmdClear = new CommandButton { Glyph = Theme.GlyphClearSelection, Text = "Clear" };
            _cmdClear.Click += (s, e) => _grid.SetAllSelected(false);

            _cmdRemove = new CommandButton { Glyph = Theme.GlyphDelete, Text = "Remove" };
            _cmdRemove.Click += (s, e) => ExecuteMethod(RemoveSelected);

            _cmdRefresh = new CommandButton { Glyph = Theme.GlyphRefresh, Text = "Refresh" };
            _cmdRefresh.Click += (s, e) => ExecuteMethod(RefreshData);

            _cmdModifiedSince = new CommandButton { Glyph = Theme.GlyphCalendar, Text = "Modified since" };
            _cmdModifiedSince.Click += (s, e) => SelectModifiedSince();

            _cmdNewSolution = new CommandButton { Glyph = Theme.GlyphAdd, Text = "New solution" };
            _cmdNewSolution.Click += (s, e) => ExecuteMethod(NewTargetSolution);

            _cmdPublish = new CommandButton { Glyph = Theme.GlyphPublish, Text = "Publish all" };
            _cmdPublish.Click += (s, e) => ExecuteMethod(PublishAllCustomizations);

            _cmdCheckTarget = new CommandButton { Glyph = Theme.GlyphCheckList, Text = "Check target", Enabled = false };
            _cmdCheckTarget.Click += (s, e) => ExecuteMethod(CheckTargetDependencies);

            // What goes into the target, then what is ticked, then the environment-level tools; Remove sits
            // alone at the far end, away from Copy.
            _commandBar.Controls.Add(_cmdCopy);
            _commandBar.Controls.Add(_cmdMove);
            _commandBar.Controls.Add(_cmdPreview);
            _commandBar.Controls.Add(CreateCommandSeparator());
            _commandBar.Controls.Add(_cmdSelectAll);
            _commandBar.Controls.Add(_cmdClear);
            _commandBar.Controls.Add(_cmdModifiedSince);
            _commandBar.Controls.Add(CreateCommandSeparator());
            _commandBar.Controls.Add(_cmdRefresh);
            _commandBar.Controls.Add(_cmdNewSolution);
            _commandBar.Controls.Add(_cmdPublish);
            _commandBar.Controls.Add(_cmdCheckTarget);
            _commandBar.Controls.Add(CreateCommandSeparator());
            _commandBar.Controls.Add(_cmdRemove);

            _tips.SetToolTip(_cmdCopy, "Copy the ticked components into the target solution");
            _tips.SetToolTip(_cmdMove, "Copy the ticked components into the target solution, then remove them from the source solution");
            _tips.SetToolTip(_cmdPreview, "Preview the required components that would be added along with your selection");
            _tips.SetToolTip(_cmdSelectAll, "Tick everything shown (use the search box or the filters first to tick only the matches)");
            _tips.SetToolTip(_cmdClear, "Clear every tick");
            _tips.SetToolTip(_cmdRemove, "Remove the ticked components from the source solution (they stay in the environment and in other solutions)");
            _tips.SetToolTip(_cmdModifiedSince, "Tick every shown component modified on or after a date, by anyone or by one person");
            _tips.SetToolTip(_cmdNewSolution, "Create a new unmanaged solution to use as the target");
            _tips.SetToolTip(_cmdPublish, "Publish all customizations in this environment");
            _tips.SetToolTip(_cmdCheckTarget, "List the components the target solution depends on but does not contain, and add them");
            return _commandBar;
        }

        private static Control CreateCommandSeparator()
        {
            return new Panel { Width = 1, Height = 20, BackColor = Theme.Border, Margin = new Padding(4, 10, 4, 10) };
        }

        private Control BuildTopSection()
        {
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                BackColor = Theme.PanelBackground,
                Margin = new Padding(0)
            };

            _lblViewTitle = new Label
            {
                Text = "Components",
                Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(14, 10, 14, 6)
            };

            _warnBanner = new Banner();
            _noticeBanner = new Banner();
            _resultBanner = new Banner();

            top.Controls.Add(_lblViewTitle);
            top.Controls.Add(BuildLookupRow());
            top.Controls.Add(BuildPickersHost());
            top.Controls.Add(_warnBanner);
            top.Controls.Add(_noticeBanner);
            top.Controls.Add(_resultBanner);
            return top;
        }

        private Control BuildSearchBox()
        {
            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontRegular,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };
            _txtSearch.TextChanged += (s, e) => _grid.SearchText = _txtSearch.Text;
            _txtSearch.HandleCreated += (s, e) => SendMessage(_txtSearch.Handle, EmSetCueBanner, (IntPtr)1, "Search components (Ctrl+F)");

            var glyph = new Label
            {
                Dock = DockStyle.Right,
                Width = 26,
                Text = Theme.GlyphSearch,
                Font = Theme.FontIconSmall,
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White
            };

            // A thin grey frame around the box, like Fluent text fields.
            var frame = new Panel
            {
                Width = 240,
                Height = 28,
                Padding = new Padding(1),
                BackColor = Theme.BorderStrong,
                Margin = new Padding(0, 6, 18, 0)
            };
            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8, 5, 0, 0) };
            inner.Controls.Add(_txtSearch);
            inner.Controls.Add(glyph);
            frame.Controls.Add(inner);
            return frame;
        }

        /// <summary>The search box and the two options that shape what is listed and copied, in one row above the list's header.</summary>
        private Control BuildOptionsRow()
        {
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                BackColor = Theme.PanelBackground,
                Padding = new Padding(14, 4, 14, 6)
            };
            row.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                }
            };

            _membership = _settings.HideExisting ? MembershipFilter.NotInTarget : MembershipFilter.All;

            _chipAll = new FilterChip("All");
            _chipAll.Click += (s, e) => ClearFilters();
            _chipNotInTarget = MembershipChip("Not in target", MembershipFilter.NotInTarget);
            _chipInBoth = MembershipChip("In both", MembershipFilter.InBoth);
            _chipOnlyInTarget = MembershipChip("Only in target", MembershipFilter.OnlyInTarget);
            _chipOnlyInTarget.Margin = new Padding(0, 8, 14, 0);

            _chipRecent = new FilterChip("Last 7 days");
            _chipRecent.Click += (s, e) =>
            {
                _chipRecent.Active = !_chipRecent.Active;
                _grid.ModifiedSinceFilter = _chipRecent.Active ? DateTime.Now.AddDays(-7) : (DateTime?)null;
                UpdateChips();
            };
            _chipByMe = new FilterChip("Modified by me") { Enabled = false, Margin = new Padding(0, 8, 18, 0) };
            _chipByMe.Click += (s, e) =>
            {
                _chipByMe.Active = !_chipByMe.Active;
                _grid.ModifiedByFilter = _chipByMe.Active ? _currentUserName : null;
                UpdateChips();
            };

            _chkIncludeRequired = new CheckBox
            {
                Text = "Include required",
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Margin = new Padding(0, 10, 12, 0),
                Checked = _settings.IncludeRequired
            };
            _grid.IncludeRequired = _chkIncludeRequired.Checked;
            _chkIncludeRequired.CheckedChanged += (s, e) =>
            {
                _settings.IncludeRequired = _chkIncludeRequired.Checked;
                SaveSettings();
                _grid.IncludeRequired = _chkIncludeRequired.Checked;
            };

            _cmdCollapse = new CommandButton { Glyph = Theme.GlyphCollapse, Height = 28, Margin = new Padding(0, 6, 0, 0) };
            _cmdCollapse.Click += (s, e) => _grid.CollapseAll();
            _cmdExpand = new CommandButton { Glyph = Theme.GlyphExpand, Height = 28, Margin = new Padding(0, 6, 0, 0) };
            _cmdExpand.Click += (s, e) => _grid.ExpandAll();

            row.Controls.Add(BuildSearchBox());
            row.Controls.Add(_chipAll);
            row.Controls.Add(_chipNotInTarget);
            row.Controls.Add(_chipInBoth);
            row.Controls.Add(_chipOnlyInTarget);
            row.Controls.Add(_chipRecent);
            row.Controls.Add(_chipByMe);
            row.Controls.Add(_chkIncludeRequired);
            row.Controls.Add(_cmdCollapse);
            row.Controls.Add(_cmdExpand);

            _tips.SetToolTip(_chipAll, "Show every component of the source solution");
            _tips.SetToolTip(_chipNotInTarget, "Show only the components the target solution does not contain yet");
            _tips.SetToolTip(_chipInBoth, "Show only the components that are in both solutions");
            _tips.SetToolTip(_chipOnlyInTarget, "Show the components that are in the target solution but not in the source (read-only)");
            _tips.SetToolTip(_chipRecent, "Show only the components modified in the last 7 days");
            _tips.SetToolTip(_chipByMe, "Show only the components you were the last to modify");
            _tips.SetToolTip(_chkIncludeRequired,
                "Also add every component the selected ones depend on (a table can override this from its right-click menu)");
            _tips.SetToolTip(_cmdCollapse, "Collapse all folders");
            _tips.SetToolTip(_cmdExpand, "Expand all folders");

            UpdateChips();
            _optionsRow = row;
            return row;
        }

        private FilterChip MembershipChip(string text, MembershipFilter filter)
        {
            var chip = new FilterChip(text) { Enabled = false };
            chip.Click += (s, e) => SetMembership(_membership == filter ? MembershipFilter.All : filter);
            return chip;
        }

        private void ClearFilters()
        {
            _chipRecent.Active = false;
            _chipByMe.Active = false;
            _grid.ModifiedSinceFilter = null;
            _grid.ModifiedByFilter = null;
            SetMembership(MembershipFilter.All);
        }

        /// <summary>Remembers which of source/target/both the user wants listed and shows it as soon as the target is known.</summary>
        private void SetMembership(MembershipFilter filter)
        {
            _membership = filter;
            _settings.HideExisting = filter == MembershipFilter.NotInTarget;
            SaveSettings();
            ApplyMembership();
        }

        private void ApplyMembership()
        {
            UpdateChips();

            if (_targetKeys == null)
            {
                _grid.Membership = MembershipFilter.All;
            }
            else if (_membership == MembershipFilter.OnlyInTarget && !_targetOnlyLoaded)
            {
                EnsureTargetOnlyLoaded(() =>
                {
                    if (_membership == MembershipFilter.OnlyInTarget)
                    {
                        _grid.Membership = MembershipFilter.OnlyInTarget;
                    }
                });
            }
            else
            {
                _grid.Membership = _membership;
            }
        }

        private void UpdateChips()
        {
            var compared = _targetKeys != null;
            _chipNotInTarget.Enabled = compared;
            _chipInBoth.Enabled = compared;
            _chipOnlyInTarget.Enabled = compared;
            _chipByMe.Enabled = !string.IsNullOrEmpty(_currentUserName);

            _chipNotInTarget.Active = _membership == MembershipFilter.NotInTarget;
            _chipInBoth.Active = _membership == MembershipFilter.InBoth;
            _chipOnlyInTarget.Active = _membership == MembershipFilter.OnlyInTarget;
            _chipAll.Active = _membership == MembershipFilter.All && !_chipRecent.Active && !_chipByMe.Active;
        }

        private Control BuildLookupRow()
        {
            _lookupRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(14, 2, 14, 10),
                Margin = new Padding(0),
                Visible = false
            };
            _lookupRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _lookupRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lookupRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var swap = CreateSwapButton();
            swap.Anchor = AnchorStyles.Bottom;
            swap.Margin = new Padding(8, 0, 8, 0);

            _lookupRow.Controls.Add(BuildLookup("Source solution", out _lblSourceLookup, new Padding(0)), 0, 0);
            _lookupRow.Controls.Add(swap, 1, 0);
            _lookupRow.Controls.Add(BuildLookup("Target solution", out _lblTargetLookup, new Padding(0)), 2, 0);
            return _lookupRow;
        }

        private Button CreateSwapButton()
        {
            var button = new Button
            {
                Text = Theme.GlyphSwap,
                Font = Theme.FontIcon,
                ForeColor = Theme.Accent,
                BackColor = Theme.PanelBackground,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(34, 28),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.Hover;
            button.FlatAppearance.MouseDownBackColor = Theme.Pressed;
            button.Click += (s, e) => SwapSolutions();
            new ToolTip().SetToolTip(button, "Swap the source and target solutions");
            return button;
        }

        private Control BuildLookup(string caption, out Label valueLabel, Padding margin)
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = margin
            };

            stack.Controls.Add(new Label
            {
                Text = caption,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 2)
            }, 0, 0);

            var field = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 28,
                BackColor = Theme.GroupBackground,
                Cursor = Cursors.Hand,
                Padding = new Padding(0, 0, 0, 1),
                Margin = new Padding(0)
            };
            field.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderStrong))
                {
                    e.Graphics.DrawLine(pen, 0, field.Height - 1, field.Width, field.Height - 1);
                }
            };

            valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            var glyph = new Label
            {
                Dock = DockStyle.Right,
                Width = 28,
                Text = Theme.GlyphSearch,
                Font = Theme.FontIconSmall,
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };

            field.Controls.Add(valueLabel);
            field.Controls.Add(glyph);
            field.Click += (s, e) => ShowPickers(true);
            valueLabel.Click += (s, e) => ShowPickers(true);
            glyph.Click += (s, e) => ShowPickers(true);

            stack.Controls.Add(field, 0, 1);
            return stack;
        }

        private Control BuildPickersHost()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Margin = new Padding(0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));

            var sourceGroup = BuildSolutionPicker("Source solution", out _cmbSource);
            var targetGroup = BuildSolutionPicker("Target solution", out _cmbTarget);

            var swap = CreateSwapButton();
            swap.Anchor = AnchorStyles.None;
            swap.Margin = new Padding(10, 24, 10, 0);

            panel.Controls.Add(sourceGroup, 0, 0);
            panel.Controls.Add(swap, 1, 0);
            panel.Controls.Add(targetGroup, 2, 0);

            _cmbSource.SelectionChanged += (s, e) =>
            {
                if (_swapping)
                {
                    return;
                }

                LoadSourceComponentsAsync();
                OnSolutionSelectionChanged();
            };
            _cmbTarget.SelectionChanged += (s, e) =>
            {
                if (!_swapping)
                {
                    OnSolutionSelectionChanged();
                }
            };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(14, 4, 14, 10),
                BackColor = Theme.PanelBackground
            };
            host.Controls.Add(panel);
            _pickersHost = host;
            return host;
        }

        private Control BuildSolutionPicker(string label, out SolutionPickerControl picker)
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2
            };
            stack.Controls.Add(new Label
            {
                Text = label,
                Font = Theme.FontBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 4)
            }, 0, 0);

            // A filter box above a plain ListView of matches — same composition as XrmToolBox's
            // own MsCrmTools.SolutionComponentsMover uses for its solution picker. Filtering and
            // selecting are two separate, decoupled controls, so neither one can fight the other.
            picker = new SolutionPickerControl();
            stack.Controls.Add(picker.Control, 0, 1);
            return stack;
        }

        /// <summary>The strip above the results list: a title and the two CSV exports.</summary>
        private Control BuildResultsHeader()
        {
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                WrapContents = false,
                BackColor = Theme.PanelBackground,
                Padding = new Padding(8, 0, 8, 0)
            };

            _cmdExport = new CommandButton { Glyph = Theme.GlyphDownload, Text = "Export results", Height = 30, Enabled = false };
            _cmdExport.Click += (s, e) => ExportResults(failuresOnly: false);
            _cmdExportFailures = new CommandButton { Glyph = Theme.GlyphDownload, Text = "Export failures", Height = 30, Enabled = false };
            _cmdExportFailures.Click += (s, e) => ExportResults(failuresOnly: true);

            header.Controls.Add(new Label
            {
                Text = "Results",
                Font = Theme.FontBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(6, 8, 12, 0)
            });
            header.Controls.Add(_cmdExport);
            header.Controls.Add(_cmdExportFailures);

            _tips.SetToolTip(_cmdExport, "Save every row of the results, with its outcome, to a CSV file");
            _tips.SetToolTip(_cmdExportFailures, "Save only the components that failed to a CSV file");
            return header;
        }

        private Control BuildSplit()
        {
            _grid = new GroupedComponentGrid { Dock = DockStyle.Fill, ShowWhereUsed = true };
            _grid.ComponentSelectionChanged += (s, e) => UpdateSelectionUi();
            _grid.ViewChanged += (s, e) => UpdateFooter();
            _grid.WhereUsedRequested += ShowSolutionsContaining;

            _results = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = Theme.FontRegular,
                BorderStyle = BorderStyle.None
            };
            _results.Columns.Add("Component", 260);
            _results.Columns.Add("Type", 160);
            _results.Columns.Add("Result", 90);
            _results.Columns.Add("Details", 300);
            _results.Resize += (s, e) =>
            {
                var used = _results.Columns[0].Width + _results.Columns[1].Width + _results.Columns[2].Width;
                _results.Columns[3].Width = Math.Max(300, _results.ClientSize.Width - used);
            };

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 160,
                Panel2MinSize = 60,
                SplitterWidth = 6,
                BackColor = Theme.Border,
                Panel2Collapsed = true
            };
            _split.Panel1.Controls.Add(_grid);
            _split.Panel1.Controls.Add(BuildOptionsRow());
            _split.Panel2.Controls.Add(_results);
            _split.Panel2.Controls.Add(BuildResultsHeader());
            return _split;
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Theme.PanelBackground,
                Margin = new Padding(0),
                Padding = new Padding(14, 0, 14, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                }
            };

            _lblFooter = new Label
            {
                Font = Theme.FontRegular,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            footer.Controls.Add(_lblFooter, 0, 0);
            _lnkExportComparison = new Label
            {
                Text = "Export comparison",
                Font = new Font(Theme.FontRegular, FontStyle.Underline),
                ForeColor = Theme.Accent,
                Cursor = Cursors.Hand,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, 16, 0),
                Visible = false
            };
            _lnkExportComparison.Click += (s, e) => ExecuteMethod(ExportComparison);
            _tips.SetToolTip(_lnkExportComparison, "Save a CSV that lists every component as only in source, in both, or only in target");

            footer.ColumnCount = 3;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(_lnkExportComparison, 1, 0);
            footer.Controls.Add(new Label
            {
                Text = "Grouped by Type",
                Font = Theme.FontRegular,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Right
            }, 2, 0);
            return footer;
        }

        private void OnSolutionSelectionChanged()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;

            _cmdCheckTarget.Enabled = target != null;
            if (source != null)
            {
                _lblViewTitle.Text = "Components in " + source.FriendlyName;
            }

            if (source == null || target == null)
            {
                return;
            }

            if (source.SolutionId == _lastSourceId && target.SolutionId == _lastTargetId)
            {
                return;
            }

            var targetChanged = target.SolutionId != _lastTargetId;
            _lastSourceId = source.SolutionId;
            _lastTargetId = target.SolutionId;
            _lblSourceLookup.Text = source.FriendlyName;
            _lblTargetLookup.Text = target.FriendlyName;
            ShowPickers(false);

            // A running component load compares with the target itself once it finishes.
            if (targetChanged && _grid.TotalCount > 0 && !_loadingComponents)
            {
                RefreshTargetMembership();
            }
        }

        private void SwapSolutions()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;
            if (_busy || source == null || target == null || source.SolutionId == target.SolutionId)
            {
                return;
            }

            _swapping = true;
            try
            {
                _cmbSource.SelectBySolutionId(target.SolutionId);
                _cmbTarget.SelectBySolutionId(source.SolutionId);
            }
            finally
            {
                _swapping = false;
            }

            _lastSourceId = null;
            _lastTargetId = null;
            LoadSourceComponentsAsync();
            OnSolutionSelectionChanged();
        }

        private void ShowPickers(bool show)
        {
            _pickersHost.Visible = show;
            _lookupRow.Visible = !show;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _commandBar.Enabled = !busy;
            _optionsRow.Enabled = !busy;
            _grid.Enabled = !busy;
            _pickersHost.Enabled = !busy;
            _lookupRow.Enabled = !busy;
            UpdateSelectionUi();
        }

        private void UpdateSelectionUi()
        {
            var selected = _grid.SelectedCount;
            var total = _grid.TotalCount;

            // The "Only in target" view lists components that are not in the source, so nothing can be ticked or copied from it.
            var sourceListed = _grid.Membership != MembershipFilter.OnlyInTarget;

            _cmdCopy.Text = selected > 0 ? $"Copy {selected} to target" : "Copy to target";
            _cmdCopy.Enabled = selected > 0 && sourceListed;
            _cmdMove.Enabled = selected > 0 && sourceListed;
            _cmdPreview.Enabled = selected > 0 && sourceListed;
            _cmdClear.Enabled = selected > 0;
            _cmdRemove.Text = selected > 0 ? $"Remove {selected}" : "Remove";
            _cmdRemove.Enabled = selected > 0 && sourceListed;
            _cmdSelectAll.Enabled = total > 0 && sourceListed;
            _cmdCollapse.Enabled = total > 0;
            _cmdExpand.Enabled = total > 0;

            _cmdModifiedSince.Enabled = total > 0 && sourceListed;
            _cmdCheckTarget.Enabled = _cmbTarget?.SelectedSolution != null;

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            _lnkExportComparison.Visible = _targetKeys != null;

            var total = _grid.TotalCount;
            if (total == 0)
            {
                _lblFooter.Text = "No components";
                return;
            }

            if (_grid.Membership == MembershipFilter.OnlyInTarget)
            {
                var listed = _grid.TargetOnlyItems.Count;
                var only = $"{listed} component{(listed == 1 ? string.Empty : "s")} only in {_cmbTarget.SelectedSolution?.FriendlyName ?? "the target"}";
                _lblFooter.Text = _grid.VisibleCount == listed ? only : only + $" · showing {_grid.VisibleCount}";
                return;
            }

            var types = _grid.GroupCount;
            var text = $"{total} component{(total == 1 ? string.Empty : "s")} in {types} type{(types == 1 ? string.Empty : "s")} ({_grid.SelectedCount} selected)";

            if (_grid.IsFiltered)
            {
                text += $" · showing {_grid.VisibleCount}";
            }

            if (_targetKeys != null)
            {
                text += $" · {_grid.InTargetCount} already in target";
                if (_onlyInTargetCount > 0)
                {
                    text += $", {_onlyInTargetCount} only in target";
                }
            }

            _lblFooter.Text = text;
        }

        #endregion
    }
}
