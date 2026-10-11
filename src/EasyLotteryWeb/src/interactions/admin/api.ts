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

export async function completeIdentityLink(code: string): Promise<void> {
  await apiRequest<void>("/api/interactions/identity-links/complete", {
    method: "POST",
    body: JSON.stringify({ code })
  });
}
