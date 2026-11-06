using Terravia.Domain.Catalog;
using Terravia.Domain;
using Terravia.Domain.Villages;

namespace Terravia.Domain.Tests.Villages;

public sealed class BuildingDemolitionTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly IConstructionCalculator _calculator;
    private readonly IBuildingRuleCatalog _ruleCatalog;
    private readonly VillageConstructionManager _manager;

    public BuildingDemolitionTests()
    {
        _calculator = new ConstructionCalculator(_catalog);
        _ruleCatalog = new BuildingRuleCatalog(_catalog, _calculator);
        _manager = new VillageConstructionManager(_catalog, _ruleCatalog, _calculator);
    }

    private static VillageState CreateDemolitionVillage(int mainBuildingLevel = 10)
    {
        var state = new VillageState(villageId: 200, ownerPlayerId: 1, tribeId: 1, isCapital: true)
        {
            PlayerGoldBalance = 50,
            Population = 100
        };
        state.SetSlot(19, 15, mainBuildingLevel); // Main Building
        state.SetSlot(20, 19, 3); // Barracks Level 3
        state.SetSlot(21, 10, 5); // Warehouse Level 5
        return state;
    }

    [Fact]
    public void Demolition_ShouldRequireMainBuildingAtLeastLevel10()
    {
        var stateMb9 = CreateDemolitionVillage(mainBuildingLevel: 9);
        var (res, _) = _manager.StartDemolition(
            stateMb9,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 20, IsInstantCompleteWithGold: false),
            nowMs: 1000L);

        Assert.False(res.Succeeded);
        Assert.Equal("MainBuildingBelowLevel10", res.ErrorCode);
    }

    [Fact]
    public void TimedDemolition_ShouldReduceLevelByOne_AndClearSlotTypeWhenReachingZero()
    {
        var state = CreateDemolitionVillage(mainBuildingLevel: 10);
        state.SetSlot(22, 23, 1); // Cranny Level 1 on Slot 22

        var (res, gold) = _manager.StartDemolition(
            state,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 22, IsInstantCompleteWithGold: false),
            nowMs: 1000L);

        Assert.True(res.Succeeded);
        Assert.Equal(0, gold);
        Assert.NotNull(state.ActiveDemolition);
        Assert.Equal(0, state.ActiveDemolition.TargetLevel);

        // Complete timed demolition
        bool completed = _manager.CompleteDueDemolition(state, state.ActiveDemolition.FinishesAtMs);
        Assert.True(completed);
        Assert.Null(state.ActiveDemolition);
        Assert.Equal(0, state.GetSlotLevel(22));
        Assert.Equal(0, state.GetSlotBuildingType(22)); // Cleared to empty slot
    }

    [Fact]
    public void TenGoldInstantCompleteDemolition_ShouldRazeDirectlyToZero_AndDeductCumulativePopulation()
    {
        var state = CreateDemolitionVillage(mainBuildingLevel: 10);
        int initialPop = state.Population;
        int barracksCumulativePop =
            _catalog.GetBuildingLevelStats(19, 1).Population +
            _catalog.GetBuildingLevelStats(19, 2).Population +
            _catalog.GetBuildingLevelStats(19, 3).Population;

        var (res, gold) = _manager.StartDemolition(
            state,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 20, IsInstantCompleteWithGold: true),
            nowMs: 1000L);

        Assert.True(res.Succeeded);
        Assert.Equal(10, gold);
        Assert.Equal(40, state.PlayerGoldBalance);
        Assert.Equal(0, state.GetSlotLevel(20));
        Assert.Equal(0, state.GetSlotBuildingType(20));
        Assert.Equal(initialPop - barracksCumulativePop, state.Population);
    }

    [Fact]
    public void AllianceFounderEmbassySafeguard_ShouldBlockDemolition_WhenLastQualifyingEmbassy()
    {
        var state = CreateDemolitionVillage(mainBuildingLevel: 10);
        state.IsAllianceOwner = true;
        state.AllianceMemberCount = 10; // Requires min Embassy level = ceil(10 / 3.0) = 4
        state.QualifyingEmbassyCountAcrossPlayerVillages = 1;
        state.SetSlot(25, 18, 4); // Embassy Level 4

        // Blocked for both timed and 10-Gold instant demolition
        var (timedRes, _) = _manager.StartDemolition(
            state,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 25, IsInstantCompleteWithGold: false),
            nowMs: 1000L);
        Assert.False(timedRes.Succeeded);
        Assert.Equal("CannotDemolishLastAllianceEmbassy", timedRes.ErrorCode);

        var (instantRes, _) = _manager.StartDemolition(
            state,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 25, IsInstantCompleteWithGold: true),
            nowMs: 1000L);
        Assert.False(instantRes.Succeeded);
        Assert.Equal("CannotDemolishLastAllianceEmbassy", instantRes.ErrorCode);

        // Allowed if player has a second qualifying Embassy in another village
        state.QualifyingEmbassyCountAcrossPlayerVillages = 2;
        var (allowedRes, _) = _manager.StartDemolition(
            state,
            new StartBuildingDemolitionCommand(1, 1, 200, SlotId: 25, IsInstantCompleteWithGold: false),
            nowMs: 1000L);
        Assert.True(allowedRes.Succeeded);
    }
}
