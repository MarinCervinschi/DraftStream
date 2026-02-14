using DraftStream.Domain;

namespace DraftStream.Application;

public interface IWorkflowHandler
{
    WorkflowType WorkflowType { get; }
}
