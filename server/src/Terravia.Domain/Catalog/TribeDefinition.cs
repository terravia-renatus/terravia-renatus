using System.Collections.Immutable;

namespace Terravia.Domain.Catalog;

/// <summary>
/// Immutable faction definition for one of the 9 tribes (TribeId 1..9).
/// </summary>
public sealed record TribeDefinition
{
    public int TribeId { get; }
    public string Name { get; }
    public bool IsPlayable { get; }
    public int MerchantCapacity { get; }
    public int MerchantSpeed { get; }
    public double CrannyMultiplier { get; }
    public int WallBuildingTypeId { get; }
    public double WallFactor { get; }
    public bool HasDualBuildQueue { get; }
    public bool CanBuildResidenceOrPalace { get; }
    public ImmutableArray<int> CommandBuildingTypeIds { get; }
    public int HospitalBuildingTypeId { get; }
    public ImmutableArray<int> ExclusiveBuildingTypeIds { get; }
    public ImmutableArray<int> UnitIds { get; }

    public TribeDefinition(
        int tribeId,
        string name,
        bool isPlayable,
        int merchantCapacity,
        int merchantSpeed,
        double crannyMultiplier,
        int wallBuildingTypeId,
        double wallFactor,
        bool hasDualBuildQueue,
        bool canBuildResidenceOrPalace,
        ImmutableArray<int> commandBuildingTypeIds,
        int hospitalBuildingTypeId,
        ImmutableArray<int> exclusiveBuildingTypeIds,
        ImmutableArray<int> unitIds)
    {
        if (tribeId is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(tribeId), "TribeId must be in range 1..9.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        if (unitIds.Length != 10)
            throw new ArgumentException("Each tribe must have exactly 10 units.", nameof(unitIds));

        TribeId = tribeId;
        Name = name;
        IsPlayable = isPlayable;
        MerchantCapacity = merchantCapacity;
        MerchantSpeed = merchantSpeed;
        CrannyMultiplier = crannyMultiplier;
        WallBuildingTypeId = wallBuildingTypeId;
        WallFactor = wallFactor;
        HasDualBuildQueue = hasDualBuildQueue;
        CanBuildResidenceOrPalace = canBuildResidenceOrPalace;
        CommandBuildingTypeIds = commandBuildingTypeIds;
        HospitalBuildingTypeId = hospitalBuildingTypeId;
        ExclusiveBuildingTypeIds = exclusiveBuildingTypeIds;
        UnitIds = unitIds;
    }
}
