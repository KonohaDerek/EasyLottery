import { describe, expect, it } from "vitest";
import { createParticipant, type Participant } from "./draw";
import {
  applyEligibilityImport,
  classifyEligibility,
  parseEligibilityPreview,
  summarizeEligibility,
  type EligibilityPreview
} from "./interaction-eligibility";

const roundId = "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0001";
const snapshotId = "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0002";
const profileA = "aaaaaaaa-0000-4000-8000-000000000001";
const profileB = "bbbbbbbb-0000-4000-8000-000000000002";
const profileC = "cccccccc-0000-4000-8000-000000000003";

function sequentialIds(prefix = "id"): () => string {
  let next = 0;
  return () => `${prefix}-${++next}`;
}

function importedSnapshot(entries: EligibilityPreview["entries"], overrides: Partial<EligibilityPreview> = {}): EligibilityPreview {
  return {
    roundId,
    roundName: "簽到回合",
    roundStatus: "Settled",
    imported: true,
    snapshotId,
    snapshotCreatedAtUtc: "2026-11-10T00:00:00Z",
    liveEligibleCount: entries.length,
    entries,
    ...overrides
  };
}

function manual(name: string, identity?: Participant["identity"], id = `manual-${name}`): Participant {
  return createParticipant({ name, ...(identity ? { identity } : {}) }, () => id);
}

describe("parseEligibilityPreview", () => {
  it("normalizes stable IDs and identities and drops duplicate profiles", () => {
    const preview = parseEligibilityPreview({
      roundId: roundId.toUpperCase(),
      roundName: "簽到回合",
      roundStatus: "Settled",
      imported: true,
      snapshotId: snapshotId.toUpperCase(),
      eligibleCount: 3,
      entries: [
        { audienceProfileId: profileA.toUpperCase(), displayName: " Mika ", identities: [{ platform: "YouTube", accountId: " UC-1 " }, { platform: "youtube", accountId: "UC-1" }, { platform: "", accountId: "x" }] },
        { audienceProfileId: profileA, displayName: "Mika duplicate", identities: [] }
      ]
    }, roundId);

    expect(preview.roundId).toBe(roundId);
    expect(preview.snapshotId).toBe(snapshotId);
    expect(preview.liveEligibleCount).toBe(3);
    expect(preview.entries).toEqual([{ audienceProfileId: profileA, displayName: "Mika", identities: [{ platform: "youtube", accountId: "UC-1" }] }]);
  });

  it("keeps channel scope and treats the same account ID under different scopes as distinct identities", () => {
    const preview = parseEligibilityPreview({
      roundId,
      imported: false,
      entries: [{
        audienceProfileId: profileA,
        displayName: "Mika",
        identities: [
          { platform: "youtube", channelScope: " channel-a ", accountId: "UC-1" },
          { platform: "youtube", channelScope: "channel-a", accountId: "UC-1" },
          { platform: "youtube", channelScope: "channel-b", accountId: "UC-1" },
          { platform: "youtube", channelScope: "", accountId: "UC-1" }
        ]
      }]
    }, roundId);

    expect(preview.entries[0].identities).toEqual([
      { platform: "youtube", accountId: "UC-1", channelScope: "channel-a" },
      { platform: "youtube", accountId: "UC-1", channelScope: "channel-b" },
      { platform: "youtube", accountId: "UC-1" }
    ]);
  });

  it("rejects malformed, mismatched or incomplete responses instead of treating them as an empty list", () => {
    expect(() => parseEligibilityPreview(null, roundId)).toThrow();
    expect(() => parseEligibilityPreview({ roundId: profileA, imported: false, entries: [] }, roundId)).toThrow("不一致");
    expect(() => parseEligibilityPreview({ roundId, imported: true, snapshotId: null, entries: [] }, roundId)).toThrow("缺少識別碼");
    expect(() => parseEligibilityPreview({ roundId, imported: false, entries: [{ audienceProfileId: "Mika" }] }, roundId)).toThrow("觀眾識別碼");
    expect(() => parseEligibilityPreview({ roundId, imported: false, snapshotId: "not-a-uuid", entries: [] }, roundId)).toThrow("快照識別碼");
  });
});

describe("eligibility import", () => {
  it("adds one entry per stable profile with provenance and is idempotent on retry", () => {
    const snapshot = importedSnapshot([
      { audienceProfileId: profileA, displayName: "Mika", identities: [{ platform: "youtube", accountId: "UC-a" }] },
      { audienceProfileId: profileB, displayName: "", identities: [] }
    ]);
    const first = applyEligibilityImport([], snapshot, sequentialIds("first"));
    const retry = applyEligibilityImport(first.participants, snapshot, sequentialIds("retry"));

    expect(first.added).toHaveLength(2);
    expect(first.added[0]).toMatchObject({
      name: "Mika",
      lotteryGroupId: `interaction-profile:${profileA}`,
      identity: { platform: "youtube", accountId: "UC-a" },
      source: { roundId, snapshotId, audienceProfileId: profileA },
      isWinner: false
    });
    expect(first.added[1].name).toBe("觀眾 bbbbbbbb");
    expect(retry.added).toEqual([]);
    expect(retry.participants).toEqual(first.participants);
    expect(retry.summary).toMatchObject({ eligible: 2, added: 0, duplicates: 2 });
  });

  it("treats a profile already imported from another round as a duplicate and keeps its original provenance", () => {
    const fromEarlierRound = applyEligibilityImport([], importedSnapshot([{ audienceProfileId: profileA, displayName: "Mika", identities: [] }], {
      roundId: "11111111-0000-4000-8000-000000000001",
      snapshotId: "11111111-0000-4000-8000-000000000002"
    }), sequentialIds()).participants;
    const result = applyEligibilityImport(fromEarlierRound, importedSnapshot([{ audienceProfileId: profileA, displayName: "Mika renamed", identities: [] }]), sequentialIds("x"));

    expect(result.added).toEqual([]);
    expect(result.participants).toHaveLength(1);
    expect(result.participants[0].source?.roundId).toBe("11111111-0000-4000-8000-000000000001");
    expect(result.participants[0].name).toBe("Mika");
  });

  it("links a manual participant only through an exact platform + channel scope + account identity without adding tickets", () => {
    const identity = { platform: "youtube", channelScope: "channel-a", accountId: "UC-ren" };
    const memberTickets: Participant[] = [
      { id: "ticket-1", lotteryGroupId: "member-group", name: "Ren", identity, level: "Gold", group: "YT會員", tags: [], isWinner: false },
      { id: "ticket-2", lotteryGroupId: "member-group", name: "Ren", identity, level: "Gold", group: "YT會員", tags: [], isWinner: false }
    ];
    const result = applyEligibilityImport(memberTickets, importedSnapshot([
      { audienceProfileId: profileA, displayName: "Ren (new name)", identities: [{ ...identity }] }
    ]), sequentialIds());

    expect(result.added).toEqual([]);
    expect(result.summary).toMatchObject({ linked: 1, added: 0 });
    expect(result.participants.map(participant => participant.id)).toEqual(["ticket-1", "ticket-2"]);
    expect(result.participants.every(participant => participant.lotteryGroupId === "member-group" && participant.name === "Ren")).toBe(true);
    expect(result.participants.every(participant => participant.source?.audienceProfileId === profileA)).toBe(true);
    expect(memberTickets[0].source).toBeUndefined();
  });

  it("never auto-links the same account ID with a different or missing channel scope; reports an identity conflict", () => {
    const existing = [
      manual("Different scope", { platform: "youtube", channelScope: "channel-b", accountId: "UC-ren" }, "manual-scope-b"),
      manual("No scope", { platform: "youtube", accountId: "UC-ren" }, "manual-no-scope")
    ];
    const snapshot = importedSnapshot([
      { audienceProfileId: profileA, displayName: "Ren", identities: [{ platform: "youtube", channelScope: "channel-a", accountId: "UC-ren" }] }
    ]);
    const result = applyEligibilityImport(existing, snapshot, sequentialIds("new"));

    expect(result.classifications[0]).toMatchObject({ kind: "identity-conflict", participantIds: [], identityConflictIds: ["manual-scope-b", "manual-no-scope"] });
    expect(result.summary).toMatchObject({ linked: 0, identityConflicts: 1, added: 1 });
    expect(result.participants.slice(0, 2)).toEqual(existing);
    expect(result.participants.slice(0, 2).every(participant => participant.source === undefined)).toBe(true);
    expect(result.added[0]).toMatchObject({
      lotteryGroupId: `interaction-profile:${profileA}`,
      identity: { platform: "youtube", channelScope: "channel-a", accountId: "UC-ren" },
      source: { audienceProfileId: profileA }
    });
  });

  it("does not link an older entry identity without channel scope even when the manual identity has one", () => {
    const existing = [manual("Ren", { platform: "youtube", channelScope: "channel-a", accountId: "UC-ren" }, "manual-scoped")];
    const classifications = classifyEligibility(existing, [
      { audienceProfileId: profileA, displayName: "Ren", identities: [{ platform: "youtube", accountId: "UC-ren" }] }
    ]);

    expect(classifications[0]).toMatchObject({ kind: "identity-conflict", participantIds: [], identityConflictIds: ["manual-scoped"] });
  });

  it("never uses the display name as an identity even when the account ID matches under a different scope", () => {
    const existing = [manual("Ren", { platform: "youtube", channelScope: "channel-b", accountId: "UC-ren" }, "manual-ren")];
    const [classification] = classifyEligibility(existing, [
      { audienceProfileId: profileA, displayName: "Ren", identities: [{ platform: "youtube", channelScope: "channel-a", accountId: "UC-ren" }] }
    ]);

    expect(classification.kind).toBe("identity-conflict");
    expect(classification.participantIds).toEqual([]);
    expect(classification.nameCollisionIds).toEqual(["manual-ren"]);
  });

  it("keeps same-name participants without an identity link distinct and reports the collision", () => {
    const existing = [manual("Mika"), manual("Mika", { platform: "twitch", accountId: "tw-other" }, "manual-twitch")];
    const snapshot = importedSnapshot([
      { audienceProfileId: profileA, displayName: "mika", identities: [{ platform: "youtube", accountId: "UC-a" }] },
      { audienceProfileId: profileB, displayName: "Mika", identities: [] }
    ]);
    const classifications = classifyEligibility(existing, snapshot.entries);
    const result = applyEligibilityImport(existing, snapshot, sequentialIds());

    expect(classifications.map(item => item.kind)).toEqual(["new", "new"]);
    expect(classifications[0].nameCollisionIds).toEqual(["manual-Mika", "manual-twitch"]);
    expect(classifications[1].nameCollisionIds).toContain(`profile:${profileA}`);
    expect(result.participants).toHaveLength(4);
    expect(result.participants.slice(0, 2)).toEqual(existing);
    expect(new Set(result.added.map(participant => participant.lotteryGroupId)).size).toBe(2);
    expect(result.summary).toMatchObject({ added: 2, nameCollisions: 2, linked: 0 });
  });

  it("does not let two profiles claim the same manual identity", () => {
    const shared = { platform: "youtube", channelScope: "channel-a", accountId: "UC-shared" };
    const existing = [manual("Shared", shared)];
    const classifications = classifyEligibility(existing, [
      { audienceProfileId: profileA, displayName: "A", identities: [{ ...shared }] },
      { audienceProfileId: profileC, displayName: "C", identities: [{ ...shared }] }
    ]);

    expect(classifications.map(item => item.kind)).toEqual(["linked", "identity-conflict"]);
    expect(classifications[1].identityConflictIds).toEqual(["manual-Shared"]);
    expect(summarizeEligibility(classifications)).toMatchObject({ linked: 1, identityConflicts: 1, added: 1 });
  });

  it("refuses to apply a preview that has no persisted snapshot", () => {
    const existing = [manual("Mika")];
    const preview = importedSnapshot([{ audienceProfileId: profileA, displayName: "A", identities: [] }], { imported: false, snapshotId: null });

    expect(() => applyEligibilityImport(existing, preview)).toThrow("名單保持不變");
    expect(existing).toHaveLength(1);
  });

  it("an empty snapshot changes nothing", () => {
    const existing = [manual("Mika")];
    const result = applyEligibilityImport(existing, importedSnapshot([]));

    expect(result.participants).toEqual(existing);
    expect(result.summary).toEqual({ eligible: 0, added: 0, duplicates: 0, linked: 0, identityConflicts: 0, nameCollisions: 0 });
  });

  it("later merge or unlink changes only future previews; already imported provenance stays", () => {
    const imported = applyEligibilityImport([], importedSnapshot([
      { audienceProfileId: profileA, displayName: "A", identities: [{ platform: "youtube", accountId: "UC-a" }] }
    ]), sequentialIds()).participants;
    // After a merge, the API reports the identity under the surviving profile B.
    const afterMerge = classifyEligibility(imported, [{ audienceProfileId: profileB, displayName: "B", identities: [{ platform: "youtube", accountId: "UC-a" }] }]);

    expect(imported[0].source?.audienceProfileId).toBe(profileA);
    // Imported rows are never re-linked through identity; B is flagged as a conflict for host review.
    expect(afterMerge[0].kind).toBe("identity-conflict");
    expect(afterMerge[0].participantIds).toEqual([]);
  });
});
