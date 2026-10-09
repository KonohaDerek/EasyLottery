import { describe, expect, it } from "vitest";
import { filterActivityResults, serializeActivityResultsCsv } from "./results";
import type { ActivityResultRecord } from "./results";

const localDate = (value: string) => {
  const date = new Date(value);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
};

const records: ActivityResultRecord[] = [
  {
    id: 7,
    activityType: 0,
    activityName: "週末戳戳樂",
    activityDateUtc: "2026-10-08T12:00:00Z",
    summary: "已揭曉 2 格",
    items: [{ order: 1, name: "特獎", description: "第一格", imageUrl: "", color: "", resultedAtUtc: null }]
  },
  {
    id: 8,
    activityType: 1,
    activityName: "直播轉盤",
    activityDateUtc: "2026-10-09T12:00:00Z",
    summary: "中獎項目：旅行券",
    items: [{ order: 1, name: "旅行券", description: "第 2 格", imageUrl: "", color: "", resultedAtUtc: "2026-10-09T12:00:02Z" }]
  },
  {
    id: 6,
    activityType: 0,
    activityName: "舊活動",
    activityDateUtc: "2026-10-01T12:00:00Z",
    summary: "已揭曉 1 格",
    items: []
  }
];

describe("activity result filtering", () => {
  it("combines trimmed case-insensitive search, type, and browser-local date range", () => {
    const matchingDate = localDate(records[0].activityDateUtc);

    expect(filterActivityResults(records, {
      search: "  特獎 ",
      type: "pokebox",
      startDate: matchingDate,
      endDate: matchingDate
    }).map(record => record.id)).toEqual([7]);
  });

  it("searches names, summaries, type names, and item descriptions and sorts newest first", () => {
    expect(filterActivityResults(records, { search: "ROULETTE" }).map(record => record.id)).toEqual([8]);
    expect(filterActivityResults(records).map(record => record.id)).toEqual([8, 7, 6]);
  });

  it("uses record ID as the descending tie-breaker", () => {
    const tied = [records[0], { ...records[2], activityDateUtc: records[0].activityDateUtc }];
    expect(filterActivityResults(tied).map(record => record.id)).toEqual([7, 6]);
  });
});

describe("activity result CSV", () => {
  it("preserves the existing BOM, header, CRLF, item order, and quoted-cell escaping", () => {
    const record: ActivityResultRecord = {
      ...records[0],
      activityName: '活動, "特別場"',
      items: [
        { order: 2, name: "第二名", description: "", imageUrl: "", color: "", resultedAtUtc: null },
        { order: 1, name: '第一名, "幸運"', description: "說明", imageUrl: "", color: "", resultedAtUtc: "2026-10-08T12:01:00Z" }
      ]
    };

    expect(serializeActivityResultsCsv([record])).toBe(
      '\uFEFF活動 ID,活動類型,活動名稱,活動時間 UTC,摘要,結果順序,獎項,說明,結果時間 UTC\r\n' +
      '"7","PokeBox","活動, ""特別場""","2026-10-08T12:00:00.0000000Z","已揭曉 2 格","1","第一名, ""幸運""","說明","2026-10-08T12:01:00.0000000Z"\r\n' +
      '"7","PokeBox","活動, ""特別場""","2026-10-08T12:00:00.0000000Z","已揭曉 2 格","2","第二名","",""\r\n'
    );
  });
});
