using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Api.Common;

/// <summary>
/// Describes in the OpenAPI document what the API already does on the wire, so a generated client does not have to
/// guess: the bearer token, the Retry-After header on every 429, and the machine-readable <c>code</c> on a problem.
/// Nothing here changes behaviour.
/// </summary>
public static class OpenApiContract
{
    public const string BearerSchemeName = "Bearer";

    /// <summary>The scheme the JWT bearer middleware actually enforces.</summary>
    public static OpenApiOptions AddBearerScheme(this OpenApiOptions options) =>
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The short-lived access token from POST /api/v1/auth/login, sent as 'Authorization: Bearer <token>'.",
            };
            return Task.CompletedTask;
        }).AddOperationTransformer((operation, context, _) =>
        {
            // Every endpoint requires a signed-in user (the fallback authorization policy) unless it says [AllowAnonymous].
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAllowAnonymous>().Any())
                return Task.CompletedTask;

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = [],
            });
            return Task.CompletedTask;
        });

    /// <summary>
    /// Declares a 429 with a Retry-After header (whole seconds) on every operation that has [EnableRateLimiting].
    /// Some of those operations declared no 429 at all.
    /// </summary>
    public static OpenApiOptions AddRetryAfterOnRateLimited(this OpenApiOptions options) =>
        options.AddOperationTransformer(async (operation, context, ct) =>
        {
            if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<EnableRateLimitingAttribute>().Any())
                return;

            operation.Responses ??= [];
            if (!operation.Responses.TryGetValue("429", out var existing) || existing is not OpenApiResponse response)
            {
                response = new OpenApiResponse { Description = "Too Many Requests" };
                operation.Responses["429"] = response;
            }

            response.Content ??= new Dictionary<string, OpenApiMediaType>();
            if (!response.Content.ContainsKey("application/problem+json"))
            {
                response.Content["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = await context.GetOrCreateSchemaAsync(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), null, ct),
                };
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["Retry-After"] = new OpenApiHeader
            {
                Description = "How long to wait before trying again, in whole seconds (never an HTTP date). "
                    + "It is the length of the limiter's window, not the time left in it.",
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            };
        });
}
