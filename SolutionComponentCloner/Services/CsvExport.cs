using System.Collections.Generic;
using System.Linq;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    /// <summary>Builds the lines of the CSV files the tool can save.</summary>
    internal static class CsvExport
    {
        /// <summary>One line per component of a copy, move or removal, with its outcome.</summary>
        public static List<string> Results(IEnumerable<ComponentCopyResult> results)
        {
            var lines = new List<string> { "Component,Type,Id,Result,Details" };
            lines.AddRange(results.Select(r => string.Join(",",
                Field(r.Component.DisplayName),
                Field(r.Component.ComponentTypeName),
                r.Component.ObjectId.ToString(),
                r.Outcome == CopyOutcome.Failed ? "Failed" : "Success",
                Field(r.Message))));
            return lines;
        }

        /// <summary>Every component of the source and the target, saying whether it is only in one of them or in both.</summary>
        public static List<string> Comparison(IEnumerable<SolutionComponentItem> source, IEnumerable<SolutionComponentItem> onlyInTarget)
        {
            var lines = new List<string> { "Component,Type,Table,Id,Where" };
            lines.AddRange(source.Select(i => ComparisonLine(i, i.InTarget ? "In both" : "Only in source")));
            lines.AddRange(onlyInTarget.Select(i => ComparisonLine(i, "Only in target")));
            return lines;
        }

        private static string ComparisonLine(SolutionComponentItem item, string where)
        {
            return string.Join(",", Field(item.DisplayName), Field(item.ComponentTypeName), Field(item.TableName), item.ObjectId.ToString(), where);
        }

        public static string Field(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
