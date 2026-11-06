using Terravia.Domain.Catalog;
using Terravia.Domain.Oases;

namespace Terravia.Domain.Villages;

/// <summary>
/// Deterministic 5-tier FIFO starvation engine:
/// 1: OasisReinforcement -&gt; 2: VillageReinforcement (excluding captured Nature rows <c>OwnerVillageId == 0</c>)
/// -&gt; 3: TrappedPrisoner -&gt; 4: OwnStationed -&gt; 5: OwnMoving.
/// Selects the first non-empty stack ordered by <c>StackOrMovementId</c> ascending (FIFO),
/// eliminates 1 unit from the strictly largest unit stack (<c>u1..u90</c> excluding <c>u31..u40</c>, lowest <c>UnitTypeId</c> tie-breaker,
/// <c>Hero</c> last only when all regular units in that stack are 0), flags emptied stacks/movements for deletion,
/// and returns the starved unit's base training <c>CropCost</c> (or <c>6</c> for a Hero).
/// </summary>
public sealed class StarvationCalculator : IStarvationCalculator
{
    private readonly IGameCatalog _catalog;

    public StarvationCalculator(IGameCatalog? catalog = null)
    {
        _catalog = catalog ?? FrozenGameCatalog.Default;
    }

    public bool TryResolveNextStarvationStep(
        in StarvationEvaluationContext context,
        out StarvationStepResult stepResult)
    {
        stepResult = default;

        if (context.IsPeaceMode || context.CurrentStoredCrop > 0.0 || context.NetCropPerHour >= 0)
            return false;

        if (context.CandidateStacks is null || context.CandidateStacks.Count == 0)
            return false;

        // Evaluate tiers 1 through 5 in strict priority order
        for (int tierValue = 1; tierValue <= 5; tierValue++)
        {
            var tier = (StarvationPriorityTier)tierValue;

            // Within the active tier, select the first non-empty eligible stack in strict FIFO order (StackOrMovementId ascending)
            StarvationCandidateStack? selectedStack = null;
            foreach (var candidate in context.CandidateStacks)
            {
                if (candidate.Tier != tier)
                    continue;

                // FR-008 & FR-012: Captured Nature animals (OwnerVillageId == 0) are exempt from starvation
                if (tier == StarvationPriorityTier.VillageReinforcement &&
                    candidate.OwnerVillageId == OasisConsts.CapturedNatureOwnerVillageId)
                {
                    continue;
                }

                if (!HasStarvableUnits(candidate))
                    continue;

                if (selectedStack is null || candidate.StackOrMovementId < selectedStack.StackOrMovementId)
                {
                    selectedStack = candidate;
                }
            }

            if (selectedStack is null)
                continue;

            return StarveSingleUnitFromStack(selectedStack, out stepResult);
        }

        return false;
    }

    private bool StarveSingleUnitFromStack(
        StarvationCandidateStack stack,
        out StarvationStepResult stepResult)
    {
        int bestUnitTypeId = 0;
        int maxCount = 0;

        for (int u = 1; u < stack.UnitCounts.Length && u <= 90; u++)
        {
            // Captured Nature animals (u31..u40) are exempt from starvation
            if (u is >= OasisConsts.MinNatureUnitTypeId and <= OasisConsts.MaxNatureUnitTypeId)
                continue;

            int count = stack.UnitCounts[u];
            if (count > maxCount) // strictly greater preserves lowest UnitTypeId on ties
            {
                maxCount = count;
                bestUnitTypeId = u;
            }
        }

        bool isHeroStarved = false;
        int creditedCrop;

        if (bestUnitTypeId > 0)
        {
            stack.UnitCounts[bestUnitTypeId]--;
            creditedCrop = _catalog.GetUnit(bestUnitTypeId).Cost.Crop;
        }
        else if (stack.HasHero)
        {
            // Hero starves ONLY when all regular units (u1..u90) in this stack are 0
            stack.HasHero = false;
            isHeroStarved = true;
            creditedCrop = VillageConsts.HeroStarvationCropCredit;
        }
        else
        {
            stepResult = default;
            return false;
        }

        bool isEmptyAfterDecrement = !HasStarvableUnits(stack) && stack.TotalRegularUnits == 0 && !stack.HasHero;
        bool shouldDelete = isEmptyAfterDecrement && stack.Tier != StarvationPriorityTier.OwnStationed;
        int freedTraps = stack.Tier == StarvationPriorityTier.TrappedPrisoner ? 1 : 0;

        stepResult = new StarvationStepResult(
            StarvedTier: stack.Tier,
            TargetStackOrMovementId: stack.StackOrMovementId,
            StarvedUnitTypeId: bestUnitTypeId,
            IsHeroStarved: isHeroStarved,
            CreditedCrop: creditedCrop,
            ShouldDeleteEmptyStackOrMovement: shouldDelete,
            FreedTrapCount: freedTraps);

        return true;
    }

    private static bool HasStarvableUnits(StarvationCandidateStack stack)
    {
        for (int u = 1; u < stack.UnitCounts.Length && u <= 90; u++)
        {
            if (u is >= OasisConsts.MinNatureUnitTypeId and <= OasisConsts.MaxNatureUnitTypeId)
                continue;
            if (stack.UnitCounts[u] > 0)
                return true;
        }
        return stack.HasHero;
    }
}
