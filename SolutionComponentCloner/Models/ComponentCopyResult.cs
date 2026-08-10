namespace SolutionComponentCloner.Models
{
    public enum CopyOutcome
    {
        Success,
        AlreadyPresent,
        Failed
    }

    public sealed class ComponentCopyResult
    {
        public SolutionComponentItem Component { get; set; }
        public CopyOutcome Outcome { get; set; }
        public string Message { get; set; }
    }
}
