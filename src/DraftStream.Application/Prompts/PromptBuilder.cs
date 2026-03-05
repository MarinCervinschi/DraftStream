using System.Reflection;

namespace DraftStream.Application.Prompts;

public sealed class PromptBuilder
{
    private const string _baseTemplate = """
                                         You are processing a message for the "{0}" workflow.
                                         Database ID: {1} | Source: {2}

                                         ## Steps

                                         1. Discover the schema:
                                            - Call `API-retrieve-a-database` with database ID → extract the first `data_sources` ID
                                            - Call `API-retrieve-a-data-source` with that ID → read property names, types, and allowed values
                                         2. Call `API-post-page` to create the page. The JSON argument must contain:
                                            - `parent`: `{{ "database_id": "<id>" }}`
                                            - `properties`: match each property name (case-sensitive) and type from the schema exactly
                                            - `children`: array of block objects for body content (paragraph or code blocks)
                                         3. Reply with a short confirmation (1-2 sentences max).

                                         ## Property type reference

                                         title → `{{ "title": [{{ "text": {{ "content": "..." }} }}] }}`
                                         rich_text → `{{ "rich_text": [{{ "text": {{ "content": "..." }} }}] }}`
                                         select → `{{ "select": {{ "name": "..." }} }}`
                                         multi_select → `{{ "multi_select": [{{ "name": "..." }}] }}`
                                         date → `{{ "date": {{ "start": "YYYY-MM-DD" }} }}`
                                         checkbox → `{{ "checkbox": true }}`
                                         status → `{{ "status": {{ "name": "..." }} }}` (use an allowed value from the schema)

                                         ## Rules

                                         - Page title: short summary of the user's message
                                         - Main content goes in `children` blocks, NOT in properties
                                         - Skip system-managed properties (Created Time, Last Edited Time, etc.)
                                         - Leave unknown property values empty rather than guessing

                                         ## Workflow Instructions

                                         {3}
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
