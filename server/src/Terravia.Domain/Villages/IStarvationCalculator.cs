namespace Terravia.Domain.Villages;

/// <summary>
/// Pure, deterministic 5-tier FIFO starvation calculator (<c>FR-012</c>).
/// </summary>
public interface IStarvationCalculator
{
    bool TryResolveNextStarvationStep(
        in StarvationEvaluationContext context,
        out StarvationStepResult stepResult);
}
