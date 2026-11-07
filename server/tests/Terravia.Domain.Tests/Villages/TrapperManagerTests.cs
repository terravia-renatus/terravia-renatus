using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class TrapperManagerTests
{
    private readonly TrapperManager _manager = new();

    [Fact]
    public void CalculateTotalTrapCapacity_SumsMultipleTrappersUpTo400PerLevel20()
    {
        var state = CreateGaulVillage();
        state.SetSlot(20, 36, 20); // L20 Trapper = 400
        state.SetSlot(21, 36, 10); // L10 Trapper = 154

        int cap = _manager.CalculateTotalTrapCapacity(state.SlotBuildingTypes, state.SlotLevels);
        Assert.Equal(554, cap);
    }

    [Fact]
    public void InterceptIncomingArmy_BypassesRecon_ExcludesHeroOnPartialCapture_AndClampsRemainder()
    {
        var state = CreateGaulVillage();
        state.SetSlot(20, 36, 20);
        state.TotalTrapsCount = 15;
        state.OccupiedTrapsCount = 0;

        int[] incoming = new int[11];
        incoming[1] = 2;  // 2 Legionnaires
        incoming[2] = 20; // 20 Praetorians

        // 1. Reconnaissance (AttackType = 1) bypasses traps completely
        var reconRes = _manager.InterceptIncomingArmy(state, 1, 50, 200, 10, 1, incoming, incomingHasHero: true, 1);
        Assert.Equal(0, reconRes.TotalCapturedUnits);
        Assert.Equal(0, state.OccupiedTrapsCount);

        // 2. Partial Capture (15 free traps < 23 total units including Hero):
        // Hero escapes! Remainder traps never overflow slot 1 (which only has 2 units).
        var raidRes = _manager.InterceptIncomingArmy(state, 4, 50, 200, 10, 1, incoming, incomingHasHero: true, 1);
        Assert.False(raidRes.HeroWasCaptured);
        Assert.True(raidRes.AttackingHeroRemains);
        Assert.Equal(15, raidRes.TotalCapturedUnits);
        Assert.True(raidRes.CapturedUnitsBySlot[1] <= 2);
        Assert.Equal(15, raidRes.CapturedUnitsBySlot[1] + raidRes.CapturedUnitsBySlot[2]);
        Assert.Equal(15, state.OccupiedTrapsCount);
    }

    [Fact]
    public void InterceptIncomingArmy_FullCapture_CapturesHero_AndArchonCapitalAutoTraps100Percent()
    {
        var state = CreateGaulVillage();
        state.SetSlot(20, 36, 20);
        state.TotalTrapsCount = 100;

        int[] incoming = new int[11];
        incoming[1] = 25;

        var fullRes = _manager.InterceptIncomingArmy(state, 3, 50, 200, 10, 1, incoming, incomingHasHero: true, 1);
        Assert.True(fullRes.HeroWasCaptured);
        Assert.False(fullRes.AttackingHeroRemains);
        Assert.Equal(26, fullRes.TotalCapturedUnits);

        // Archon Capital auto-traps 100% even on Reconnaissance
        var archonCap = new VillageState(99, 5, tribeId: 5, isCapital: true) { IsArchonVillage = true };
        var archonRes = _manager.InterceptIncomingArmy(archonCap, 1, 50, 200, 10, 1, incoming, incomingHasHero: true, 2);
        Assert.Equal(26, archonRes.TotalCapturedUnits);
        Assert.True(archonRes.HeroWasCaptured);
    }

    [Fact]
    public void PrisonerReleasePaths_CombatLiberation_DefenderRelease_AndAttackerDissolution_EnforceMortalityAndFreeAutoRepairs()
    {
        var state = CreateGaulVillage();
        state.SetSlot(20, 36, 20); // Capacity 400
        state.TotalTrapsCount = 100;

        int[] armyA = new int[11];
        armyA[1] = 20; // From village 50 (same attacking village)
        int[] armyB = new int[11];
        armyB[2] = 12; // From village 60 (allied village)

        _manager.InterceptIncomingArmy(state, 4, 50, 200, 10, 1, armyA, incomingHasHero: true, 1);
        _manager.InterceptIncomingArmy(state, 4, 60, 201, 10, 1, armyB, incomingHasHero: false, 2);
        Assert.Equal(33, state.OccupiedTrapsCount);

        // Path A: Combat Liberation from village 50 (player 200, alliance 10)
        var libRes = _manager.LiberatePrisonersOnSuccessfulNormalAttack(state, 50, 200, 10, null, 10_000L);
        Assert.Equal(2, libRes.LiberatedStacks.Count);
        Assert.Equal(33, libRes.TotalDestroyedTraps);
        // 20 - round(20/4) = 15 surviving from village 50 merged into attacker return
        Assert.Equal(15, libRes.MergedIntoAttackerReturnBySlot[1]);
        Assert.True(libRes.MergedHeroIntoAttackerReturn);
        // round(33 / 3) = 11 free auto-repaired traps queued!
        Assert.Equal(11, libRes.FreeAutoRepairedTrapsQueued);
        Assert.Equal(67, state.TotalTrapsCount);
        Assert.Equal(0, state.OccupiedTrapsCount);

        // Capture two more stacks for Path B (Defender Manual Release) and Path C (Attacker Dissolution)
        _manager.InterceptIncomingArmy(state, 4, 70, 300, 20, 2, armyB, incomingHasHero: false, 3);
        _manager.InterceptIncomingArmy(state, 4, 80, 301, 20, 2, armyB, incomingHasHero: false, 4);

        // Path B: Defender Manual Release -> 100% survive (12) and 100% free trap repairs (12)
        var relRes = _manager.ReleasePrisonersByTrapOwner(state, new ReleasePrisonersCommand(state.VillageId, 3), 20_000L, SitterPermissions.Reinforce);
        Assert.True(relRes.Result.Succeeded);
        Assert.Equal(12, relRes.Outcome!.Value.SurvivingUnitsBySlot[2]);
        Assert.Equal(12, relRes.FreeRepairsQueued);

        // Path C: Attacker Dissolution -> 100% die (12) and round(12 / 3) = 4 free trap repairs
        var disRes = _manager.DissolvePrisonersByTroopOwner(state, new DissolvePrisonersCommand(state.VillageId, 301, 4), 30_000L, SitterPermissions.Reinforce);
        Assert.True(disRes.Result.Succeeded);
        Assert.Equal(12, disRes.DissolvedCount);
        Assert.Equal(4, disRes.FreeRepairsQueued);
    }

    private static VillageState CreateGaulVillage()
    {
        var state = new VillageState(1, 100, tribeId: 3, isCapital: false)
        {
            WarehouseCapacity = 500_000,
            GranaryCapacity = 500_000,
            Wood = 200_000,
            Clay = 200_000,
            Iron = 200_000,
            Crop = 200_000,
            ServerSpeed = 1.0
        };
        state.SyncExactResourcesFromInteger();
        return state;
    }
}
