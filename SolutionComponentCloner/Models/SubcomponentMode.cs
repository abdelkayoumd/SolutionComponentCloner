namespace SolutionComponentCloner.Models
{
    /// <summary>How a table's own subcomponents are treated when the table is copied.</summary>
    public enum SubcomponentMode
    {
        /// <summary>Follow the "Include required components" checkbox.</summary>
        UseGlobalSetting = 0,

        /// <summary>Add the table with all of its subcomponents.</summary>
        IncludeAll = 1,

        /// <summary>Add the table without its subcomponents; only the ticked ones are added.</summary>
        OnlySelected = 2
    }
}
