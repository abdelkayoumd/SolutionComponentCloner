using System;
using SolutionComponentCloner.Models;

namespace SolutionComponentCloner.Services
{
    /// <summary>Which components to list, by where they are: the source solution, the target solution, or both.</summary>
    internal enum MembershipFilter
    {
        All,
        NotInTarget,
        InBoth,
        OnlyInTarget
    }

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

        /// <summary>
        /// True when the component passes the filter chips: where it is, how recently it changed and who changed it.
        /// A component with no modified date or modified-by never passes a filter on that field.
        /// </summary>
        public static bool MatchesView(SolutionComponentItem item, MembershipFilter membership, DateTime? modifiedSince, string modifiedBy)
        {
            if (membership == MembershipFilter.NotInTarget && item.InTarget)
            {
                return false;
            }

            if (membership == MembershipFilter.InBoth && !item.InTarget)
            {
                return false;
            }

            if (modifiedSince.HasValue && !(item.ModifiedOn >= modifiedSince.Value))
            {
                return false;
            }

            return string.IsNullOrEmpty(modifiedBy) || string.Equals(item.ModifiedBy, modifiedBy, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
