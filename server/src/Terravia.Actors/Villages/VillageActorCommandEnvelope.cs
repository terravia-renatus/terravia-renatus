using Terravia.Domain;

namespace Terravia.Actors.Villages;

/// <summary>
/// Actor message envelope wrapping a domain command with arrival timestamp and optional Sitter permissions.
/// </summary>
public sealed record VillageActorCommandEnvelope<TCommand>(
    TCommand Command,
    long NowMs,
    SitterPermissions? SitterPermissions = null)
    where TCommand : notnull;
