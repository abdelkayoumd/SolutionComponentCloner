using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;

namespace SolutionComponentCloner.UI
{
    public partial class PluginControl
    {
        /// <summary>A batched operation of the data service: it reports progress, can be cancelled and returns one result per component.</summary>
        private delegate List<ComponentCopyResult> BatchWork(Action<int, int> progress, Func<bool> isCancelled, out bool cancelled);

        /// <summary>What was last taken out of a solution, so "Add back" can undo it.</summary>
        private sealed class RemovalUndo
        {
            public List<SolutionComponentItem> Components;
            public string SolutionUniqueName;
            public string SolutionFriendlyName;
        }

        private RemovalUndo _undo;

        #region Copy, move and remove

        private void CopySelected()
        {
            if (!TryGetSelectionForTarget("copy", out _, out var target, out var selected) || !ConfirmCopyWarnings(selected, target))
            {
                return;
            }

            StartCopy(selected, target.UniqueName, target.FriendlyName, _chkIncludeRequired.Checked);
        }

        private void MoveSelected()
        {
            if (!TryGetSelectionForTarget("move", out var source, out var target, out var selected))
            {
                return;
            }

            if (source.IsManaged)
            {
                MessageBox.Show(this, "The source solution is managed, so components cannot be moved out of it. Copy them instead.",
                    "Solution Component Cloner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ConfirmCopyWarnings(selected, target))
            {
                return;
            }

            var answer = MessageBox.Show(this,
                $"Move {Count(selected.Count)} from {source.FriendlyName} to {target.FriendlyName}?\n\n" +
                "Each one is copied to the target and then removed from the source. " +
                "A component that cannot be copied stays in the source.",
                "Solution Component Cloner", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            StartMove(selected, source, target, _chkIncludeRequired.Checked);
        }

        /// <summary>Checks that a copy or move can start and returns the ticked components, tables first.</summary>
        private bool TryGetSelectionForTarget(string verb, out SolutionListItem source, out SolutionListItem target, out List<SolutionComponentItem> selected)
        {
            source = _cmbSource.SelectedSolution;
            target = _cmbTarget.SelectedSolution;
            selected = TablesFirst(_grid.SelectedComponents);

            string problem = null;
            var icon = MessageBoxIcon.Information;
            if (source == null || target == null)
            {
                problem = "Choose both a source and a target solution first.";
            }
            else if (target.IsManaged)
            {
                problem = "The target solution is managed and cannot receive new components. Choose an unmanaged target solution.";
                icon = MessageBoxIcon.Warning;
            }
            else if (source.SolutionId == target.SolutionId)
            {
                problem = "Source and target solutions must be different.";
            }
            else if (selected.Count == 0)
            {
                problem = $"Select at least one component to {verb}.";
            }

            if (problem == null)
            {
                return true;
            }

            MessageBox.Show(this, problem, "Solution Component Cloner", MessageBoxButtons.OK, icon);
            return false;
        }

        /// <summary>Tables first, so a table is already in the target when its columns, forms and views are added.</summary>
        private static List<SolutionComponentItem> TablesFirst(IEnumerable<SolutionComponentItem> components)
        {
            return components.OrderBy(c => c.ComponentType == ComponentTypeCatalog.Entity ? 0 : 1).ToList();
        }

        private bool ConfirmCopyWarnings(List<SolutionComponentItem> selected, SolutionListItem target)
        {
            var warnings = CopyWarnings.Build(selected, _chkIncludeRequired.Checked);
            if (warnings.Count == 0)
            {
                return true;
            }

            using (var dialog = new CopyWarningDialog(warnings, target.FriendlyName))
            {
                return dialog.ShowDialog(this) == DialogResult.OK;
            }
        }

        private void StartCopy(List<SolutionComponentItem> selected, string targetUniqueName, string targetFriendlyName, bool includeRequired)
        {
            selected = TablesFirst(selected);
            RunBatch(selected.Count,
                (done, total) => $"Copying {done} of {total} to {targetFriendlyName}...",
                (BatchWork)((Action<int, int> progress, Func<bool> isCancelled, out bool cancelled) =>
                    _dataService.CopyComponents(Service, selected, targetUniqueName, includeRequired, progress, isCancelled, out cancelled)),
                "Copy operation failed.",
                (results, cancelled) =>
                {
                    RenderResults(results, selected.Count, cancelled);

                    if (results.Any(r => r.Outcome == CopyOutcome.Success))
                    {
                        RefreshTargetMembership();
                    }
                });
        }

        private void StartMove(List<SolutionComponentItem> selected, SolutionListItem source, SolutionListItem target, bool includeRequired)
        {
            var count = selected.Count;
            RunBatch(count * 2,
                (done, total) => done <= count
                    ? $"Moving: copying {done} of {count} to {target.FriendlyName}..."
                    : $"Moving: removing {done - count} of {count} from {source.FriendlyName}...",
                (BatchWork)((Action<int, int> progress, Func<bool> isCancelled, out bool cancelled) =>
                    _dataService.MoveComponents(Service, selected, source.UniqueName, target.UniqueName, includeRequired, progress, isCancelled, out cancelled)),
                "Moving the components failed.",
                (results, cancelled) =>
                {
                    RenderResults(results, count, cancelled, "moved");
                    OfferAddBack(results, source, "Add back to source");

                    // Anything that was copied changed the target, and anything moved changed the source.
                    LoadSourceComponentsAsync(keepResults: true);
                });
        }

        private void RemoveSelected()
        {
            var source = _cmbSource.SelectedSolution;
            var selected = _grid.SelectedComponents.ToList();

            if (source == null)
            {
                MessageBox.Show(this, "Choose a source solution first.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (source.IsManaged)
            {
                MessageBox.Show(this, "The source solution is managed, so components cannot be removed from it.",
                    "Solution Component Cloner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one component to remove.", "Solution Component Cloner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(this,
                $"Remove {Count(selected.Count)} from {source.FriendlyName}?\n\n" +
                "They are not deleted: they stay in this environment and in any other solution that contains them. " +
                "Only this solution stops listing them, and you can add them back right after.",
                "Solution Component Cloner", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            StartRemove(selected, source);
        }

        private void StartRemove(List<SolutionComponentItem> selected, SolutionListItem source)
        {
            RunBatch(selected.Count,
                (done, total) => $"Removing {done} of {total} from {source.FriendlyName}...",
                (BatchWork)((Action<int, int> progress, Func<bool> isCancelled, out bool cancelled) =>
                    _dataService.RemoveComponents(Service, selected, source.UniqueName, progress, isCancelled, out cancelled)),
                "Removing the components failed.",
                (results, cancelled) =>
                {
                    RenderResults(results, selected.Count, cancelled, "removed", string.Empty);
                    OfferAddBack(results, source, "Add back");

                    if (results.Any(r => r.Outcome == CopyOutcome.Success))
                    {
                        LoadSourceComponentsAsync(keepResults: true);
                    }
                });
        }

        /// <summary>Remembers what left the solution and shows the link that puts it back.</summary>
        private void OfferAddBack(List<ComponentCopyResult> results, SolutionListItem solution, string linkText)
        {
            var gone = results.Where(r => r.Outcome == CopyOutcome.Success).Select(r => r.Component).ToList();
            if (gone.Count == 0)
            {
                return;
            }

            _undo = new RemovalUndo { Components = gone, SolutionUniqueName = solution.UniqueName, SolutionFriendlyName = solution.FriendlyName };
            _resultBanner.SetSecondLink(linkText, AddBackRemoved);
        }

        private void AddBackRemoved()
        {
            var undo = _undo;
            if (undo == null || _busy)
            {
                return;
            }

            var answer = MessageBox.Show(this,
                $"Add {Count(undo.Components.Count)} back to {undo.SolutionFriendlyName}?",
                "Solution Component Cloner", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            RunBatch(undo.Components.Count,
                (done, total) => $"Adding back {done} of {total} to {undo.SolutionFriendlyName}...",
                (BatchWork)((Action<int, int> progress, Func<bool> isCancelled, out bool cancelled) =>
                    _dataService.RestoreComponents(Service, undo.Components, undo.SolutionUniqueName, progress, isCancelled, out cancelled)),
                "Adding the components back failed.",
                (results, cancelled) =>
                {
                    RenderResults(results, undo.Components.Count, cancelled, "added back", string.Empty);

                    if (results.Any(r => r.Outcome == CopyOutcome.Success))
                    {
                        LoadSourceComponentsAsync(keepResults: true);
                    }
                });
        }

        private static string Count(int count) => $"{count} component{(count == 1 ? string.Empty : "s")}";

        /// <summary>
        /// Runs a batched operation off the UI thread with a live "n of m" banner and a Cancel link, then hands
        /// the results to <paramref name="completed"/>.
        /// </summary>
        private void RunBatch(int total, Func<int, int, string> progressText, BatchWork work, string failureMessage,
            Action<List<ComponentCopyResult>, bool> completed)
        {
            ResetResults();
            _noticeBanner.HideBanner();
            SetBusy(true);
            ShowBatchProgress(progressText(0, total));

            var worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            _copyWorker = worker;

            worker.DoWork += (s, e) =>
            {
                var results = work((done, count) => worker.ReportProgress(0, new[] { done, count }), () => worker.CancellationPending, out var cancelled);
                e.Result = new CopyOutcomeData { Results = results, Cancelled = cancelled };
            };
            worker.ProgressChanged += (s, e) =>
            {
                // Once Cancel is clicked the banner says so; do not overwrite it with a late progress count.
                if (!worker.CancellationPending)
                {
                    var counts = (int[])e.UserState;
                    ShowBatchProgress(progressText(counts[0], counts[1]));
                }
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                _copyWorker = null;
                _noticeBanner.HideBanner();
                SetBusy(false);

                if (e.Error != null)
                {
                    ShowErrorDialog(e.Error, "Solution Component Cloner", failureMessage, false);
                    return;
                }

                var data = (CopyOutcomeData)e.Result;
                completed(data.Results, data.Cancelled);
            };
            worker.RunWorkerAsync();
        }

        private void ShowBatchProgress(string text)
        {
            _noticeBanner.ShowMessage(BannerKind.Info, text, "Cancel", CancelCopy);
        }

        private void CancelCopy()
        {
            if (_copyWorker != null && _copyWorker.IsBusy)
            {
                _copyWorker.CancelAsync();
                _noticeBanner.ShowMessage(BannerKind.Info, "Cancelling after the batch in progress...");
            }
        }

        #endregion

        #region Results

        private void ResetResults()
        {
            _results.Items.Clear();
            _lastResults = new List<ComponentCopyResult>();
            _undo = null;
            _resultBanner.HideBanner();
            _cmdExport.Enabled = false;
            _cmdExportFailures.Enabled = false;
            HideResultDetails();
        }

        private void RenderResults(List<ComponentCopyResult> results, int requested, bool cancelled,
            string pastTense = "copied", string successHint = " Publish your customizations to make them live.")
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
            _cmdExport.Enabled = results.Count > 0;
            _cmdExportFailures.Enabled = failed > 0;

            if (cancelled)
            {
                _resultBanner.ShowMessage(BannerKind.Warning,
                    $"Cancelled: {succeeded} {pastTense}, {failed} failed, {requested - results.Count} not attempted.", "View details", ToggleResultDetails);
            }
            else if (failed == 0)
            {
                _resultBanner.ShowMessage(BannerKind.Success,
                    $"{Count(succeeded)} {pastTense} successfully.{successHint}",
                    "View details", ToggleResultDetails);
            }
            else
            {
                _resultBanner.ShowMessage(BannerKind.Error,
                    $"{succeeded} {pastTense}, {failed} failed.", "View details", ToggleResultDetails);
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

        private void ExportResults(bool failuresOnly)
        {
            var rows = failuresOnly ? _lastResults.Where(r => r.Outcome == CopyOutcome.Failed).ToList() : _lastResults;
            if (rows.Count == 0)
            {
                return;
            }

            SaveCsv(failuresOnly ? "SolutionComponentCloner-failures.csv" : "SolutionComponentCloner-results.csv", CsvExport.Results(rows));
        }

        /// <summary>Asks where to save and writes the lines; returns false when the user cancelled or the file could not be written.</summary>
        private bool SaveCsv(string suggestedName, List<string> lines)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = suggestedName
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                try
                {
                    File.WriteAllLines(dialog.FileName, lines);
                    return true;
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Solution Component Cloner", "Unable to save the export file.", false);
                    return false;
                }
            }
        }

        #endregion
    }
}
