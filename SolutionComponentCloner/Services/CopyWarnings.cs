using System.Collections.Generic;
using System.Linq;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    internal sealed class CopyWarning
    {
        public SolutionComponentItem Item { get; set; }
        public string Kind { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Finds the selected components that bring much more than themselves into the target, so the user can
    /// confirm before copying: model-driven apps, and tables that are in their solution completely.
    /// </summary>
    internal static class CopyWarnings
    {
        public static List<CopyWarning> Build(IEnumerable<SolutionComponentItem> selected, bool includeRequired)
        {
            var warnings = new List<CopyWarning>();

            foreach (var item in selected)
            {
                if (item.ComponentType == ComponentTypeCatalog.ModelDrivenApp)
                {
                    warnings.Add(new CopyWarning
                    {
                        Item = item,
                        Kind = "Model-driven app",
                        Message = "Adds every table, form, view and process it is built from."
                    });
                }
                else if (item.IsAddedCompletely)
                {
                    var includesAll = item.SubcomponentMode == SubcomponentMode.IncludeAll ||
                                      (item.SubcomponentMode == SubcomponentMode.UseGlobalSetting && includeRequired);
                    warnings.Add(new CopyWarning
                    {
                        Item = item,
                        Kind = "Table added completely",
                        Message = includesAll
                            ? "All of its subcomponents will be copied."
                            : "Only the table and the subcomponents you ticked will be copied."
                    });
                }
            }

            return warnings
                .OrderBy(w => w.Kind)
                .ThenBy(w => w.Item.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
