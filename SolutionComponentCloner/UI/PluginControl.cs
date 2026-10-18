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
    public partial class PluginControl : PluginControlBase
    {
        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private sealed class CopyOutcomeData
        {
            public List<ComponentCopyResult> Results;
            public bool Cancelled;
        }

        private readonly DataverseSolutionService _dataService = new DataverseSolutionService();
        private PluginSettings _settings = new PluginSettings();

        private SolutionPickerControl _cmbSource;
        private SolutionPickerControl _cmbTarget;
        private Control _pickersHost;
        private TableLayoutPanel _lookupRow;
        private Label _lblSourceLookup;
        private Label _lblTargetLookup;
        private Label _lblViewTitle;
        private Guid? _lastSourceId;
        private Guid? _lastTargetId;
        private List<SolutionListItem> _solutions = new List<SolutionListItem>();
        private Guid _connectionId;
        private bool _applySavedSelection;
        private bool _applyingSettings;
        private string _pendingTargetUniqueName;

        private FlowLayoutPanel _commandBar;
        private CommandButton _cmdCopy;
        private CommandButton _cmdPreview;
        private CommandButton _cmdSelectAll;
        private CommandButton _cmdClear;
        private CommandButton _cmdCollapse;
        private CommandButton _cmdExpand;
        private CommandButton _cmdRefresh;
        private CommandButton _cmdMore;
        private Control _optionsRow;
        private CheckBox _chkIncludeRequired;
        private CheckBox _chkHideExisting;
        private TextBox _txtSearch;
        private ContextMenuStrip _moreMenu;
        private ToolStripMenuItem _miPublish;
        private ToolStripMenuItem _miNewSolution;
        private ToolStripMenuItem _miSaveSelection;
        private ToolStripMenuItem _miLoadSelection;
        private ToolStripMenuItem _miModifiedSince;
        private ToolStripMenuItem _miExport;

        private Banner _warnBanner;
        private Banner _noticeBanner;
        private Banner _resultBanner;
        private GroupedComponentGrid _grid;
        private SplitContainer _split;
        private ListView _results;
        private Label _lblConnection;
        private Label _lblFooter;
        private List<ComponentCopyResult> _lastResults = new List<ComponentCopyResult>();

        private HashSet<(int Type, Guid Id)> _targetKeys;
        private int _onlyInTargetCount;
        private BackgroundWorker _copyWorker;
        private bool _busy;

        public PluginControl()
        {
            ToolName = "Solution Component Cloner";
            LoadSettings();
            BuildLayout();
            ConnectionUpdated += PluginControl_ConnectionUpdated;
        }

        private void PluginControl_ConnectionUpdated(object sender, ConnectionUpdatedEventArgs e)
        {
            _lblConnection.Text = e.ConnectionDetail != null
                ? $"Connected to {e.ConnectionDetail.OrganizationFriendlyName}"
                : "Connected";
            _connectionId = e.ConnectionDetail?.ConnectionId ?? Guid.Empty;
            _applySavedSelection = true;

            LoadSolutionsAsync();
        }

        #region Settings

        private void LoadSettings()
        {
            try
            {
                if (SettingsManager.Instance.TryLoad(typeof(PluginControl), out PluginSettings loaded) && loaded != null)
                {
                    _settings = loaded;
                }
            }
            catch (Exception)
            {
                // Preferences are a convenience; start with defaults if they cannot be read.
            }
        }

        private void SaveSettings()
        {
            try
            {
                SettingsManager.Instance.Save(typeof(PluginControl), _settings);
            }
            catch (Exception)
            {
                // Not being able to remember a preference should never interrupt the user.
            }
        }

        private void RememberSolutions(SolutionListItem source, SolutionListItem target)
        {
            if (_connectionId == Guid.Empty || _applyingSettings)
            {
                return;
            }

            var preference = _settings.GetOrAdd(_connectionId);
            preference.SourceUniqueName = source.UniqueName;
            preference.TargetUniqueName = target.UniqueName;
            SaveSettings();
        }

        private void ApplySavedSolutions()
        {
            var preference = _connectionId == Guid.Empty ? null : _settings.ForConnection(_connectionId);
            if (preference == null)
            {
                return;
            }

            _applyingSettings = true;
            try
            {
                _cmbSource.SelectByUniqueName(preference.SourceUniqueName);
                _cmbTarget.SelectByUniqueName(preference.TargetUniqueName);
            }
            finally
            {
                _applyingSettings = false;
            }
        }

        #endregion

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

            _cmdPreview = new CommandButton { Glyph = Theme.GlyphPreview, Text = "Preview" };
            _cmdPreview.Click += (s, e) => ExecuteMethod(PreviewRequired);

            _cmdSelectAll = new CommandButton { Glyph = Theme.GlyphSelectAll, Text = "Select all" };
            _cmdSelectAll.Click += (s, e) => _grid.SetAllSelected(true);

            _cmdClear = new CommandButton { Glyph = Theme.GlyphClearSelection, Text = "Clear" };
            _cmdClear.Click += (s, e) => _grid.SetAllSelected(false);

            _cmdCollapse = new CommandButton { Glyph = Theme.GlyphCollapse, Text = "Collapse all" };
            _cmdCollapse.Click += (s, e) => _grid.CollapseAll();

            _cmdExpand = new CommandButton { Glyph = Theme.GlyphExpand, Text = "Expand all" };
            _cmdExpand.Click += (s, e) => _grid.ExpandAll();

            _cmdRefresh = new CommandButton { Glyph = Theme.GlyphRefresh, Text = "Refresh" };
            _cmdRefresh.Click += (s, e) => ExecuteMethod(RefreshData);

            _cmdMore = new CommandButton { Glyph = Theme.GlyphMore, Text = "More" };
            _cmdMore.Click += (s, e) => _moreMenu.Show(_cmdMore, new Point(0, _cmdMore.Height));
            BuildMoreMenu();

            _commandBar.Controls.Add(_cmdCopy);
            _commandBar.Controls.Add(_cmdPreview);
            _commandBar.Controls.Add(_cmdSelectAll);
            _commandBar.Controls.Add(_cmdClear);
            _commandBar.Controls.Add(CreateCommandSeparator());
            _commandBar.Controls.Add(_cmdCollapse);
            _commandBar.Controls.Add(_cmdExpand);
            _commandBar.Controls.Add(CreateCommandSeparator());
            _commandBar.Controls.Add(_cmdRefresh);
            _commandBar.Controls.Add(_cmdMore);

            var tips = new ToolTip { AutoPopDelay = 15000 };
            tips.SetToolTip(_cmdCopy, "Copy the ticked components into the target solution");
            tips.SetToolTip(_cmdPreview, "Preview the required components that would be added along with your selection");
            tips.SetToolTip(_cmdSelectAll, "Tick everything shown (use the search box first to tick only the matches)");
            tips.SetToolTip(_cmdClear, "Clear every tick");
            return _commandBar;
        }

        private void BuildMoreMenu()
        {
            _moreMenu = new ContextMenuStrip();

            _miPublish = new ToolStripMenuItem("Publish all customizations");
            _miPublish.Click += (s, e) => ExecuteMethod(PublishAllCustomizations);

            _miNewSolution = new ToolStripMenuItem("New target solution...");
            _miNewSolution.Click += (s, e) => ExecuteMethod(NewTargetSolution);

            _miSaveSelection = new ToolStripMenuItem("Save selection...");
            _miSaveSelection.Click += (s, e) => SaveSelection();

            _miLoadSelection = new ToolStripMenuItem("Load selection...");
            _miLoadSelection.Click += (s, e) => LoadSelection();

            _miModifiedSince = new ToolStripMenuItem("Select modified since...");
            _miModifiedSince.Click += (s, e) => SelectModifiedSince();

            _miExport = new ToolStripMenuItem("Export failures...") { Enabled = false };
            _miExport.Click += BtnExportFailures_Click;

            _moreMenu.Items.Add(_miPublish);
            _moreMenu.Items.Add(_miNewSolution);
            _moreMenu.Items.Add(new ToolStripSeparator());
            _moreMenu.Items.Add(_miSaveSelection);
            _moreMenu.Items.Add(_miLoadSelection);
            _moreMenu.Items.Add(_miModifiedSince);
            _moreMenu.Items.Add(new ToolStripSeparator());
            _moreMenu.Items.Add(_miExport);
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
                Width = 280,
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

            _chkHideExisting = new CheckBox
            {
                Text = "Hide components already in target",
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Margin = new Padding(0, 10, 18, 0),
                Checked = _settings.HideExisting,
                Enabled = false
            };
            _chkHideExisting.CheckedChanged += (s, e) =>
            {
                _settings.HideExisting = _chkHideExisting.Checked;
                SaveSettings();
                _grid.HideInTarget = _chkHideExisting.Checked;
            };

            _chkIncludeRequired = new CheckBox
            {
                Text = "Include required",
                AutoSize = true,
                Font = Theme.FontRegular,
                ForeColor = Theme.TextPrimary,
                Margin = new Padding(0, 10, 0, 0),
                Checked = _settings.IncludeRequired
            };
            _chkIncludeRequired.CheckedChanged += (s, e) =>
            {
                _settings.IncludeRequired = _chkIncludeRequired.Checked;
                SaveSettings();
            };

            row.Controls.Add(BuildSearchBox());
            row.Controls.Add(_chkHideExisting);
            row.Controls.Add(_chkIncludeRequired);

            var tips = new ToolTip { AutoPopDelay = 15000 };
            tips.SetToolTip(_chkHideExisting, "Hide the components the target solution already contains");
            tips.SetToolTip(_chkIncludeRequired,
                "Also add every component the selected ones depend on (a table can override this from its right-click menu)");

            _optionsRow = row;
            return row;
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
            _grid.ViewChanged += (s, e) => UpdateFooter();

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

            var targetChanged = target.SolutionId != _lastTargetId;
            _lastSourceId = source.SolutionId;
            _lastTargetId = target.SolutionId;
            _lblSourceLookup.Text = source.FriendlyName;
            _lblTargetLookup.Text = target.FriendlyName;
            ShowPickers(false);
            RememberSolutions(source, target);

            if (targetChanged && _grid.TotalCount > 0)
            {
                RefreshTargetMembership();
            }
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

            _cmdCopy.Text = selected > 0 ? $"Copy {selected} to target" : "Copy to target";
            _cmdCopy.Enabled = selected > 0;
            _cmdPreview.Enabled = selected > 0;
            _cmdClear.Enabled = selected > 0;
            _cmdSelectAll.Enabled = total > 0;
            _cmdCollapse.Enabled = total > 0;
            _cmdExpand.Enabled = total > 0;

            _miSaveSelection.Enabled = selected > 0;
            _miLoadSelection.Enabled = total > 0;
            _miModifiedSince.Enabled = total > 0;

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            var total = _grid.TotalCount;
            if (total == 0)
            {
                _lblFooter.Text = "No components";
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

                    _solutions = (List<SolutionListItem>)args.Result;
                    _cmbSource.SetSolutions(_solutions);
                    _cmbTarget.SetSolutions(_solutions);

                    if (_pendingTargetUniqueName != null)
                    {
                        var created = _pendingTargetUniqueName;
                        _pendingTargetUniqueName = null;
                        if (_cmbTarget.SelectByUniqueName(created))
                        {
                            ShowNotice(BannerKind.Success, $"Solution {created} was created and selected as the target.");
                        }
                    }
                    else if (_applySavedSelection)
                    {
                        _applySavedSelection = false;
                        ApplySavedSolutions();
                    }
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
            _noticeBanner.HideBanner();
            ClearTargetMembership();
            _cmdCopy.Enabled = false;
            _lblFooter.Text = "Loading components...";

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading components from {source.FriendlyName}...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetSolutionComponents(Service, source.SolutionId, message => worker.ReportProgress(0, message));
                },
                ProgressChanged = e => SetWorkingMessage(e.UserState as string),
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

                    RefreshTargetMembership();
                }
            });
        }

        private void ClearTargetMembership()
        {
            _targetKeys = null;
            _onlyInTargetCount = 0;
            _grid.ClearTargetInfo();
            _chkHideExisting.Enabled = false;
        }

        private void RefreshTargetMembership()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;
            if (Service == null || target == null || _grid.TotalCount == 0 || (source != null && source.SolutionId == target.SolutionId))
            {
                ClearTargetMembership();
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Comparing with {target.FriendlyName}...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetSolutionComponentKeys(Service, target.SolutionId);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ClearTargetMembership();
                        ShowNotice(BannerKind.Warning, "The target solution's components could not be read, so existing components are not marked.");
                        return;
                    }

                    _targetKeys = (HashSet<(int Type, Guid Id)>)args.Result;
                    var sourceKeys = new HashSet<(int, Guid)>(_grid.AllItems.Select(i => (i.ComponentType, i.ObjectId)));
                    _onlyInTargetCount = _targetKeys.Count(k => !sourceKeys.Contains((k.Type, k.Id)));
                    _grid.ApplyTargetKeys(_targetKeys);
                    _chkHideExisting.Enabled = true;
                    if (_chkHideExisting.Checked)
                    {
                        _grid.HideInTarget = true;
                    }

                    UpdateFooter();
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

            StartCopy(selected, target.UniqueName, target.FriendlyName, _chkIncludeRequired.Checked);
        }

        private void StartCopy(List<SolutionComponentItem> selected, string targetUniqueName, string targetFriendlyName, bool includeRequired)
        {
            ResetResults();
            _noticeBanner.HideBanner();
            SetBusy(true);
            ShowCopyProgress(0, selected.Count, targetFriendlyName);

            var worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            _copyWorker = worker;

            worker.DoWork += (s, e) =>
            {
                var results = _dataService.CopyComponents(Service, selected, targetUniqueName, includeRequired,
                    (done, total) => worker.ReportProgress(total == 0 ? 0 : done * 100 / total, new[] { done, total }),
                    () => worker.CancellationPending, out var cancelled);
                e.Result = new CopyOutcomeData { Results = results, Cancelled = cancelled };
            };
            worker.ProgressChanged += (s, e) =>
            {
                var counts = (int[])e.UserState;
                ShowCopyProgress(counts[0], counts[1], targetFriendlyName);
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                _copyWorker = null;
                _noticeBanner.HideBanner();
                SetBusy(false);

                if (e.Error != null)
                {
                    ShowErrorDialog(e.Error, "Solution Component Cloner", "Copy operation failed.", false);
                    return;
                }

                var data = (CopyOutcomeData)e.Result;
                RenderResults(data.Results, selected.Count, data.Cancelled);

                if (data.Results.Any(r => r.Outcome == CopyOutcome.Success))
                {
                    RefreshTargetMembership();
                }
            };
            worker.RunWorkerAsync();
        }

        private void ShowCopyProgress(int done, int total, string targetName)
        {
            _noticeBanner.ShowMessage(BannerKind.Info, $"Copying {done} of {total} to {targetName}...", "Cancel", CancelCopy);
        }

        private void CancelCopy()
        {
            if (_copyWorker != null && _copyWorker.IsBusy)
            {
                _copyWorker.CancelAsync();
                _noticeBanner.ShowMessage(BannerKind.Info, "Cancelling after the batch in progress...");
            }
        }

        private void ResetResults()
        {
            _results.Items.Clear();
            _lastResults = new List<ComponentCopyResult>();
            _resultBanner.HideBanner();
            _miExport.Enabled = false;
            HideResultDetails();
        }

        private void RenderResults(List<ComponentCopyResult> results, int requested, bool cancelled)
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
            _miExport.Enabled = failed > 0;

            if (cancelled)
            {
                _resultBanner.ShowMessage(BannerKind.Warning,
                    $"Cancelled: {succeeded} copied, {failed} failed, {requested - results.Count} not attempted.", "View details", ToggleResultDetails);
            }
            else if (failed == 0)
            {
                _resultBanner.ShowMessage(BannerKind.Success,
                    $"{succeeded} component{(succeeded == 1 ? string.Empty : "s")} copied successfully. Publish your customizations to make them live.",
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

        #region Tools

        private void ShowNotice(BannerKind kind, string message)
        {
            _noticeBanner.ShowMessage(kind, message);
        }

        private void PreviewRequired()
        {
            var selected = _grid.SelectedComponents.ToList();
            if (selected.Count == 0)
            {
                return;
            }

            if (selected.Count > 300 &&
                MessageBox.Show(this, $"Checking dependencies for {selected.Count} components can take a while. Continue?", "Preview required components",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Checking dependencies...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetRequiredComponents(Service, selected, message => worker.ReportProgress(0, message));
                },
                ProgressChanged = e => SetWorkingMessage(e.UserState as string),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to check the required components.", false);
                        return;
                    }

                    using (var dialog = new RequiredComponentsDialog((SolutionComponentLoadResult)args.Result, selected.Count))
                    {
                        dialog.ShowDialog(this);
                    }
                }
            });
        }

        private void PublishAllCustomizations()
        {
            if (MessageBox.Show(this,
                    "Publish all customizations in this environment? This makes every unpublished change live for all users.",
                    "Publish all customizations", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Publishing all customizations...",
                Work = (worker, args) => _dataService.PublishAll(Service),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to publish the customizations.", false);
                        return;
                    }

                    ShowNotice(BannerKind.Success, "All customizations were published.");
                }
            });
        }

        private void NewTargetSolution()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading publishers...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetPublishers(Service);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to load the publishers.", false);
                        return;
                    }

                    var publishers = (List<PublisherListItem>)args.Result;
                    using (var dialog = new NewSolutionDialog(publishers, _solutions.Select(s => s.UniqueName)))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return;
                        }

                        CreateSolution(dialog.FriendlyName, dialog.UniqueName, dialog.Version, dialog.Publisher.PublisherId);
                    }
                }
            });
        }

        private void CreateSolution(string friendlyName, string uniqueName, string version, Guid publisherId)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Creating {friendlyName}...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.CreateSolution(Service, friendlyName, uniqueName, version, publisherId);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to create the solution.", false);
                        return;
                    }

                    _pendingTargetUniqueName = uniqueName;
                    LoadSolutionsAsync();
                }
            });
        }

        private void SaveSelection()
        {
            var selected = _grid.SelectedComponents.ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var source = _cmbSource.SelectedSolution;
            using (var dialog = new SaveFileDialog
            {
                Filter = "Saved selection (*.json)|*.json",
                FileName = (source?.UniqueName ?? "components") + "-selection.json"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    SelectionFile.Save(dialog.FileName, SelectionFile.FromSelection(selected, source?.FriendlyName));
                    ShowNotice(BannerKind.Success, $"Saved {selected.Count} selected component(s) to {Path.GetFileName(dialog.FileName)}.");
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Solution Component Cloner", "Unable to save the selection.", false);
                }
            }
        }

        private void LoadSelection()
        {
            using (var dialog = new OpenFileDialog { Filter = "Saved selection (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var document = SelectionFile.Load(dialog.FileName);
                    var (matched, missing) = SelectionFile.Apply(_grid.AllItems.ToList(), document);
                    _grid.NotifySelectionChanged();

                    var message = $"Selected {matched} of {document.Components.Count} saved component(s).";
                    if (missing > 0)
                    {
                        message += $" {missing} were not found in this solution.";
                    }

                    ShowNotice(missing > 0 ? BannerKind.Warning : BannerKind.Success, message);
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Solution Component Cloner", "Unable to read the selection file.", false);
                }
            }
        }

        private void SelectModifiedSince()
        {
            using (var dialog = new DatePromptDialog("Select modified since", "Select components modified on or after:", DateTime.Today.AddDays(-7)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var count = _grid.SelectModifiedSince(dialog.Value);
                ShowNotice(BannerKind.Info, $"Selected {count} component(s) modified since {dialog.Value:yyyy-MM-dd HH:mm}.");
            }
        }

        #endregion
    }
}
