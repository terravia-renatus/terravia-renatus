namespace Terravia.Domain;

/// <summary>
/// Standardized outcome of a domain command dispatched through <see cref="IGameCommandGateway"/>.
/// </summary>
public sealed record CommandResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static readonly CommandResult Success = new(true);
    public static CommandResult Fail(string errorCode, string? message = null) => new(false, errorCode, message ?? errorCode);
}
