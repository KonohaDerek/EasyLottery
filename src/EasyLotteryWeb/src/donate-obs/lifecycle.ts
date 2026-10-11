export interface DonateDrawRecord {
  id: number;
  activityId?: number;
  donorName: string;
  amount: number;
  prizeName: string;
  prizeImageUrl?: string;
  isWinning: boolean;
  donationMessage?: string;
  paymentMethod?: string;
}

export interface DonateRevealSettings {
  id: number;
  showDonateInformation: boolean;
  animationDurationSeconds: number;
  resultDisplayDurationSeconds: number;
}

export type DonateRevealPhase = "notification" | "animation" | "reveal";

export interface DonateObsState {
  activityId: number | null;
  lastSeenId: number;
  current: DonateDrawRecord | null;
  queue: DonateDrawRecord[];
  phase: DonateRevealPhase | null;
  phaseEndsAt: number | null;
  resultEndsAt: number | null;
}

export interface DonateRevealTransition {
  isNewResult: boolean;
  enteredReveal: boolean;
  cleared: boolean;
}

export function shouldWaitForAnimationVideo(video: {
  phase: DonateRevealPhase | null;
  configured: boolean;
  loop: boolean;
  reducedMotion: boolean;
  ready: boolean;
  failed: boolean;
  ended: boolean;
  timedOut: boolean;
}): boolean {
  return video.phase === "animation"
    && video.configured
    && !video.loop
    && !video.reducedMotion
    && video.ready
    && !video.failed
    && !video.ended
    && !video.timedOut;
}

export function createDonateObsState(): DonateObsState {
  return {
    activityId: null,
    lastSeenId: 0,
    current: null,
    queue: [],
    phase: null,
    phaseEndsAt: null,
    resultEndsAt: null
  };
}

export function acceptDonateSnapshot(
  state: DonateObsState,
  activityId: number | null,
  records: DonateDrawRecord[]
): DonateObsState {
  if (activityId === null) return createDonateObsState();

  const ordered = [...new Map(records
    .filter(record => Number.isInteger(record.id) && record.id > 0)
    .map(record => [record.id, record])).values()]
    .sort((left, right) => left.id - right.id);

  if (state.activityId !== activityId) {
    return { ...createDonateObsState(), activityId, lastSeenId: ordered.at(-1)?.id ?? 0 };
  }

  const unseen = ordered.filter(record => record.id > state.lastSeenId);
  if (unseen.length === 0) return state;
  return {
    ...state,
    lastSeenId: unseen.at(-1)?.id ?? state.lastSeenId,
    queue: [...state.queue, ...unseen]
  };
}

export function advanceDonateReveal(
  state: DonateObsState,
  settings: DonateRevealSettings,
  now: number,
  animationCompleted = true
): { state: DonateObsState; transition: DonateRevealTransition } {
  const unchanged = { state, transition: { isNewResult: false, enteredReveal: false, cleared: false } };
  if (!state.current) {
    const [next, ...queue] = state.queue;
    if (!next) return unchanged;
    const phase: DonateRevealPhase = settings.showDonateInformation ? "notification" : "animation";
    const duration = phase === "notification" ? 3 : clamp(settings.animationDurationSeconds, 3, 30);
    return {
      state: {
        ...state,
        current: next,
        queue,
        phase,
        phaseEndsAt: now + duration * 1000,
        resultEndsAt: null
      },
      transition: { isNewResult: true, enteredReveal: false, cleared: false }
    };
  }

  if (state.phase === "notification" && state.phaseEndsAt !== null && now >= state.phaseEndsAt) {
    return {
      state: {
        ...state,
        phase: "animation",
        phaseEndsAt: now + clamp(settings.animationDurationSeconds, 3, 30) * 1000
      },
      transition: unchanged.transition
    };
  }

  if (state.phase === "animation" && state.phaseEndsAt !== null && now >= state.phaseEndsAt && animationCompleted) {
    return completeDonateAnimation(state, settings, now);
  }

  if (state.phase === "reveal" && state.resultEndsAt !== null && now >= state.resultEndsAt) {
    return {
      state: { ...state, current: null, phase: null, phaseEndsAt: null, resultEndsAt: null },
      transition: { isNewResult: false, enteredReveal: false, cleared: true }
    };
  }

  return unchanged;
}

export function completeDonateAnimation(
  state: DonateObsState,
  settings: DonateRevealSettings,
  now: number
): { state: DonateObsState; transition: DonateRevealTransition } {
  if (state.current === null || state.phase !== "animation") {
    return { state, transition: { isNewResult: false, enteredReveal: false, cleared: false } };
  }
  return {
    state: {
      ...state,
      phase: "reveal",
      phaseEndsAt: null,
      resultEndsAt: now + clamp(settings.resultDisplayDurationSeconds, 3, 300) * 1000
    },
    transition: { isNewResult: false, enteredReveal: true, cleared: false }
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
