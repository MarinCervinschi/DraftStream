using DraftStream.Application;
using DraftStream.Domain;

namespace DraftStream.Application.Tasks;

public sealed class TasksWorkflowHandler : IWorkflowHandler
{
    public WorkflowType WorkflowType => WorkflowType.Tasks;
}
