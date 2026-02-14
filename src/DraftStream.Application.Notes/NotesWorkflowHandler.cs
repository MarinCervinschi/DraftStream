using DraftStream.Application;
using DraftStream.Domain;

namespace DraftStream.Application.Notes;

public sealed class NotesWorkflowHandler : IWorkflowHandler
{
    public WorkflowType WorkflowType => WorkflowType.Notes;
}
