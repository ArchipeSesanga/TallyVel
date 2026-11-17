using System.Text.Json.Nodes;
using Tests.TestSupport;

namespace Tests.Contract;

/// <summary>
/// What a generated client needs from the OpenAPI document and cannot guess: the machine-readable error code, the
/// Retry-After header on every 429, and which operations need a bearer token. All three were true on the wire before
/// they were true in the document (found while writing the mobile client's contract notes).
/// </summary>
[Collection("Postgres collection")]
public class OpenApiClientContractTests(PostgresApiFactory factory)
{
    private async Task<JsonNode> DocumentAsync() =>
        JsonNode.Parse(await TestApi.Client(factory).GetStringAsync("/openapi/v1.json"))!;

    // Every operation with [EnableRateLimiting]: login, the four money endpoints and the two invite endpoints.
    public static TheoryData<string, string> RateLimited => new()
    {
        { "post", "/api/v1/auth/login" },
        { "post", "/api/v1/stokvels/{stokvelId}/contributions" },
        { "post", "/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof" },
        { "post", "/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout" },
        { "post", "/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/close" },
        { "get", "/api/v1/invites/{code}" },
        { "post", "/api/v1/invites/{code}/join" },
    };

    [Theory]
    [MemberData(nameof(RateLimited))]
    public async Task A_rate_limited_operation_declares_a_429_with_an_integer_Retry_After_header(string method, string path)
    {
        var doc = await DocumentAsync();

        var response = doc["paths"]![path]![method]!["responses"]!["429"];
        Assert.True(response is not null, $"{method.ToUpperInvariant()} {path} declares no 429");

        var header = response!["headers"]?["Retry-After"];
        Assert.True(header is not null, $"{method.ToUpperInvariant()} {path}: the 429 declares no Retry-After header");
        Assert.Equal("integer", header!["schema"]!["type"]!.GetValue<string>());
        Assert.Contains("seconds", header["description"]!.GetValue<string>());
    }

    [Fact]
    public async Task Only_rate_limited_operations_declare_a_429()
    {
        var doc = await DocumentAsync();

        var declared = doc["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(op => (Path: p.Key, Method: op.Key, Op: op.Value!)))
            .Where(x => x.Op["responses"]?["429"] is not null)
            .Select(x => $"{x.Method} {x.Path}")
            .Order()
            .ToList();

        var expected = RateLimited.Select(row => $"{row[0]} {row[1]}").Order().ToList();
        Assert.Equal(expected, declared);
    }

    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("ValidationProblemDetails")]
    public async Task An_error_body_declares_the_machine_readable_code_and_says_it_is_optional(string schemaName)
    {
        var doc = await DocumentAsync();

        var schema = doc["components"]!["schemas"]![schemaName]!;
        var code = schema["properties"]!["code"];

        Assert.True(code is not null, $"{schemaName} has no code");
        var types = code!["type"]!.AsArray().Select(t => t!.GetValue<string>()).Order().ToList();
        Assert.Equal(["null", "string"], types);
        // Framework-made problems (401, 403, 404, 405, 415, validation 400) have no code, so it must never be required.
        Assert.DoesNotContain(schema["required"]?.AsArray().Select(r => r!.GetValue<string>()) ?? [], r => r == "code");
    }

    [Fact]
    public async Task A_bearer_security_scheme_is_declared()
    {
        var doc = await DocumentAsync();

        var scheme = doc["components"]!["securitySchemes"]!["Bearer"];

        Assert.True(scheme is not null, "components.securitySchemes.Bearer is missing");
        Assert.Equal("http", scheme!["type"]!.GetValue<string>());
        Assert.Equal("bearer", scheme["scheme"]!.GetValue<string>());
        Assert.Equal("JWT", scheme["bearerFormat"]!.GetValue<string>());
    }

    [Fact]
    public async Task Every_operation_needs_the_bearer_token_except_the_four_auth_operations()
    {
        var doc = await DocumentAsync();

        string[] anonymous =
        [
            "post /api/v1/auth/register", "post /api/v1/auth/login", "post /api/v1/auth/refresh", "post /api/v1/auth/logout",
        ];

        var wrong = new List<string>();
        foreach (var (path, item) in doc["paths"]!.AsObject())
        foreach (var (method, op) in item!.AsObject())
        {
            var key = $"{method} {path}";
            var requiresBearer = op!["security"]?.AsArray().Any(r => r!.AsObject().ContainsKey("Bearer")) == true;
            var isAnonymous = anonymous.Contains(key);

            if (isAnonymous && op["security"]?.AsArray().Count > 0) wrong.Add($"{key} is anonymous but declares security");
            if (!isAnonymous && !requiresBearer) wrong.Add($"{key} requires a token but declares no Bearer security");
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>
    /// clients/typescript/openapi.json is what other repos copy (the mobile app's contract/ folder, the TypeScript types).
    /// It is exported by hand, so it can silently fall behind the controllers: it already did once, by one description.
    /// This fails the build when it does.
    /// </summary>
    [Fact]
    public async Task The_committed_openapi_snapshot_is_what_the_api_serves()
    {
        var snapshotPath = Path.Combine(RepositoryRoot(), "clients", "typescript", "openapi.json");

        var served = Normalise(await DocumentAsync());
        var committed = Normalise(JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))!);

        Assert.True(JsonNode.DeepEquals(served, committed),
            "clients/typescript/openapi.json is out of date. Regenerate it from the repo root and commit the result:\n" +
            "  OPENAPI_OUTPUT=$PWD/clients/typescript/openapi.json dotnet test --filter \"FullyQualifiedName~OpenApiDocument\"\n" +
            "  (cd clients/typescript && npm run contract)");
    }

    // Same line-ending rule as the exporter above: a checkout on Windows must not look like a change.
    private static JsonNode Normalise(JsonNode node) =>
        JsonNode.Parse(node.ToJsonString().Replace("\\r\\n", "\\n"))!;

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TallyVel.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("TallyVel.slnx not found above " + AppContext.BaseDirectory);
    }
}
