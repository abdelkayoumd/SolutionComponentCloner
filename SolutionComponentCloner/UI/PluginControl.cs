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
        private string _pendingTargetUniqueName;

        private FlowLayoutPanel _commandBar;
        private CommandButton _cmdCopy;
        private CommandButton _cmdMove;
        private CommandButton _cmdCheckTarget;
        private CommandButton _cmdPreview;
        private CommandButton _cmdSelectAll;
        private CommandButton _cmdClear;
        private CommandButton _cmdRemove;
        private CommandButton _cmdCollapse;
        private CommandButton _cmdExpand;
        private CommandButton _cmdRefresh;
        private CommandButton _cmdPublish;
        private CommandButton _cmdNewSolution;
        private CommandButton _cmdModifiedSince;
        private CommandButton _cmdExport;
        private CommandButton _cmdExportFailures;
        private Control _optionsRow;
        private CheckBox _chkIncludeRequired;
        private FilterChip _chipAll;
        private FilterChip _chipNotInTarget;
        private FilterChip _chipInBoth;
        private FilterChip _chipOnlyInTarget;
        private FilterChip _chipRecent;
        private FilterChip _chipByMe;
        private TextBox _txtSearch;
        private Label _lnkExportComparison;
        private readonly ToolTip _tips = new ToolTip { AutoPopDelay = 15000 };

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
        private bool _targetOnlyLoaded;
        private MembershipFilter _membership;
        private string _currentUserName;
        private BackgroundWorker _copyWorker;
        private bool _busy;
        private bool _swapping;
        private bool _loadingComponents;

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
                    var solutions = _dataService.GetSolutions(Service);

                    // Only used by the "Modified by me" filter, so a failure here must not stop the solutions from loading.
                    string userName = null;
                    try
                    {
                        userName = _dataService.GetCurrentUserName(Service);
                    }
                    catch (Exception)
                    {
                    }

                    args.Result = Tuple.Create(solutions, userName);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to load solutions.", false);
                        return;
                    }

                    var loaded = (Tuple<List<SolutionListItem>, string>)args.Result;
                    _solutions = loaded.Item1;
                    _currentUserName = loaded.Item2;
                    if (_chipByMe.Active)
                    {
                        _grid.ModifiedByFilter = _currentUserName;
                        _chipByMe.Active = !string.IsNullOrEmpty(_currentUserName);
                    }

                    UpdateChips();
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
                }
            });
        }

        private void LoadSourceComponentsAsync(bool keepResults = false)
        {
            var source = _cmbSource.SelectedSolution;
            if (source == null || Service == null)
            {
                return;
            }

            if (!keepResults)
            {
                ResetResults();
            }

            _warnBanner.HideBanner();
            _noticeBanner.HideBanner();
            ClearTargetMembership();
            _cmdCopy.Enabled = false;
            _lblFooter.Text = "Loading components...";
            _loadingComponents = true;

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
                    _loadingComponents = false;
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
            _targetOnlyLoaded = false;
            ApplyMembership();
            _grid.ClearTargetInfo();
            _grid.SetTargetOnlyItems(null);
        }

        /// <summary>
        /// Resolves the names of the components that are only in the target (once per comparison), then runs
        /// <paramref name="then"/>. They are needed by the "Only in target" view and by the comparison export.
        /// </summary>
        private void EnsureTargetOnlyLoaded(Action then)
        {
            if (_targetOnlyLoaded || _targetKeys == null)
            {
                then();
                return;
            }

            var comparedKeys = _targetKeys;
            var sourceKeys = new HashSet<(int, Guid)>(_grid.AllItems.Select(i => (i.ComponentType, i.ObjectId)));
            var keys = comparedKeys.Where(k => !sourceKeys.Contains((k.Type, k.Id))).ToList();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading the components that are only in the target...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetComponents(Service, keys, message => worker.ReportProgress(0, message));
                },
                ProgressChanged = e => SetWorkingMessage(e.UserState as string),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        SetMembership(MembershipFilter.All);
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to read the components that are only in the target.", false);
                        return;
                    }

                    // The source or target may have changed while this was running; then this answer is stale.
                    if (!ReferenceEquals(comparedKeys, _targetKeys))
                    {
                        return;
                    }

                    _grid.SetTargetOnlyItems(((SolutionComponentLoadResult)args.Result).Items);
                    _targetOnlyLoaded = true;
                    then();
                }
            });
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
                    _targetOnlyLoaded = false;
                    _grid.ApplyTargetKeys(_targetKeys);
                    ApplyMembership();
                    UpdateFooter();
                }
            });
        }

        #endregion
    }
}
