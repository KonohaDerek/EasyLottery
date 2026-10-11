import { afterEach, describe, expect, it, vi } from "vitest";
import { completeIdentityLink, createInteractionRound, listInteractionRounds, transitionInteractionRound } from "./api";

describe("interaction management API", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("uses the existing admin REST routes and session-token header", async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify([]), {
      status: 200,
      headers: { "content-type": "application/json" }
    }));
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-session" });

    await listInteractionRounds();

    const [, init] = fetchMock.mock.calls[0];
    expect(fetchMock.mock.calls[0][0]).toBe("/api/interactions/rounds");
    expect(new Headers(init?.headers).get("X-EasyLottery-Session-Token")).toBe("admin-session");
  });

  it("preserves round DTO fields and action endpoint names", async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify({ id: "round-1" }), {
      status: 200,
      headers: { "content-type": "application/json" }
    }));
    vi.stubGlobal("fetch", fetchMock);

    await createInteractionRound({
      name: "Vote night",
      type: "Vote",
      commandPrefix: "!vote",
      voteOptions: ["A", "B"],
      correctAnswer: null,
      correctAnswerPoints: 0,
      eligibilityTickets: 0
    });
    await transitionInteractionRound("round/one", "start");

    expect(fetchMock.mock.calls[0][0]).toBe("/api/interactions/rounds");
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toMatchObject({ name: "Vote night", type: "Vote", voteOptions: ["A", "B"] });
    expect(fetchMock.mock.calls[1][0]).toBe("/api/interactions/rounds/round%2Fone/start");
  });

  it("sends one-time identity codes only in the request body", async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await completeIdentityLink("one-time-code");

    expect(fetchMock.mock.calls[0][0]).toBe("/api/interactions/identity-links/complete");
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({ code: "one-time-code" });
  });
});
