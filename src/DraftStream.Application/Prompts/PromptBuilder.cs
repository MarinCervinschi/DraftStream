using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DraftStream.Application.Mcp;

namespace DraftStream.Application.Prompts;

public sealed class PromptBuilder
{
    private const string _baseTemplate = """
                                         You are processing a message for the "{0}" workflow.
                                         Source: {1}

                                         DATA_SOURCE_ID: {2} — use this EXACT value in parent.data_source_id. Do NOT use any other ID.
                                         TODAY: {3}

                                         ## Database Schema

                                         Properties:
                                         {4}

                                         ## Property JSON Formats

                                         title → {{ "title": [{{ "text": {{ "content": "..." }} }}] }}
                                         rich_text → {{ "rich_text": [{{ "text": {{ "content": "..." }} }}] }}
                                         select → {{ "select": {{ "name": "..." }} }}
                                         multi_select → {{ "multi_select": [{{ "name": "..." }}] }}
                                         date → {{ "date": {{ "start": "YYYY-MM-DD" }} }}
                                         checkbox → {{ "checkbox": true }}
                                         status → {{ "status": {{ "name": "..." }} }}
                                         number → {{ "number": 42 }}
                                         url → {{ "url": "https://..." }}
                                         email → {{ "email": "..." }}
                                         phone_number → {{ "phone_number": "..." }}

                                         ## Example

                                         User message: "Buy groceries for the weekend"

                                         Call API-post-page with:
                                         {5}

                                         Reply: "Saved to {0}."

                                         ## Workflow Instructions

                                         {6}

                                         ## Rules

                                         - Call API-post-page ONCE with parent.data_source_id = "{2}"
                                         - Use EXACT property names from the schema (case-sensitive)
                                         - Reply with 1-2 sentence confirmation
                                         - Skip system-managed properties (created_time, last_edited_time, created_by, last_edited_by, formula, rollup)
                                         - Leave unknown values empty rather than guessing
                                         - Max 2000 characters per text field, max 100 items in arrays
                                         """;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, string> _instructionCache = new();

    public string BuildSystemPrompt(
        string workflowName,
        string sourceType,
        DatabaseSchema schema
    )
    {
        string instructions = LoadWorkflowInstructions(workflowName);
        string schemaSection = FormatSchemaForPrompt(schema);
        string example = GenerateFewShotExample(schema);
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return string.Format(
            _baseTemplate,
            workflowName,
            sourceType,
            schema.DataSourceId,
            today,
            schemaSection,
            example,
            instructions);
    }

    private static string FormatSchemaForPrompt(DatabaseSchema schema)
    {
        var sb = new StringBuilder();

        foreach (SchemaProperty prop in schema.Properties)
        {
            if (prop.IsSystemManaged)
            {
                sb.AppendLine($"- \"{prop.Name}\" ({prop.Type}) — SKIP (system-managed)");
            }
            else if (prop.AllowedValues is { Count: > 0 })
            {
                string values = string.Join(", ", prop.AllowedValues);
                sb.AppendLine($"- \"{prop.Name}\" ({prop.Type}) — allowed: {values}");
            }
            else
            {
                sb.AppendLine($"- \"{prop.Name}\" ({prop.Type})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string GenerateFewShotExample(DatabaseSchema schema)
    {
        var properties = new Dictionary<string, object>();

        int count = 0;
        foreach (SchemaProperty prop in schema.Properties)
        {
            if (prop.IsSystemManaged || count >= 6)
            {
                continue;
            }

            object? value = GenerateExampleValue(prop);
            if (value is null)
            {
                continue;
            }

            properties[prop.Name] = value;
            count++;
        }

        var page = new Dictionary<string, object>
        {
            ["parent"] = new Dictionary<string, string>
            {
                ["data_source_id"] = schema.DataSourceId
            },
            ["properties"] = properties,
            ["children"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["object"] = "block",
                    ["type"] = "paragraph",
                    ["paragraph"] = new Dictionary<string, object>
                    {
                        ["rich_text"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["text"] = new Dictionary<string, string>
                                {
                                    ["content"] = "Content from the user's message goes here."
                                }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(page, _jsonOptions);
    }

    private static object? GenerateExampleValue(SchemaProperty prop)
    {
        return prop.Type switch
        {
            "title" => new Dictionary<string, object>
            {
                ["title"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["text"] = new Dictionary<string, string> { ["content"] = "Example title" }
                    }
                }
            },
            "rich_text" => new Dictionary<string, object>
            {
                ["rich_text"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["text"] = new Dictionary<string, string> { ["content"] = "Example text" }
                    }
                }
            },
            "select" => new Dictionary<string, object>
            {
                ["select"] = new Dictionary<string, string>
                {
                    ["name"] = prop.AllowedValues is { Count: > 0 } ? prop.AllowedValues[0] : "Option"
                }
            },
            "multi_select" => new Dictionary<string, object>
            {
                ["multi_select"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["name"] = prop.AllowedValues is { Count: > 0 } ? prop.AllowedValues[0] : "Tag"
                    }
                }
            },
            "status" => new Dictionary<string, object>
            {
                ["status"] = new Dictionary<string, string>
                {
                    ["name"] = prop.AllowedValues is { Count: > 0 } ? prop.AllowedValues[0] : "Not started"
                }
            },
            "date" => new Dictionary<string, object>
            {
                ["date"] = new Dictionary<string, string> { ["start"] = "2025-01-15" }
            },
            "checkbox" => new Dictionary<string, bool>
            {
                ["checkbox"] = false
            },
            "number" => new Dictionary<string, int>
            {
                ["number"] = 0
            },
            "url" => new Dictionary<string, string>
            {
                ["url"] = "https://example.com"
            },
            "email" => new Dictionary<string, string>
            {
                ["email"] = "example@email.com"
            },
            "phone_number" => new Dictionary<string, string>
            {
                ["phone_number"] = "+1234567890"
            },
            _ => null
        };
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
