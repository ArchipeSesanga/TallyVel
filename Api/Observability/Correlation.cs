using System.Diagnostics;

namespace Api.Observability;

public static class Correlation
{
    // Activity.Current is the request span ASP.NET Core starts. If the caller
    // sent a traceparent header, its TraceId IS the caller's trace id.
    public static string GetId(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}