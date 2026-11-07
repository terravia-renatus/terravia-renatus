using Terravia.Domain.Catalog;
using Terravia.Domain.Villages;
using Xunit;

namespace Terravia.Domain.Tests.Villages;

public sealed class HospitalHealingManagerTests
{
    private readonly IGameCatalog _catalog = FrozenGameCatalog.Default;
    private readonly HospitalHealingManager _manager = new();
    private readonly MilitaryTrainingManager _trainingManager = new();

    [Fact]
    public void AdmitBattleCasualties_Hospital40Percent_BigHospital50Percent_AndHeroBandagePrecedence()
    {
        // Roman village with Hospital (gid 46) at Level 10 -> capacity = 300, rate = 40%
        var romanState = CreateVillageState(tribeId: 1);
        romanState.SetSlot(25, 46, 10);
        Assert.Equal(300, _manager.GetMaxHospitalCapacity(romanState));

        // 100 dead slot-1 infantry, Hero carries 50 Large Bandages (33% revival)
        // Bandages revive floor(100 * 0.33) = 33 troops first; remaining dead = 67
        // Hospital intercepts floor(67 * 0.40) = 26 wounded troops
        int[] deadBySlot = new int[11];
        deadBySlot[1] = 100;

        var resRoman = _manager.AdmitBattleCasualties(
            romanState,
            new BattleCasualtyAdmissionContext(
                DeadUnitsBySlot: deadBySlot,
                HasSurvivingHeroWithBandages: true,
                BandageTier: HeroBandageTier.LargeBandage33,
                AvailableBandageItemCount: 50));

        Assert.Equal(33, resRoman.BandagedUnitsBySlot[1]);
        Assert.Equal(33, resRoman.TotalBandagesConsumed);
        Assert.Equal(26, resRoman.AdmittedWoundedBySlot[1]);
        Assert.Equal(26, romanState.WoundedUnitsBySlot[1]);

        // Spartan village with Big Hospital (gid 48) at Level 10 -> capacity = 600, rate = 50%
        var spartanState = CreateVillageState(tribeId: 8);
        spartanState.SetSlot(25, 48, 10);
        Assert.Equal(600, _manager.GetMaxHospitalCapacity(spartanState));

        var resSpartan = _manager.AdmitBattleCasualties(
            spartanState,
            new BattleCasualtyAdmissionContext(DeadUnitsBySlot: deadBySlot));
        Assert.Equal(50, resSpartan.AdmittedWoundedBySlot[1]);
    }

    [Fact]
    public void AdmitBattleCasualties_ClampsInAscendingSlotOrder1To8_AndRetainsWoundedOnHospitalDamage()
    {
        // Hospital L5 -> max capacity 150, already holds 120 wounded in slot 1 -> free = 30
        var state = CreateVillageState(tribeId: 1);
        state.SetSlot(25, 46, 5);
        state.WoundedUnitsBySlot[1] = 120;

        int[] dead = new int[11];
        dead[1] = 65; // floor(65 * 0.40) = 26 wounded
        dead[2] = 50; // floor(50 * 0.40) = 20 wounded (clamped to remaining 4!)
        dead[9] = 10; // Chief -> ignored
        dead[10] = 10; // Settler -> ignored

        var res = _manager.AdmitBattleCasualties(state, new BattleCasualtyAdmissionContext(dead));
        Assert.Equal(26, res.AdmittedWoundedBySlot[1]);
        Assert.Equal(4, res.AdmittedWoundedBySlot[2]);
        Assert.Equal(0, res.RemainingFreeHospitalCapacity);
        Assert.Equal(146, state.WoundedUnitsBySlot[1]);
        Assert.Equal(4, state.WoundedUnitsBySlot[2]);

        // Damage Hospital to Level 0 (catapulted): existing 150 wounded are retained (soft cap), but new healing cannot start until rebuilt
        state.SetSlot(25, 0, 0);
        Assert.Equal(146, state.WoundedUnitsBySlot[1]);

        var blockedHeal = _manager.EnqueueHealingBatch(state, new EnqueueHospitalHealingCommand(state.VillageId, 1, 10), 1_000L);
        Assert.False(blockedHeal.Succeeded);
        Assert.Equal(TerraviaDomainErrorCodes.Villages.TrainingFacilityNotBuilt, blockedHeal.ErrorCode);
    }

    [Fact]
    public void EnqueueHealingBatch_Deducts50PercentCost_AndChainsSequentialSingleLaneQueue()
    {
        var state = CreateVillageState(tribeId: 1);
        state.SetSlot(25, 46, 7); // Hospital L7 (attri = 0.5314 -> ~50% duration)
        state.WoundedUnitsBySlot[1] = 20;
        state.WoundedUnitsBySlot[2] = 20;

        long t0 = 10_000L;
        var u1 = _catalog.GetUnit(1);

        var h1 = _manager.EnqueueHealingBatch(state, new EnqueueHospitalHealingCommand(state.VillageId, 1, 10), t0);
        Assert.True(h1.Succeeded);
        Assert.Equal((int)Math.Ceiling(u1.Cost.Wood * 10 / 2.0), h1.DeductedCost.Wood);
        Assert.Equal(10, state.WoundedUnitsBySlot[1]);

        // Queue second healing batch for slot 2 (Praetorian u2) -> MUST chain after h1.FinishesAtMs (not run in parallel!)
        var h2 = _manager.EnqueueHealingBatch(state, new EnqueueHospitalHealingCommand(state.VillageId, 2, 5), t0 + 1_000L);
        Assert.True(h2.Succeeded);
        Assert.Equal(2, state.TrainingQueues.Count);
        Assert.Equal(h1.FinishesAtMs, state.TrainingQueues[1].CommenceAtMs);

        // Graduate first unit of h1
        int graduated = _trainingManager.CompleteDueTrainingGraduations(state, t0 + h1.EachTimeSeconds * 1000L);
        Assert.Equal(1, graduated);
        Assert.Equal(1, state.TroopStacks[0].UnitCounts[1]);
    }

    private static VillageState CreateVillageState(int tribeId)
    {
        var state = new VillageState(1, 100, tribeId, isCapital: false)
        {
            WarehouseCapacity = 500_000,
            GranaryCapacity = 500_000,
            Wood = 200_000,
            Clay = 200_000,
            Iron = 200_000,
            Crop = 200_000,
            NetCropProductionPerHour = 1_000,
            ServerSpeed = 1.0
        };
        state.SyncExactResourcesFromInteger();
        return state;
    }
}
