using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class CompletionActionPolicy
{
    public static CompletionAction ResolveForCompletedRun(IReadOnlyList<CopyJob> jobs)
    {
        if (jobs.Count == 0 || jobs.Any(job => job.Status is not (
                CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings)))
            return CompletionAction.None;
        return jobs[^1].CompletionAction;
    }
}
