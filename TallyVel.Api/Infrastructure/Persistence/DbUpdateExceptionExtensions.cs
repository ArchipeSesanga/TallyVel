using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TallyVel.Api.Infrastructure.Persistence;

internal static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True if Postgres rejected the write because it would break a
    /// unique constraint or index — optionally a specific one, by name
    /// (the names come from the migration, e.g. "IX_Users_Email").
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException ex, string? constraintName = null) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } pg
        && (constraintName is null || pg.ConstraintName == constraintName);
}
