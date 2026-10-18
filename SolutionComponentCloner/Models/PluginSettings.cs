namespace SolutionComponentCloner.Models
{
    /// <summary>The two checkbox preferences XrmToolBox keeps for this tool between sessions.</summary>
    public sealed class PluginSettings
    {
        public bool IncludeRequired { get; set; }
        public bool HideExisting { get; set; }
    }
}
