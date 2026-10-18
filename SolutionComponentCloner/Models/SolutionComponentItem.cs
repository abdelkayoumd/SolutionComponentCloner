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

        public DateTime? ModifiedOn { get; set; }
        public string ModifiedBy { get; set; }

        /// <summary>True for a managed component, false for unmanaged, null when Dataverse does not say.</summary>
        public bool? IsManaged { get; set; }

        /// <summary>
        /// How the component was added to its solution: 0 = with all subcomponents, 1 = without them, 2 = as a shell only.
        /// Null when Dataverse does not say.
        /// </summary>
        public int? RootComponentBehavior { get; set; }

        /// <summary>True for a table that is in its solution completely, with all of its subcomponents.</summary>
        public bool IsAddedCompletely => ComponentType == 1 && RootComponentBehavior == 0;

        /// <summary>True when the same component is already in the target solution.</summary>
        public bool InTarget { get; set; }

        /// <summary>For a table: how its subcomponents are treated on copy.</summary>
        public SubcomponentMode SubcomponentMode { get; set; }
    }
}
