namespace DraftStream.Application;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WorkflowHandlerAttribute : Attribute
{
    public string Name { get; }

    public WorkflowHandlerAttribute(string name) => Name = name;
}
