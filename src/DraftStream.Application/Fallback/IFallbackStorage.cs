namespace DraftStream.Application.Fallback;

public interface IFallbackStorage
{
    Task<bool> SaveToWorkflowDatabaseAsync(
        string databaseId,
        string title,
        string messageText,
        string senderName,
        string sourceType,
        string workflowName,
        CancellationToken cancellationToken);

    Task<bool> SaveToGeneralFallbackAsync(
        string messageText,
        string senderName,
        string sourceType,
        string sourceContext,
        CancellationToken cancellationToken);
}
