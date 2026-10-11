import { describe, expect, it } from "vitest";
import { canApplyPokeLiveDrawState, canApplyPokeTemplateSnapshot, normalizeLiveDrawPhase, type PokeLiveDrawState } from "./live-draw";

const state: PokeLiveDrawState = {
  publicId: "a22b3cc4-9c67-48d6-b0b9-78fc2f2274b0",
  revision: 4,
  phase: 2,
  phaseEndsAtUtc: null,
  pokeCellIndex: 1,
  resultTitle: "獎品",
  updatedAtUtc: "2026-10-11T00:00:00Z"
};

describe("PokeBox Live Draw state", () => {
  it("accepts current-template updates case-insensitively and rejects stale or foreign state", () => {
    expect(canApplyPokeLiveDrawState(state, state.publicId.toUpperCase(), 3)).toBe(true);
    expect(canApplyPokeLiveDrawState(state, state.publicId, 5)).toBe(false);
    expect(canApplyPokeLiveDrawState(state, "different-template", 0)).toBe(false);
  });

  it("falls back to ready for an unknown server phase", () => {
    expect(normalizeLiveDrawPhase(3)).toBe(3);
    expect(normalizeLiveDrawPhase(99)).toBe(0);
  });

  it("rejects delayed template responses superseded by another request or live revision", () => {
    expect(canApplyPokeTemplateSnapshot(1, 2, 3, 4)).toBe(false);
    expect(canApplyPokeTemplateSnapshot(2, 2, 3, 4)).toBe(false);
    expect(canApplyPokeTemplateSnapshot(2, 2, 4, 4)).toBe(true);
  });
});
