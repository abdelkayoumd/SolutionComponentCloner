using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    [DataContract]
    internal sealed class SelectionEntry
    {
        [DataMember(Order = 1)] public int ComponentType { get; set; }
        [DataMember(Order = 2)] public string ObjectId { get; set; }
        [DataMember(Order = 3)] public string Name { get; set; }
        [DataMember(Order = 4)] public string Group { get; set; }
    }

    [DataContract]
    internal sealed class SelectionDocument
    {
        [DataMember(Order = 1)] public int Version { get; set; } = 1;
        [DataMember(Order = 2)] public string SourceSolution { get; set; }
        [DataMember(Order = 3)] public List<SelectionEntry> Components { get; set; } = new List<SelectionEntry>();
    }

    /// <summary>Saves the ticked components to a JSON file and applies a saved selection to another solution's components.</summary>
    internal static class SelectionFile
    {
        public static SelectionDocument FromSelection(IEnumerable<SolutionComponentItem> selected, string sourceSolution)
        {
            return new SelectionDocument
            {
                SourceSolution = sourceSolution,
                Components = selected
                    .Select(i => new SelectionEntry
                    {
                        ComponentType = i.ComponentType,
                        ObjectId = i.ObjectId.ToString(),
                        Name = i.DisplayName,
                        Group = i.GroupName
                    })
                    .ToList()
            };
        }

        public static void Save(string path, SelectionDocument document)
        {
            var serializer = new DataContractJsonSerializer(typeof(SelectionDocument));
            using (var stream = File.Create(path))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true, "  "))
            {
                serializer.WriteObject(writer, document);
            }
        }

        public static SelectionDocument Load(string path)
        {
            var serializer = new DataContractJsonSerializer(typeof(SelectionDocument));
            using (var stream = File.OpenRead(path))
            {
                var document = (SelectionDocument)serializer.ReadObject(stream);
                if (document?.Components == null)
                {
                    throw new InvalidDataException("This file does not contain a saved selection.");
                }

                return document;
            }
        }

        /// <summary>
        /// Ticks every item that matches an entry, first by component type and id, then by component type and name.
        /// Returns how many entries matched and how many did not.
        /// </summary>
        public static (int Matched, int Missing) Apply(IReadOnlyCollection<SolutionComponentItem> items, SelectionDocument document)
        {
            var byId = new Dictionary<(int, Guid), SolutionComponentItem>();
            var byName = new Dictionary<(int, string), SolutionComponentItem>();
            foreach (var item in items)
            {
                byId[(item.ComponentType, item.ObjectId)] = item;
                if (!string.IsNullOrEmpty(item.DisplayName))
                {
                    byName[(item.ComponentType, item.DisplayName.ToLowerInvariant())] = item;
                }
            }

            var matched = 0;
            foreach (var entry in document.Components)
            {
                SolutionComponentItem target = null;
                if (Guid.TryParse(entry.ObjectId, out var id))
                {
                    byId.TryGetValue((entry.ComponentType, id), out target);
                }

                if (target == null && !string.IsNullOrEmpty(entry.Name))
                {
                    byName.TryGetValue((entry.ComponentType, entry.Name.ToLowerInvariant()), out target);
                }

                if (target != null)
                {
                    target.Selected = true;
                    matched++;
                }
            }

            return (matched, document.Components.Count - matched);
        }
    }
}
