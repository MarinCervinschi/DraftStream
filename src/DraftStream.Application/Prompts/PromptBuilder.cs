using System.Reflection;

namespace DraftStream.Application.Prompts;

public sealed class PromptBuilder
{
    private const string _baseTemplate = """
                                         You are processing a message for the "{0}" workflow.

                                         ## Notion Database

                                         Database ID: {1}
                                         Source: {2}

                                         ## Schema Discovery

                                         IMPORTANT: Before creating any page, you MUST first discover the database schema:
                                         1. Call `API-retrieve-a-database` with the database ID above
                                         2. From the response, extract the first data source ID from the `data_sources` array
                                         3. Call `API-retrieve-a-data-source` with that data source ID to get the full schema
                                         4. Use the schema response to correctly structure properties when creating the page

                                         Pay close attention to property types and formats. Use exact property names (case-sensitive) and value formats.

                                         ## Workflow Instructions

                                         {3}

                                         ## Rules

                                         - Use the provided tools to create a new page in the database above
                                         - For the page title, use a short summary of the user's message, very concise.
                                         - The main content should be stored as body content in the new page, NOT as a property value
                                         - Follow the instructions carefully about the content formatting and which properties to fill
                                         - If you cannot determine a value for a property, leave it empty rather than guessing
                                         - Some properties are set by the system and should not be filled in by you.
                                             These include "Created Time", "Last Edited Time", and any properties with "created" or "edited" in their name.
                                         - After creating the page, respond with a brief, human-friendly confirmation of what was stored, not to longer than a couple of sentences.
                                         """;

    private readonly Dictionary<string, string> _instructionCache = new();

    public string BuildSystemPrompt(
        string workflowName,
        string databaseId,
        string sourceType)
    {
        string instructions = LoadWorkflowInstructions(workflowName);

        return string.Format(_baseTemplate, workflowName, databaseId, sourceType, instructions);
    }

    private string LoadWorkflowInstructions(string workflowName)
    {
        if (_instructionCache.TryGetValue(workflowName, out string? cached))
        {
            return cached;
        }

        string resourceName = $"DraftStream.Application.Prompts.{workflowName}.md";
        Assembly assembly = typeof(PromptBuilder).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            const string fallback = $"Process the user's message and store it in the database.";
            _instructionCache[workflowName] = fallback;
            return fallback;
        }

        using var reader = new StreamReader(stream);
        string instructions = reader.ReadToEnd();
        _instructionCache[workflowName] = instructions;
        return instructions;
    }
}
