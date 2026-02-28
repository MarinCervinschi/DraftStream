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
        - For the page title, use a short summary of the user's message, very concise.
        - The main content should be stored as body content in the new page, NOT as a property value
        - Follow the istructions carefully about the content formatting and which properties to fill
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
        builder.AppendLine("IMPORTANT: Property names are case-sensitive. Use them exactly as shown (e.g. \"Status\" not \"status\").");
        builder.AppendLine();

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

            builder.Append($"- \"{propertyName}\" (type: {type})");

            AppendTypeHint(builder, propertyValue, type);

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendTypeHint(StringBuilder builder, JsonElement propertyValue, string type)
    {
        string? optionsProperty = type switch
        {
            "select" => "select",
            "multi_select" => "multi_select",
            "status" => "status",
            _ => null
        };

        if (optionsProperty is null)
            return;

        if (!propertyValue.TryGetProperty(optionsProperty, out JsonElement typeElement))
            return;

        if (!typeElement.TryGetProperty("options", out JsonElement optionsElement))
            return;

        var optionNames = new List<string>();
        foreach (JsonElement option in optionsElement.EnumerateArray())
        {
            if (option.TryGetProperty("name", out JsonElement nameElement))
            {
                string? name = nameElement.GetString();
                if (!string.IsNullOrEmpty(name))
                    optionNames.Add($"\"{name}\"");
            }
        }

        if (optionNames.Count == 0)
            return;

        builder.Append($" — allowed values: {string.Join(", ", optionNames)}");
        builder.Append(type switch
        {
            "status" => " — format: {\"status\": {\"name\": \"<value>\"}}",
            "select" => " — format: {\"select\": {\"name\": \"<value>\"}}",
            "multi_select" => " — format: {\"multi_select\": [{\"name\": \"<value>\"}]}",
            _ => ""
        });
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
