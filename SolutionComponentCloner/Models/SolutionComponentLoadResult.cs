using System.Collections.Generic;

namespace SolutionComponentCloner.Models
{
    public sealed class SolutionComponentLoadResult
    {
        public List<SolutionComponentItem> Items { get; set; } = new List<SolutionComponentItem>();
        public List<string> Warnings { get; } = new List<string>();
    }
}
