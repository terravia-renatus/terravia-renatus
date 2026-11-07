using Terravia.Domain.Villages;

namespace Terravia.Actors.Villages;

/// <summary>
/// Command instructing a <see cref="VillageActor"/> to evaluate T4 Hero Bandage revival and admit surviving
/// combat casualties into its Hospital or Big Hospital (FR-011).
/// </summary>
public sealed record AdmitVillageBattleCasualtiesCommand(
    BattleCasualtyAdmissionContext Context,
    long NowMs);
