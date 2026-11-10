import { apiRequest } from "../../api/client";

export interface InteractionRound {
  id: string;
  name: string;
  status: string;
  type: string;
  commandPrefix: string;
  voteOptions: string[];
  correctAnswerPoints: number;
  eligibilityTickets: number;
}

export interface CreateInteractionRound {
  name: string;
  type: "SignIn" | "Vote" | "Quiz";
  commandPrefix: string;
  voteOptions: string[];
  correctAnswer: string | null;
  correctAnswerPoints: number;
  eligibilityTickets: number;
}

export async function listInteractionRounds(): Promise<InteractionRound[]> {
  return (await apiRequest<InteractionRound[]>("/api/interactions/rounds")).data;
}

export async function createInteractionRound(round: CreateInteractionRound): Promise<InteractionRound> {
  return (await apiRequest<InteractionRound>("/api/interactions/rounds", {
    method: "POST",
    body: JSON.stringify(round)
  })).data;
}

export async function transitionInteractionRound(id: string, action: "start" | "pause" | "settle" | "cancel"): Promise<InteractionRound> {
  return (await apiRequest<InteractionRound>(`/api/interactions/rounds/${encodeURIComponent(id)}/${action}`, {
    method: "POST"
  })).data;
}

/** Admin-only; returns the raw payload so callers validate it (see parseEligibilityPreview). */
export async function previewInteractionEligibility(roundId: string, signal?: AbortSignal): Promise<unknown> {
  return (await apiRequest<unknown>(`/api/interactions/rounds/${encodeURIComponent(roundId)}/eligibility-preview`, { signal })).data;
}

/** Idempotent per round: repeated calls return the same persisted snapshot. */
export async function importInteractionEligibility(roundId: string): Promise<unknown> {
  return (await apiRequest<unknown>(`/api/interactions/rounds/${encodeURIComponent(roundId)}/eligibility-import`, {
    method: "POST"
  })).data;
}

export async function completeIdentityLink(code: string): Promise<void> {
  await apiRequest<void>("/api/interactions/identity-links/complete", {
    method: "POST",
    body: JSON.stringify({ code })
  });
}
