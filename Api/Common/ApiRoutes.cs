namespace Api.Common;

/// <summary>
/// The one place the API version appears. Every controller route and the
/// refresh cookie path are built from it.
/// </summary>
public static class ApiRoutes
{
    public const string V1 = "api/v1";
}
