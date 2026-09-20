import { expect, test } from "./fixtures.mjs";

test("主持人可建立、暫停並結算互動回合", async ({ request }) => {
  const tokenResponse = await request.get("/api/test/session-token");
  expect(tokenResponse.ok()).toBeTruthy();
  const token = (await tokenResponse.json()).token;
  const headers = { "X-EasyLottery-Session-Token": token };
  const created = await request.post("/api/interactions/rounds", { headers, data: { name: "E2E 投票", type: "Vote", commandPrefix: "!vote", voteOptions: ["a", "b"] } });
  expect(created.ok()).toBeTruthy();
  const round = await created.json();
  expect((await request.post(`/api/interactions/rounds/${round.id}/start`, { headers })).ok()).toBeTruthy();
  expect((await request.post(`/api/interactions/rounds/${round.id}/pause`, { headers })).ok()).toBeTruthy();
  const event = await request.post(`/api/interactions/rounds/${round.id}/events`, { headers, data: { audienceProfileId: "b8dd986b-b3d3-4b7d-ae8a-0253bb6a6240", platform: "YouTube", channelScope: "test", externalUserId: "viewer", externalEventId: "event", content: "!vote a" } });
  expect(event.ok()).toBeTruthy();
  expect((await event.json()).reason).toBe("round-not-live");
});
