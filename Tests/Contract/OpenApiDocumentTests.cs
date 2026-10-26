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
        {
            // Windows adds CRLF twice: to the indentation (the platform newline) and to XML doc comments
            // (the checkout's line endings, which appear in the JSON text as the characters \r\n).
            // Write plain \n for both, so the file is the same on every machine.
            var text = JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(output, text.Replace("\r\n", "\n").Replace("\\r\\n", "\\n") + "\n");
        }

        var offenders = new List<string>();
        Walk(JsonNode.Parse(json)!, "$", offenders);
        Assert.True(offenders.Count == 0, "Numeric schemas that also allow strings:\n" + string.Join("\n", offenders));
    }

    // ---- Handoff gaps (Assignment 7.1): what the generated TypeScript types are built from ----

    private async Task<JsonNode> DocumentAsync() => JsonNode.Parse(await GetDocumentAsync())!;

    private static JsonObject Schema(JsonNode doc, string name) =>
        doc["components"]!["schemas"]![name]!.AsObject();

    private static List<string> TypesOf(JsonNode? schema) => schema?["type"] switch
    {
        JsonArray array => array.Select(t => t!.GetValue<string>()).ToList(),
        JsonValue value => [value.GetValue<string>()],
        _ => [],
    };

    private static bool IsMoneyName(string name) =>
        name.EndsWith("Cents", StringComparison.Ordinal) || name.Contains("Amount", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task Recording_a_contribution_declares_a_required_uuid_Idempotency_Key_header()
    {
        var doc = await DocumentAsync();

        var parameters = doc["paths"]!["/api/v1/stokvels/{stokvelId}/contributions"]!["post"]!["parameters"]!.AsArray();
        var header = Assert.Single(parameters, p => p!["name"]!.GetValue<string>() == "Idempotency-Key")!;

        Assert.Equal("header", header["in"]!.GetValue<string>());
        Assert.True(header["required"]!.GetValue<bool>());
        Assert.Equal("string", header["schema"]!["type"]!.GetValue<string>());
        Assert.Equal("uuid", header["schema"]!["format"]!.GetValue<string>());
    }

    [Fact]
    public async Task Only_the_record_contribution_operation_declares_the_Idempotency_Key_header()
    {
        var doc = await DocumentAsync();

        var withHeader = doc["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(op => (Path: path.Key, Method: op.Key, Op: op.Value!)))
            .Where(x => x.Op["parameters"]?.AsArray().Any(p => p!["name"]!.GetValue<string>() == "Idempotency-Key") == true)
            .Select(x => $"{x.Method.ToUpperInvariant()} {x.Path}")
            .ToList();

        Assert.Equal(["POST /api/v1/stokvels/{stokvelId}/contributions"], withHeader);
    }

    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("ValidationProblemDetails")]
    public async Task An_error_body_declares_the_correlationId_the_api_really_sends(string schemaName)
    {
        var doc = await DocumentAsync();

        var properties = Schema(doc, schemaName)["properties"]!.AsObject();

        Assert.True(properties.ContainsKey("correlationId"), $"{schemaName} has no correlationId");
        Assert.Contains("string", TypesOf(properties["correlationId"]));
    }

    [Fact]
    public async Task No_money_field_anywhere_is_a_number()
    {
        var doc = await DocumentAsync();
        var moneyFields = new List<string>();
        var offenders = new List<string>();

        // Every property of every schema, and every query/path parameter.
        foreach (var (schemaName, schema) in doc["components"]!["schemas"]!.AsObject())
            foreach (var (propertyName, property) in schema!["properties"]?.AsObject() ?? [])
                if (IsMoneyName(propertyName))
                {
                    moneyFields.Add($"{schemaName}.{propertyName}");
                    if (!TypesOf(property).SequenceEqual(["integer"]) || property!["format"]?.GetValue<string>() != "int64")
                        offenders.Add($"{schemaName}.{propertyName}: {property!.ToJsonString()}");
                }
        foreach (var (path, item) in doc["paths"]!.AsObject())
            foreach (var (_, operation) in item!.AsObject())
                foreach (var parameter in operation?["parameters"]?.AsArray() ?? [])
                {
                    var name = parameter!["name"]!.GetValue<string>();
                    if (!IsMoneyName(name)) continue;
                    moneyFields.Add($"{path}?{name}");
                    if (!TypesOf(parameter["schema"]).Contains("integer") || TypesOf(parameter["schema"]).Contains("number"))
                        offenders.Add($"{path}?{name}: {parameter["schema"]!.ToJsonString()}");
                }

        Assert.Empty(offenders);
        // The check must have found the money fields it is about (a rename must not silently empty it).
        string[] expected =
        [
            "ContributionResponse.amountCents", "PayoutResponse.amountCents", "RecordContributionRequest.amountCents",
            "StokvelResponse.contributionAmountCents", "CreateStokvelRequest.contributionAmountCents",
            "HomeStokvelCard.paidThisCycleCents", "HomeStokvelCard.owedThisCycleCents",
        ];
        foreach (var field in expected)
            Assert.Contains(field, moneyFields);
        Assert.Contains(moneyFields, f => f.EndsWith("?minAmountCents"));
        Assert.Contains(moneyFields, f => f.EndsWith("?maxAmountCents"));
        // ...and the old, floating-point names are gone.
        Assert.DoesNotContain(moneyFields, f => f.EndsWith(".amount") || f.EndsWith(".contributionAmount")
            || f.EndsWith("?minAmount") || f.EndsWith("?maxAmount"));
    }

    [Fact]
    public async Task Nothing_in_the_document_is_a_floating_point_number()
    {
        var offenders = new List<string>();
        void Find(JsonNode? node, string path)
        {
            if (node is JsonObject obj)
            {
                if (TypesOf(obj).Contains("number")) offenders.Add(path);
                foreach (var (key, child) in obj) Find(child, $"{path}.{key}");
            }
            else if (node is JsonArray array)
                for (var i = 0; i < array.Count; i++) Find(array[i], $"{path}[{i}]");
        }

        Find(await DocumentAsync(), "$");

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task Statuses_and_roles_list_exactly_their_values()
    {
        var doc = await DocumentAsync();

        string[] Values(string schema) => Schema(doc, schema)["enum"]!.AsArray().Select(v => v!.GetValue<string>()).ToArray();

        Assert.Equal(["Pending", "Paid", "Confirmed"], Values("ContributionStatus"));
        Assert.Equal(["Open", "Closed", "PaidOut"], Values("CycleStatus"));
        Assert.Equal(["Member", "Treasurer", "Chairperson"], Values("MemberRole"));
        Assert.Equal("#/components/schemas/ContributionStatus",
            Schema(doc, "ContributionResponse")["properties"]!["status"]!["$ref"]!.GetValue<string>());
    }

    [Fact]
    public async Task Timestamps_are_date_time_strings_and_calendar_dates_are_dates()
    {
        var doc = await DocumentAsync();
        var untyped = new List<string>();
        foreach (var (schemaName, schema) in doc["components"]!["schemas"]!.AsObject())
            foreach (var (propertyName, property) in schema!["properties"]?.AsObject() ?? [])
                if (property!["format"]?.GetValue<string>() == "date-time" && !TypesOf(property).Contains("string"))
                    untyped.Add($"{schemaName}.{propertyName}");

        Assert.Empty(untyped);
        Assert.Equal("date-time", Schema(doc, "ContributionResponse")["properties"]!["recordedAt"]!["format"]!.GetValue<string>());
        Assert.Contains("string", TypesOf(Schema(doc, "ContributionResponse")["properties"]!["recordedAt"]));

        var cycle = Schema(doc, "HomeCurrentCycle")["properties"]!;
        Assert.Equal("date", cycle["startsOn"]!["format"]!.GetValue<string>());
        Assert.Equal("date", cycle["endsOn"]!["format"]!.GetValue<string>());
        var card = Schema(doc, "HomeStokvelCard")["properties"]!;
        Assert.Equal("date", card["nextPayoutDate"]!["format"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_current_cycle_is_an_object_that_may_be_null()
    {
        var doc = await DocumentAsync();

        var currentCycle = Schema(doc, "HomeStokvelCard")["properties"]!["currentCycle"]!;

        var options = currentCycle["oneOf"]!.AsArray();
        Assert.Contains(options, o => o!["type"]?.GetValue<string>() == "null");
        Assert.Contains(options, o => o!["$ref"]?.GetValue<string>() == "#/components/schemas/HomeCurrentCycle");
        Assert.Equal(
            ["id", "label", "status", "startsOn", "endsOn"],
            Schema(doc, "HomeCurrentCycle")["properties"]!.AsObject().Select(p => p.Key));
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
