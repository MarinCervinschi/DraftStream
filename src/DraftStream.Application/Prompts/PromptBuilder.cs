using System.Reflection;
using System.Text;
using System.Text.Json;

namespace DraftStream.Application.Prompts;

public sealed class PromptBuilder
{
    private const string _baseTemplate = """
        You are processing a message for the "{0}" workflow.

        ## Notion Database Schema

        Database ID: {1}
        Source: {2}

        The target database has the following properties:
        {3}

        ## Workflow Instructions

        {4}

        ## Rules

        - Use the provided tools to create a new page in the database above
        - The main content of the user's message should be stored as body content in the new page, not as a property value
        - Fill properties based on the user's message content
        - If you cannot determine a value for a property, leave it empty rather than guessing
        - Some properties are set by the system and should not be filled in by you.
            These include "Created Time", "Last Edited Time", and any properties with "created" or "edited" in their name.
        - After creating the page, respond with a brief, human-friendly confirmation of what was stored, not to longer than a couple of sentences.
        """;

    private readonly Dictionary<string, string> _instructionCache = new();

    public string BuildSystemPrompt(
        string workflowName,
        string databaseId,
        string sourceType,
        string schemaDescription)
    {
        string instructions = LoadWorkflowInstructions(workflowName);

        return string.Format(_baseTemplate, workflowName, databaseId, sourceType, schemaDescription, instructions);
    }

    public static string FormatSchemaDescription(string rawSchemaJson)
    {
        var builder = new StringBuilder();

        using var doc = JsonDocument.Parse(rawSchemaJson);

        if (!doc.RootElement.TryGetProperty("properties", out JsonElement properties))
            return "No properties found in database schema.";

        foreach (JsonProperty property in properties.EnumerateObject())
        {
            string propertyName = property.Name;
            JsonElement propertyValue = property.Value;

            string type = propertyValue.TryGetProperty("type", out JsonElement typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            builder.Append($"- {propertyName} ({type})");

            AppendSelectOptions(builder, propertyValue, type);

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendSelectOptions(StringBuilder builder, JsonElement propertyValue, string type)
    {
        string optionsProperty = type switch
        {
            "select" => "select",
            "multi_select" => "multi_select",
            "status" => "status",
            _ => ""
        };

        if (string.IsNullOrEmpty(optionsProperty))
            return;

        if (!propertyValue.TryGetProperty(optionsProperty, out JsonElement selectElement))
            return;

        if (!selectElement.TryGetProperty("options", out JsonElement optionsElement))
            return;

        var options = new List<string>();
        foreach (JsonElement option in optionsElement.EnumerateArray())
        {
            if (option.TryGetProperty("name", out JsonElement nameElement))
            {
                string? name = nameElement.GetString();
                if (!string.IsNullOrEmpty(name))
                    options.Add(name);
            }
        }

        if (options.Count > 0)
            builder.Append($" — options: {string.Join(", ", options)}");
    }

    private string LoadWorkflowInstructions(string workflowName)
    {
        if (_instructionCache.TryGetValue(workflowName, out string? cached))
            return cached;

        string resourceName = $"DraftStream.Application.Prompts.{workflowName}.md";
        Assembly assembly = typeof(PromptBuilder).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            string fallback = $"Process the user's message and store it in the database.";
            _instructionCache[workflowName] = fallback;
            return fallback;
        }

        using var reader = new StreamReader(stream);
        string instructions = reader.ReadToEnd();
        _instructionCache[workflowName] = instructions;
        return instructions;
    }
}
