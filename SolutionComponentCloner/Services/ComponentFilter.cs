using System;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    internal static class ComponentFilter
    {
        /// <summary>
        /// True when every word of <paramref name="text"/> appears in at least one of the component's
        /// name, type, folder, table or modified-by fields (case-insensitive).
        /// </summary>
        public static bool Matches(SolutionComponentItem item, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            foreach (var term in text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Contains(item.DisplayName, term) && !Contains(item.ComponentTypeName, term) &&
                    !Contains(item.GroupName, term) && !Contains(item.TableLabel, term) &&
                    !Contains(item.TableName, term) && !Contains(item.Category, term) &&
                    !Contains(item.SubGroup, term) && !Contains(item.ModifiedBy, term))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
