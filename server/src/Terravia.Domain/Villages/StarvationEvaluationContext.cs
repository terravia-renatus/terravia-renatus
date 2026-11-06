namespace Terravia.Domain.Villages;

/// <summary>
/// Input context for evaluating deterministic 5-tier FIFO troop starvation when <c>Crop &lt;= 0</c> and <c>NetCrop &lt; 0</c>.
/// </summary>
public readonly record struct StarvationEvaluationContext(
    double CurrentStoredCrop,
    int NetCropPerHour,
    bool IsPeaceMode,
    IReadOnlyList<StarvationCandidateStack> CandidateStacks);
