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

        /// <summary>Top-level group in the tree, e.g. "Tables" or "Web resources".</summary>
        public string GroupName { get; set; }

        /// <summary>Logical name of the table this component belongs to (or, for a table itself, its own logical name).</summary>
        public string TableName { get; set; }

        /// <summary>Friendly label of <see cref="TableName"/>, e.g. "Account (account)".</summary>
        public string TableLabel { get; set; }

        /// <summary>Sub-folder under a table, e.g. "Columns", "Forms", "Views".</summary>
        public string Category { get; set; }

        /// <summary>Sub-folder under a top-level group, e.g. "Code", "Data", "Images" for web resources.</summary>
        public string SubGroup { get; set; }
    }
}
