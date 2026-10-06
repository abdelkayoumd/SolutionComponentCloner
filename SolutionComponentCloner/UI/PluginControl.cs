using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace SolutionComponentCloner.UI
{
    public partial class PluginControl : PluginControlBase
    {
        private readonly DataverseSolutionService _dataService = new DataverseSolutionService();

        private SolutionPickerControl _cmbSource;
        private SolutionPickerControl _cmbTarget;
        private Control _pickersHost;
        private TableLayoutPanel _lookupRow;
        private Label _lblSourceLookup;
        private Label _lblTargetLookup;
        private Label _lblViewTitle;
        private Guid? _lastSourceId;
        private Guid? _lastTargetId;

        private CommandButton _cmdCopy;
        private CommandButton _cmdSelectAll;
        private CommandButton _cmdClear;
        private CommandButton _cmdCollapse;
        private CommandButton _cmdExpand;
        private CommandButton _cmdRefresh;
        private CommandButton _cmdExport;
        private CheckBox _chkIncludeRequired;

        private Banner _warnBanner;
        private Banner _resultBanner;
        private GroupedComponentGrid _grid;
        private SplitContainer _split;
        private ListView _results;
        private Label _lblConnection;
        private Label _lblFooter;
        private List<ComponentCopyResult> _lastResults = new List<ComponentCopyResult>();

        public PluginControl()
        {
            ToolName = "Solution Component Cloner";
            BuildLayout();
            ConnectionUpdated += PluginControl_ConnectionUpdated;
        }

        private void PluginControl_ConnectionUpdated(object sender, ConnectionUpdatedEventArgs e)
        {
            _lblConnection.Text = e.ConnectionDetail != null
                ? $"Connected to {e.ConnectionDetail.OrganizationFriendlyName}"
                : "Connected";

            LoadSolutionsAsync();
        }

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
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                BackColor = Theme.PanelBackground,
                Margin = new Padding(0),
                Padding = new Padding(6, 0, 6, 0)
            };
            bar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
                }
            };

            _cmdCopy = new CommandButton { Glyph = Theme.GlyphCopy, Text = "Copy to target", Strong = true };
            _cmdCopy.Click += (s, e) => ExecuteMethod(CopySelected);

            _cmdSelectAll = new CommandButton { Glyph = Theme.GlyphSelectAll, Text = "Select all" };
            _cmdSelectAll.Click += (s, e) => _grid.SetAllSelected(true);

            _cmdClear = new CommandButton { Glyph = Theme.GlyphClearSelection, Text = "Clear selection" };
            _cmdClear.Click += (s, e) => _grid.SetAllSelected(false);

            _cmdCollapse = new CommandButton { Glyph = Theme.GlyphCollapse, Text = "Collapse all" };
            _cmdCollapse.Click += (s, e) => _grid.CollapseAll();

            _cmdExpand = new CommandButton { Glyph = Theme.GlyphExpand, Text = "Expand all" };
            _cmdExpand.Click += (s, e) => _grid.ExpandAll();

            _cmdRefresh = new CommandButton { Glyph = Theme.GlyphRefresh, Text = "Refresh" };
            _cmdRefresh.Click += (s, e) => ExecuteMethod(RefreshData);

            _cmdExport = new CommandButton { Glyph = Theme.GlyphDownload, Text = "Export failures", Enabled = false };
            _cmdExport.Click += BtnExportFailures_Click;

            _chkIncludeRequired = new CheckBox
            {
                Text = "Include required components",
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Margin = new Padding(10, 11, 6, 0)
            };

            bar.Controls.Add(_cmdCopy);
            bar.Controls.Add(_cmdSelectAll);
            bar.Controls.Add(_cmdClear);
            bar.Controls.Add(CreateCommandSeparator());
            bar.Controls.Add(_cmdCollapse);
            bar.Controls.Add(_cmdExpand);
            bar.Controls.Add(CreateCommandSeparator());
            bar.Controls.Add(_cmdRefresh);
            bar.Controls.Add(_cmdExport);
            bar.Controls.Add(_chkIncludeRequired);
            return bar;
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
            _resultBanner = new Banner();

            top.Controls.Add(_lblViewTitle);
            top.Controls.Add(BuildLookupRow());
            top.Controls.Add(BuildPickersHost());
            top.Controls.Add(_warnBanner);
            top.Controls.Add(_resultBanner);
            return top;
        }

        private Control BuildLookupRow()
        {
            _lookupRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(14, 2, 14, 10),
                Margin = new Padding(0),
                Visible = false
            };
            _lookupRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _lookupRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _lookupRow.Controls.Add(BuildLookup("Source solution", out _lblSourceLookup, new Padding(0, 0, 8, 0)), 0, 0);
            _lookupRow.Controls.Add(BuildLookup("Target solution", out _lblTargetLookup, new Padding(8, 0, 0, 0)), 1, 0);
            return _lookupRow;
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

            var arrow = new Label
            {
                Text = "→",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Accent,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(10, 24, 10, 0)
            };

            panel.Controls.Add(sourceGroup, 0, 0);
            panel.Controls.Add(arrow, 1, 0);
            panel.Controls.Add(targetGroup, 2, 0);

            _cmbSource.SelectionChanged += (s, e) =>
            {
                LoadSourceComponentsAsync();
                OnSolutionSelectionChanged();
            };
            _cmbTarget.SelectionChanged += (s, e) => OnSolutionSelectionChanged();

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

        private Control BuildSplit()
        {
            _grid = new GroupedComponentGrid { Dock = DockStyle.Fill };
            _grid.ComponentSelectionChanged += (s, e) => UpdateSelectionUi();

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
            _split.Panel2.Controls.Add(_results);
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
            footer.Controls.Add(new Label
            {
                Text = "Grouped by Type",
                Font = Theme.FontRegular,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Right
            }, 1, 0);
            return footer;
        }

        private void OnSolutionSelectionChanged()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;

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

            _lastSourceId = source.SolutionId;
            _lastTargetId = target.SolutionId;
            _lblSourceLookup.Text = source.FriendlyName;
            _lblTargetLookup.Text = target.FriendlyName;
            ShowPickers(false);
        }

        private void ShowPickers(bool show)
        {
            _pickersHost.Visible = show;
            _lookupRow.Visible = !show;
        }

        private void UpdateSelectionUi()
        {
            var selected = _grid.SelectedCount;
            var total = _grid.TotalCount;

            _cmdCopy.Text = selected > 0 ? $"Copy {selected} to target" : "Copy to target";
            _cmdCopy.Enabled = selected > 0;
            _cmdClear.Enabled = selected > 0;
            _cmdSelectAll.Enabled = total > 0;
            _cmdCollapse.Enabled = total > 0;
            _cmdExpand.Enabled = total > 0;

            _lblFooter.Text = total == 0
                ? "No components"
                : $"{total} component{(total == 1 ? string.Empty : "s")} in {_grid.GroupCount} type{(_grid.GroupCount == 1 ? string.Empty : "s")} ({selected} selected)";
        }

        #endregion

        #region Data loading

        private void RefreshData()
        {
            if (_cmbSource.SelectedSolution != null)
            {
                LoadSourceComponentsAsync();
            }
            else
            {
                LoadSolutionsAsync();
            }
        }

        private void LoadSolutionsAsync()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetSolutions(Service);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to load solutions.", false);
                        return;
                    }

                    var solutions = (List<SolutionListItem>)args.Result;
                    _cmbSource.SetSolutions(solutions);
                    _cmbTarget.SetSolutions(solutions);
                }
            });
        }

        private void LoadSourceComponentsAsync()
        {
            var source = _cmbSource.SelectedSolution;
            if (source == null || Service == null)
            {
                return;
            }

            ResetResults();
            _warnBanner.HideBanner();
            _cmdCopy.Enabled = false;
            _lblFooter.Text = "Loading components...";

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading components from {source.FriendlyName}...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetSolutionComponents(Service, source.SolutionId);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        _lblFooter.Text = "Failed to load components.";
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to load solution components.", false);
                        return;
                    }

                    var loaded = (SolutionComponentLoadResult)args.Result;
                    _grid.SetItems(loaded.Items);

                    if (loaded.Warnings.Count > 0)
                    {
                        _warnBanner.ShowMessage(BannerKind.Warning,
                            $"Names are unavailable for {loaded.Warnings.Count} component type(s); their IDs are shown instead. Hover for details.",
                            toolTip: string.Join(Environment.NewLine, loaded.Warnings));
                    }
                }
            });
        }

        #endregion

        #region Copy

        private void CopySelected()
        {
            var target = _cmbTarget.SelectedSolution;
            var source = _cmbSource.SelectedSolution;
            // Tables first, so a table is already in the target when its columns, forms and views are added.
            var selected = _grid.SelectedComponents
                .OrderBy(c => c.ComponentType == ComponentTypeCatalog.Entity ? 0 : 1)
                .ToList();

            if (source == null || target == null)
            {
                MessageBox.Show(this, "Choose both a source and a target solution first.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (target.IsManaged)
            {
                MessageBox.Show(this, "The target solution is managed and cannot receive new components. Choose an unmanaged target solution.",
                    "Solution Component Cloner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (source.SolutionId == target.SolutionId)
            {
                MessageBox.Show(this, "Source and target solutions must be different.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one component to copy.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var includeRequired = _chkIncludeRequired.Checked;
            var targetUniqueName = target.UniqueName;

            ResetResults();
            _cmdCopy.Enabled = false;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Copying {selected.Count} component(s) to {target.FriendlyName}...",
                Work = (worker, args) =>
                {
                    var results = new List<ComponentCopyResult>();
                    foreach (var component in selected)
                    {
                        results.Add(_dataService.CopyComponent(Service, component, targetUniqueName, includeRequired));
                    }
                    args.Result = results;
                },
                PostWorkCallBack = args =>
                {
                    UpdateSelectionUi();

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Copy operation failed.", false);
                        return;
                    }

                    RenderResults((List<ComponentCopyResult>)args.Result);
                }
            });
        }

        private void ResetResults()
        {
            _results.Items.Clear();
            _lastResults = new List<ComponentCopyResult>();
            _resultBanner.HideBanner();
            _cmdExport.Enabled = false;
            HideResultDetails();
        }

        private void RenderResults(List<ComponentCopyResult> results)
        {
            _results.Items.Clear();
            _lastResults = results;

            foreach (var result in results)
            {
                var item = new ListViewItem(result.Component.DisplayName);
                item.SubItems.Add(result.Component.ComponentTypeName);
                item.SubItems.Add(result.Outcome == CopyOutcome.Success ? "Success" : "Failed");
                item.SubItems.Add(result.Message);
                item.ForeColor = result.Outcome == CopyOutcome.Success ? Theme.Success : Theme.Danger;
                _results.Items.Add(item);
            }

            var succeeded = results.Count(r => r.Outcome == CopyOutcome.Success);
            var failed = results.Count - succeeded;
            _cmdExport.Enabled = failed > 0;

            if (failed == 0)
            {
                _resultBanner.ShowMessage(BannerKind.Success,
                    $"{succeeded} component{(succeeded == 1 ? string.Empty : "s")} copied successfully.",
                    "View details", ToggleResultDetails);
            }
            else
            {
                _resultBanner.ShowMessage(BannerKind.Error,
                    $"{succeeded} copied, {failed} failed.", "View details", ToggleResultDetails);
                ShowResultDetails();
            }
        }

        private void ToggleResultDetails()
        {
            if (_split.Panel2Collapsed)
            {
                ShowResultDetails();
            }
            else
            {
                HideResultDetails();
            }
        }

        private void ShowResultDetails()
        {
            if (!_split.Panel2Collapsed)
            {
                return;
            }

            _split.Panel2Collapsed = false;

            var maxPanel1 = _split.Height - _split.Panel2MinSize - _split.SplitterWidth;
            if (maxPanel1 >= _split.Panel1MinSize)
            {
                _split.SplitterDistance = Math.Max(_split.Panel1MinSize, Math.Min((int)(_split.Height * 0.6), maxPanel1));
            }

            _resultBanner.SetLinkText("Hide details");
        }

        private void HideResultDetails()
        {
            _split.Panel2Collapsed = true;
            _resultBanner.SetLinkText("View details");
        }

        private void BtnExportFailures_Click(object sender, EventArgs e)
        {
            var failures = _lastResults.Where(r => r.Outcome == CopyOutcome.Failed).ToList();
            if (failures.Count == 0)
            {
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "SolutionComponentCloner-failures.csv"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var lines = new List<string> { "Component,Type,Result,Details" };
                    lines.AddRange(failures.Select(r => string.Join(",",
                        CsvField(r.Component.DisplayName),
                        CsvField(r.Component.ComponentTypeName),
                        "Failed",
                        CsvField(r.Message))));

                    File.WriteAllLines(dialog.FileName, lines);
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Solution Component Cloner", "Unable to save the export file.", false);
                }
            }
        }

        private static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        #endregion
    }
}
