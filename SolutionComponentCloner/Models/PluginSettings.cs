using System;
using System.Collections.Generic;

namespace SolutionComponentCloner.Models
{
    /// <summary>Preferences XrmToolBox stores for this tool between sessions.</summary>
    public sealed class PluginSettings
    {
        public bool IncludeRequired { get; set; }
        public bool HideExisting { get; set; }
        public List<ConnectionPreference> Connections { get; set; } = new List<ConnectionPreference>();

        public ConnectionPreference ForConnection(Guid connectionId)
        {
            return Connections.Find(c => c.ConnectionId == connectionId);
        }

        public ConnectionPreference GetOrAdd(Guid connectionId)
        {
            var preference = ForConnection(connectionId);
            if (preference == null)
            {
                preference = new ConnectionPreference { ConnectionId = connectionId };
                Connections.Add(preference);
            }

            return preference;
        }
    }

    public sealed class ConnectionPreference
    {
        public Guid ConnectionId { get; set; }
        public string SourceUniqueName { get; set; }
        public string TargetUniqueName { get; set; }
    }
}
