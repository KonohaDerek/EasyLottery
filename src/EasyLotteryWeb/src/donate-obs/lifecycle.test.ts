import { describe, expect, it } from "vitest";
import {
  acceptDonateSnapshot,
  advanceDonateReveal,
  completeDonateAnimation,
  createDonateObsState,
  shouldWaitForAnimationVideo,
  type DonateDrawRecord
} from "./lifecycle";

const activity = { id: 7, showDonateInformation: true, animationDurationSeconds: 8, resultDisplayDurationSeconds: 15 };

function draw(id: number): DonateDrawRecord {
  return { id, donorName: `贊助者 ${id}`, amount: id * 100, prizeName: `獎品 ${id}`, isWinning: true };
}

describe("Donate OBS reveal lifecycle", () => {
  it("uses the latest initial record as a cursor without replaying history", () => {
    const state = acceptDonateSnapshot(createDonateObsState(), activity.id, [draw(2), draw(5), draw(3)]);

    expect(state.lastSeenId).toBe(5);
    expect(state.current).toBeNull();
    expect(state.queue).toEqual([]);
  });

  it("queues new records in ID order once across repeated snapshots", () => {
    const initial = acceptDonateSnapshot(createDonateObsState(), activity.id, [draw(2)]);
    const next = acceptDonateSnapshot(initial, activity.id, [draw(4), draw(3), draw(4), draw(2)]);
    const repeated = acceptDonateSnapshot(next, activity.id, [draw(2), draw(3), draw(4)]);

    expect(repeated.lastSeenId).toBe(4);
    expect(repeated.queue.map(record => record.id)).toEqual([3, 4]);
  });

  it("resets queued results on activity changes and does not replay the new snapshot", () => {
    const withPending = acceptDonateSnapshot(
      acceptDonateSnapshot(createDonateObsState(), activity.id, [draw(1)]),
      activity.id,
      [draw(1), draw(2)]
    );
    const changed = acceptDonateSnapshot(withPending, 8, [draw(20), draw(21)]);

    expect(changed.activityId).toBe(8);
    expect(changed.lastSeenId).toBe(21);
    expect(changed.current).toBeNull();
    expect(changed.queue).toEqual([]);
  });

  it("advances notification, animation and result phases, then clears the result", () => {
    const queued = acceptDonateSnapshot(
      acceptDonateSnapshot(createDonateObsState(), activity.id, []),
      activity.id,
      [draw(1)]
    );
    const started = advanceDonateReveal(queued, activity, 1_000);
    const animated = advanceDonateReveal(started.state, activity, 4_000);
    const revealed = completeDonateAnimation(animated.state, activity, 5_000);
    const cleared = advanceDonateReveal(revealed.state, activity, 20_000);

    expect(started.state.phase).toBe("notification");
    expect(animated.state.phase).toBe("animation");
    expect(revealed.transition.enteredReveal).toBe(true);
    expect(revealed.state.phase).toBe("reveal");
    expect(cleared.transition.cleared).toBe(true);
    expect(cleared.state.current).toBeNull();
  });

  it("does not wait for a failed video after the configured animation duration", () => {
    const queued = acceptDonateSnapshot(
      acceptDonateSnapshot(createDonateObsState(), activity.id, []),
      activity.id,
      [draw(1)]
    );
    const started = advanceDonateReveal(queued, { ...activity, showDonateInformation: false }, 1_000);
    const waiting = advanceDonateReveal(started.state, activity, 9_001, false);
    const failedVideo = advanceDonateReveal(waiting.state, activity, 9_002, true);

    expect(waiting.state.phase).toBe("animation");
    expect(failedVideo.state.phase).toBe("reveal");
  });

  it("releases a single-play WebM when its playback watchdog expires", () => {
    const playing = {
      phase: "animation" as const,
      configured: true,
      loop: false,
      reducedMotion: false,
      ready: true,
      failed: false,
      ended: false,
      timedOut: false
    };

    expect(shouldWaitForAnimationVideo(playing)).toBe(true);
    expect(shouldWaitForAnimationVideo({ ...playing, timedOut: true })).toBe(false);
    expect(shouldWaitForAnimationVideo({ ...playing, loop: true })).toBe(false);
  });
});
