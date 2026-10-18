using System;

namespace SolutionComponentCloner.Models
{
    public sealed class PublisherListItem
    {
        public Guid PublisherId { get; set; }
        public string FriendlyName { get; set; }
        public string UniqueName { get; set; }
        public string Prefix { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Prefix) ? FriendlyName : $"{FriendlyName} ({Prefix})";
        }
    }
}
