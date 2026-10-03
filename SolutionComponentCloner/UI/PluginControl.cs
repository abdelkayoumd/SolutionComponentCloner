using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace SolutionComponentCloner.UI
{
    public partial class PluginControl : PluginControlBase
    {
        private readonly DataverseSolutionService _dataService = new DataverseSolutionService();
        private BindingList<SolutionComponentItem> _components = new BindingList<SolutionComponentItem>();

        private SolutionPickerControl _cmbSource;
        private SolutionPickerControl _cmbTarget;
        private Control _pickersPanel;
        private TableLayoutPanel _pickerSummaryBar;
        private Label _lblPickerSummary;
        private Guid? _lastSourceId;
        private Guid? _lastTargetId;
        private Button _btnSelectAll;
        private Button _btnSelectNone;
        private Button _btnCopy;
        private CheckBox _chkIncludeRequired;
        private DataGridView _grid;
        private SplitContainer _split;
        private ListView _results;
        private Label _lblConnection;
        private Label _lblComponentCount;
        private Label _lblResultsSummary;
        private Button _btnExportFailures;
        private Button _btnToggleResults;
        private List<ComponentCopyResult> _lastResults = new List<ComponentCopyResult>();
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 20000 };

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
            _lblConnection.ForeColor = Theme.Success;

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
                RowCount = 4,
                Padding = new Padding(14, 12, 14, 12),
                BackColor = Theme.PageBackground
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildSolutionSection(), 0, 1);
            root.Controls.Add(BuildSplit(), 0, 2);
            root.Controls.Add(BuildResultsStrip(), 0, 3);
        }

        private Control BuildHeader()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var titleStack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false
            };

            var title = new Label
            {
                Text = "Solution Component Cloner",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true
            };
            var subtitle = new Label
            {
                Text = "Copy components from a source solution into a target solution.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            titleStack.Controls.Add(title);
            titleStack.Controls.Add(subtitle);

            _lblConnection = new Label
            {
                Text = "Not connected",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 14, 0, 0)
            };

            panel.Controls.Add(titleStack, 0, 0);
            panel.Controls.Add(_lblConnection, 1, 0);
            return panel;
        }

        private Control BuildSolutionSection()
        {
            var section = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };

            _pickersPanel = BuildSolutionsPanel();
            section.Controls.Add(_pickersPanel);
            section.Controls.Add(BuildPickerSummaryBar());
            return section;
        }

        private Control BuildSolutionsPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Margin = new Padding(0, 0, 0, 10)
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

            return panel;
        }

        private Control BuildPickerSummaryBar()
        {
            _pickerSummaryBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 10),
                Visible = false
            };
            _pickerSummaryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _pickerSummaryBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblPickerSummary = new Label
            {
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 7, 0, 0)
            };

            var btnChange = CreateSecondaryButton("Change");
            btnChange.Margin = new Padding(0);
            btnChange.Click += (s, e) => ShowPickers(true);

            _pickerSummaryBar.Controls.Add(_lblPickerSummary, 0, 0);
            _pickerSummaryBar.Controls.Add(btnChange, 1, 0);
            return _pickerSummaryBar;
        }

        private void OnSolutionSelectionChanged()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;
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
            _lblPickerSummary.Text = $"Source: {source.FriendlyName}     →     Target: {target.FriendlyName}";
            ShowPickers(false);
        }

        private void ShowPickers(bool show)
        {
            _pickersPanel.Visible = show;
            _pickerSummaryBar.Visible = !show;
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
                Font = Theme.FontSectionHeader,
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
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 160,
                Panel2MinSize = 60,
                SplitterWidth = 6,
                BackColor = Theme.PageBackground,
                Panel2Collapsed = true
            };

            _split.Panel1.Controls.Add(BuildComponentsPanel());
            _split.Panel2.Controls.Add(BuildResultsList());
            return _split;
        }

        private Control BuildComponentsPanel()
        {
            var group = CreateBorderedPanel(out var body);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                BackgroundColor = Theme.PanelBackground,
                GridColor = Theme.Border,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                Font = Theme.FontRegular,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 30,
                MultiSelect = true,
                RowTemplate = { Height = 24 }
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Accent;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = Theme.FontSectionHeader;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
            _grid.DefaultCellStyle.SelectionForeColor = Theme.TextPrimary;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.GridAltRow;

            var colSelected = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "Selected",
                HeaderText = string.Empty,
                Width = 34,
                Resizable = DataGridViewTriState.False
            };
            var colType = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ComponentTypeName",
                HeaderText = "Type",
                Width = 170,
                ReadOnly = true
            };
            var colName = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DisplayName",
                HeaderText = "Component",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };

            _grid.Columns.Add(colSelected);
            _grid.Columns.Add(colType);
            _grid.Columns.Add(colName);
            _grid.DataSource = _components;

            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            _grid.CellPainting += (s, e) => PaintComponentCell(e, colName.Index);

            _lblComponentCount = new Label
            {
                Text = "Choose a source solution to load its components.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 9, 14, 0)
            };

            _chkIncludeRequired = new CheckBox
            {
                Text = "Include required components",
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Margin = new Padding(0, 5, 14, 0)
            };

            _btnSelectAll = CreateSecondaryButton("Select all");
            _btnSelectAll.Click += (s, e) => SetAllSelected(true);

            _btnSelectNone = CreateSecondaryButton("Select none");
            _btnSelectNone.Click += (s, e) => SetAllSelected(false);

            _btnCopy = CreatePrimaryButton("Copy selected → target");
            _btnCopy.Click += BtnCopy_Click;

            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            header.Controls.Add(new Label
            {
                Text = "Components in source solution",
                Font = Theme.FontSectionHeader,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 8, 14, 0)
            });
            header.Controls.Add(_lblComponentCount);
            header.Controls.Add(_chkIncludeRequired);
            header.Controls.Add(_btnSelectAll);
            header.Controls.Add(_btnSelectNone);
            header.Controls.Add(_btnCopy);

            body.Controls.Add(_grid);
            body.Controls.Add(header);
            return group;
        }

        private Control BuildResultsList()
        {
            _results = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = Theme.FontRegular,
                BorderStyle = BorderStyle.FixedSingle
            };
            _results.Columns.Add("Component", 260);
            _results.Columns.Add("Type", 160);
            _results.Columns.Add("Result", 90);
            _results.Columns.Add("Details", 300);
            return _results;
        }

        private Control BuildResultsStrip()
        {
            var strip = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                Margin = new Padding(0, 8, 0, 0)
            };
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "Results",
                Font = Theme.FontSectionHeader,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 12, 0)
            };

            _lblResultsSummary = new Label
            {
                Text = "No components copied yet.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 0, 0)
            };

            _btnExportFailures = CreateSecondaryButton("Export failures...");
            _btnExportFailures.Margin = new Padding(0, 0, 6, 0);
            _btnExportFailures.Enabled = false;
            _btnExportFailures.Click += BtnExportFailures_Click;

            _btnToggleResults = CreateSecondaryButton("Show details");
            _btnToggleResults.Margin = new Padding(0);
            _btnToggleResults.Click += (s, e) =>
            {
                if (_split.Panel2Collapsed)
                {
                    ShowResultDetails();
                }
                else
                {
                    HideResultDetails();
                }
            };

            strip.Controls.Add(title, 0, 0);
            strip.Controls.Add(_lblResultsSummary, 1, 0);
            strip.Controls.Add(_btnExportFailures, 2, 0);
            strip.Controls.Add(_btnToggleResults, 3, 0);
            return strip;
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

            _btnToggleResults.Text = "Hide details";
        }

        private void HideResultDetails()
        {
            _split.Panel2Collapsed = true;
            _btnToggleResults.Text = "Show details";
        }

        private Panel CreateBorderedPanel(out Panel body)
        {
            var outer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.PanelBackground,
                Padding = new Padding(12, 10, 12, 10)
            };
            outer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, outer.Width - 1, outer.Height - 1);
                }
            };

            body = new Panel { Dock = DockStyle.Fill };
            outer.Controls.Add(body);
            return outer;
        }

        private Button CreatePrimaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Font = Theme.FontButton,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                AutoSize = true,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.AccentDark;
            button.FlatAppearance.MouseDownBackColor = Theme.AccentDark;
            return button;
        }

        private Button CreateSecondaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Font = Theme.FontRegular,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.Accent,
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Theme.Accent;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Theme.AccentSoft;
            return button;
        }

        /// <summary>
        /// Model-Driven App rows get a red inline warning appended after the component name,
        /// since copying one always pulls in every entity/form/process the app references —
        /// AddRequiredComponents can't be unchecked to avoid that (see ComponentTypeCatalog).
        /// </summary>
        private void PaintComponentCell(DataGridViewCellPaintingEventArgs e, int nameColumnIndex)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != nameColumnIndex)
            {
                return;
            }

            if (!(_grid.Rows[e.RowIndex].DataBoundItem is SolutionComponentItem item) ||
                item.ComponentType != ComponentTypeCatalog.ModelDrivenApp)
            {
                return;
            }

            e.PaintBackground(e.CellBounds, true);

            var font = e.CellStyle.Font;
            var nameText = item.DisplayName ?? string.Empty;
            const string warningText = "  ⚠ copying this adds every entity/form/process the app is built from";

            var nameSize = TextRenderer.MeasureText(e.Graphics, nameText, font, e.CellBounds.Size, TextFormatFlags.NoPadding);
            var nameRect = new Rectangle(e.CellBounds.X + 2, e.CellBounds.Y, nameSize.Width, e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, nameText, font, nameRect, e.CellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            var warnRect = new Rectangle(nameRect.Right, e.CellBounds.Y, Math.Max(0, e.CellBounds.Right - nameRect.Right), e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, warningText, font, warnRect, Theme.Danger,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }

        #endregion

        #region Data loading

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

                    var solutions = (System.Collections.Generic.List<SolutionListItem>)args.Result;
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

            _lblComponentCount.Text = "Loading components...";
            _btnCopy.Enabled = false;

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
                        _lblComponentCount.Text = "Failed to load components.";
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to load solution components.", false);
                        return;
                    }

                    var loaded = (SolutionComponentLoadResult)args.Result;
                    var items = loaded.Items;
                    _components = new BindingList<SolutionComponentItem>(items);
                    _grid.DataSource = _components;
                    _lblComponentCount.Text = items.Count == 0
                        ? "This solution has no components."
                        : $"{items.Count} component(s) found.";
                    _lblComponentCount.ForeColor = Theme.TextSecondary;
                    _toolTip.SetToolTip(_lblComponentCount, null);

                    if (loaded.Warnings.Count > 0)
                    {
                        _lblComponentCount.Text += $"  ⚠ names unavailable for {loaded.Warnings.Count} type(s) (hover for details)";
                        _lblComponentCount.ForeColor = Theme.Danger;
                        _toolTip.SetToolTip(_lblComponentCount, string.Join(Environment.NewLine, loaded.Warnings));
                    }

                    _btnCopy.Enabled = items.Count > 0;
                }
            });
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var item in _components)
            {
                item.Selected = selected;
            }
            _grid.Refresh();
        }

        #endregion

        #region Copy

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            var target = _cmbTarget.SelectedSolution;
            var source = _cmbSource.SelectedSolution;
            var selected = _components.Where(c => c.Selected).ToList();

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

            _results.Items.Clear();
            _btnExportFailures.Enabled = false;
            _btnCopy.Enabled = false;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Copying {selected.Count} component(s) to {target.FriendlyName}...",
                Work = (worker, args) =>
                {
                    var results = new System.Collections.Generic.List<ComponentCopyResult>();
                    foreach (var component in selected)
                    {
                        results.Add(_dataService.CopyComponent(Service, component, targetUniqueName, includeRequired));
                    }
                    args.Result = results;
                },
                PostWorkCallBack = args =>
                {
                    _btnCopy.Enabled = true;

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Copy operation failed.", false);
                        return;
                    }

                    var results = (System.Collections.Generic.List<ComponentCopyResult>)args.Result;
                    RenderResults(results);
                }
            });
        }

        private void RenderResults(System.Collections.Generic.List<ComponentCopyResult> results)
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
            _lblResultsSummary.Text = failed == 0
                ? $"{succeeded} component(s) copied successfully."
                : $"{succeeded} succeeded, {failed} failed.";
            _lblResultsSummary.ForeColor = failed == 0 ? Theme.Success : Theme.Danger;
            _btnExportFailures.Enabled = failed > 0;
            ShowResultDetails();
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
