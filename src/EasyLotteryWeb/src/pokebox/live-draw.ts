export type LiveDrawPhase = 0 | 1 | 2 | 3;

export interface PokeLiveDrawState {
  publicId: string;
  revision: number;
  phase: LiveDrawPhase;
  phaseEndsAtUtc: string | null;
  pokeCellIndex: number | null;
  resultTitle: string | null;
  updatedAtUtc: string;
}

export function canApplyPokeLiveDrawState(state: PokeLiveDrawState, publicId: string, appliedRevision: number): boolean {
  return state.publicId.toLowerCase() === publicId.toLowerCase() && state.revision >= appliedRevision;
}

export function canApplyPokeTemplateSnapshot(
  requestGeneration: number,
  latestRequestGeneration: number,
  requestRevision: number,
  appliedRevision: number
): boolean {
  return requestGeneration === latestRequestGeneration && appliedRevision <= requestRevision;
}

export function normalizeLiveDrawPhase(phase: number): LiveDrawPhase {
  return phase === 1 || phase === 2 || phase === 3 ? phase : 0;
}
