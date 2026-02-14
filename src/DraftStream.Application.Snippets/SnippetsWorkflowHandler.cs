using DraftStream.Application;
using DraftStream.Domain;

namespace DraftStream.Application.Snippets;

public sealed class SnippetsWorkflowHandler : IWorkflowHandler
{
    public WorkflowType WorkflowType => WorkflowType.Snippets;
}
