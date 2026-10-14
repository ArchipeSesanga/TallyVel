using System.Text.Json;
using System.Text.Json.Nodes;
using Tests.TestSupport;

namespace Tests.Contract;

/// <summary>
/// Checks the generated OpenAPI document, and can write it out for the
/// TypeScript client. Writing only happens when OPENAPI_OUTPUT is set:
///   OPENAPI_OUTPUT=$PWD/clients/typescript/openapi.json dotnet test --filter "FullyQualifiedName~OpenApiDocument"
/// A normal `dotnet test` never writes a file.
/// </summary>
[Collection("Postgres collection")]
public class OpenApiDocumentTests(PostgresApiFactory factory)
{
    private async Task<string> GetDocumentAsync() =>
        await TestApi.Client(factory).GetStringAsync("/openapi/v1.json");

    [Fact]
    public async Task Numbers_are_described_as_numbers_only()
    {
        var json = await GetDocumentAsync();

        var output = Environment.GetEnvironmentVariable("OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
            await File.WriteAllTextAsync(output,
                JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        var offenders = new List<string>();
        Walk(JsonNode.Parse(json)!, "$", offenders);
        Assert.True(offenders.Count == 0, "Numeric schemas that also allow strings:\n" + string.Join("\n", offenders));
    }

    // A numeric schema must not be ["integer","string"] / ["number","string"],
    // and must not carry the numeric-string "pattern" STJ adds for that case.
    private static void Walk(JsonNode node, string path, List<string> offenders)
    {
        if (node is JsonObject obj)
        {
            if (obj["type"] is JsonArray types)
            {
                var names = types.Select(t => t?.GetValue<string>()).ToList();
                if (names.Contains("string") && (names.Contains("integer") || names.Contains("number")))
                    offenders.Add($"{path}: type {string.Join("|", names)}");
            }
            if (obj["pattern"] is not null && obj["format"]?.GetValue<string>() is "int32" or "int64" or "double" or "float" or "decimal")
                offenders.Add($"{path}: numeric pattern {obj["pattern"]}");

            foreach (var (key, child) in obj)
                if (child is not null) Walk(child, $"{path}.{key}", offenders);
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                if (array[i] is { } child) Walk(child, $"{path}[{i}]", offenders);
        }
    }
}
