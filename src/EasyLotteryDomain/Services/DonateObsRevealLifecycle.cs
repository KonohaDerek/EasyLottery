using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services;

public enum DonateObsRevealPhase
{
    Notification,
    Animation,
    Reveal
}

public sealed class DonateObsRevealState
{
    public bool IsInitialized { get; internal set; }
    public int? ObservedWinId { get; internal set; }
    public DonateLotteryDrawRecord? CurrentResult { get; internal set; }
    public DonateObsRevealPhase Phase { get; internal set; } = DonateObsRevealPhase.Notification;
    public DateTimeOffset? PhaseExpiresAtUtc { get; internal set; }
    public DateTimeOffset? ResultExpiresAtUtc { get; internal set; }
}

public readonly record struct DonateObsRevealTransition(bool IsNewResult, bool EnteredReveal, bool Cleared);

/// <summary>管理 Donate OBS 從待機、通知、動畫到揭曉的可測結果生命週期。</summary>
public static class DonateObsRevealLifecycle
{
    public static DonateObsRevealTransition Advance(
        DonateObsRevealState state,
        DonateLotteryActivity? activity,
        DonateLotteryDrawRecord? newestResult,
        DateTimeOffset nowUtc)
    {
        if (!state.IsInitialized)
        {
            if (activity is not null)
            {
                state.IsInitialized = true;
                state.ObservedWinId = newestResult?.Id;
                ClearCurrentResult(state);
            }

            return default;
        }

        if (activity is null)
        {
            return default;
        }

        if (newestResult is not null && newestResult.Id != state.ObservedWinId)
        {
            state.ObservedWinId = newestResult.Id;
            state.CurrentResult = newestResult;
            state.Phase = activity.ShowDonateInformation
                ? DonateObsRevealPhase.Notification
                : DonateObsRevealPhase.Animation;
            state.PhaseExpiresAtUtc = nowUtc.AddSeconds(state.Phase == DonateObsRevealPhase.Notification
                ? 3
                : GetAnimationDurationSeconds(activity));
            state.ResultExpiresAtUtc = null;
            return new DonateObsRevealTransition(IsNewResult: true, EnteredReveal: false, Cleared: false);
        }

        if (state.CurrentResult is null || state.PhaseExpiresAtUtc is null || nowUtc < state.PhaseExpiresAtUtc)
        {
            if (state.CurrentResult is not null && state.ResultExpiresAtUtc is not null && nowUtc >= state.ResultExpiresAtUtc)
            {
                ClearCurrentResult(state);
                return new DonateObsRevealTransition(IsNewResult: false, EnteredReveal: false, Cleared: true);
            }

            return default;
        }

        if (state.Phase == DonateObsRevealPhase.Notification)
        {
            state.Phase = DonateObsRevealPhase.Animation;
            state.PhaseExpiresAtUtc = nowUtc.AddSeconds(GetAnimationDurationSeconds(activity));
            return default;
        }

        if (state.Phase == DonateObsRevealPhase.Animation)
        {
            state.Phase = DonateObsRevealPhase.Reveal;
            state.PhaseExpiresAtUtc = null;
            state.ResultExpiresAtUtc = nowUtc.AddSeconds(GetResultDisplayDurationSeconds(activity));
            return new DonateObsRevealTransition(IsNewResult: false, EnteredReveal: true, Cleared: false);
        }

        return default;
    }

    private static int GetAnimationDurationSeconds(DonateLotteryActivity activity) =>
        Math.Clamp(activity.AnimationDurationSeconds, 3, 30);

    private static int GetResultDisplayDurationSeconds(DonateLotteryActivity activity) =>
        Math.Clamp(activity.ResultDisplayDurationSeconds, 3, 300);

    private static void ClearCurrentResult(DonateObsRevealState state)
    {
        state.CurrentResult = null;
        state.PhaseExpiresAtUtc = null;
        state.ResultExpiresAtUtc = null;
    }
}
