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

        private void SelectModifiedSince()
        {
            using (var dialog = new DatePromptDialog("Select modified since", "Select components modified on or after:", DateTime.Today.AddDays(-7),
                       _grid.ModifiedByNames, _currentUserName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var count = _grid.SelectModifiedSince(dialog.Value, dialog.ModifiedBy);
                var who = dialog.ModifiedBy == null ? string.Empty : $" by {dialog.ModifiedBy}";
                ShowNotice(BannerKind.Info, $"Selected {count} component(s) modified{who} since {dialog.Value:yyyy-MM-dd HH:mm}.");
            }
        }

        private void ShowSolutionsContaining(SolutionComponentItem component)
        {
            if (Service == null || _busy)
            {
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Looking for the solutions that contain {component.DisplayName}...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetSolutionsContaining(Service, component);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to look up the solutions.", false);
                        return;
                    }

                    using (var dialog = new SolutionListDialog(component, (List<SolutionListItem>)args.Result))
                    {
                        dialog.ShowDialog(this);
                    }
                }
            });
        }

        /// <summary>Lists what the target solution depends on but does not contain, and offers to add it.</summary>
        private void CheckTargetDependencies()
        {
            var target = _cmbTarget.SelectedSolution;
            if (target == null)
            {
                MessageBox.Show(this, "Choose a target solution first.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Checking what {target.FriendlyName} depends on...",
                Work = (worker, args) =>
                {
                    args.Result = _dataService.GetMissingDependencies(Service, target.UniqueName, message => worker.ReportProgress(0, message));
                },
                ProgressChanged = e => SetWorkingMessage(e.UserState as string),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Solution Component Cloner", "Unable to check the target solution's dependencies.", false);
                        return;
                    }

                    using (var dialog = new MissingDependenciesDialog((SolutionComponentLoadResult)args.Result, target.FriendlyName, target.IsManaged))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Chosen.Count == 0)
                        {
                            return;
                        }

                        // Only what was ticked: adding their own requirements too would defeat choosing them.
                        StartCopy(dialog.Chosen, target.UniqueName, target.FriendlyName, includeRequired: false);
                    }
                }
            });
        }

        /// <summary>Saves a CSV listing every component of both solutions as only in source, in both, or only in target.</summary>
        private void ExportComparison()
        {
            var source = _cmbSource.SelectedSolution;
            var target = _cmbTarget.SelectedSolution;
            if (source == null || target == null || _targetKeys == null || _busy)
            {
                return;
            }

            EnsureTargetOnlyLoaded(() =>
            {
                var lines = CsvExport.Comparison(_grid.AllItems, _grid.TargetOnlyItems);
                if (SaveCsv($"Comparison-{source.UniqueName}-vs-{target.UniqueName}.csv", lines))
                {
                    ShowNotice(BannerKind.Success, $"The comparison of {source.FriendlyName} and {target.FriendlyName} was saved.");
                }
            });
        }

        #endregion
    }
}
