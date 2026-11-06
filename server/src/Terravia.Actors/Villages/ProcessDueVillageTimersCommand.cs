namespace Terravia.Actors.Villages;

/// <summary>
/// Timer command scheduled via <see cref="Akka.Actor.ITimerScheduler"/> to complete due constructions or demolitions.
/// </summary>
public sealed record ProcessDueVillageTimersCommand(long NowMs);
