using System;

namespace SolutionComponentCloner.Models
{
    public sealed class SolutionComponentItem
    {
        public Guid ObjectId { get; set; }
        public int ComponentType { get; set; }
        public string ComponentTypeName { get; set; }
        public string DisplayName { get; set; }
        public bool Selected { get; set; }
    }
}
