using System;

namespace SolutionComponentCloner.Models
{
    public sealed class SolutionListItem
    {
        public Guid SolutionId { get; set; }
        public string UniqueName { get; set; }
        public string FriendlyName { get; set; }
        public bool IsManaged { get; set; }
        public string Version { get; set; }

        public override string ToString()
        {
            var managedTag = IsManaged ? " (Managed)" : string.Empty;
            return $"{FriendlyName}{managedTag}  —  {UniqueName}  •  v{Version}";
        }
    }
}
