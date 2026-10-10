import { describe, expect, it } from "vitest";
import {
  buildRouletteTestUrl,
  cloneRouletteTemplate,
  createRouletteTemplate,
  resizeRouletteSegments
} from "./template";

describe("Roulette template editor model", () => {
  it("creates the Blazor editor defaults and eight ordered segments", () => {
    const template = createRouletteTemplate();
    expect(template).toMatchObject({ id: 0, segmentCount: 8, spinDurationSec: 5, resultDisplayDurationSeconds: 12, easingFunction: "ease-out-cubic", publicationStatus: 1 });
    expect(template.segments).toHaveLength(8);
    expect(template.segments.map(segment => segment.index)).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
    expect(template.segments[0]).toMatchObject({ title: "選項 1", color: "#e74c3c", probability: 0 });
  });

  it("deep clones segments so canceling edits cannot mutate the source", () => {
    const source = createRouletteTemplate();
    source.id = 17;
    source.segments[0].id = 23;
    const copy = cloneRouletteTemplate(source);
    expect(copy).toEqual(source);
    expect(copy.segments[0]).not.toBe(source.segments[0]);
    copy.segments[0].title = "暫存修改";
    expect(source.segments[0].title).toBe("選項 1");
  });

  it("keeps existing segment data and index order while shrinking or filling", () => {
    const template = createRouletteTemplate();
    template.segments[2].id = 42;
    template.segments[2].title = "保留的項目";
    template.segments.reverse();

    const smaller = resizeRouletteSegments({ ...template, segmentCount: 6 });
    expect(smaller.segments).toHaveLength(6);
    expect(smaller.segments.map(segment => segment.index)).toEqual([0, 1, 2, 3, 4, 5]);
    expect(smaller.segments.find(segment => segment.id === 42)?.title).toBe("保留的項目");

    const larger = resizeRouletteSegments({ ...smaller, segmentCount: 12 });
    expect(larger.segments).toHaveLength(12);
    expect(larger.segments[11]).toMatchObject({ id: 0, index: 11, title: "選項 12", color: "#2ecc71" });
  });

  it("keeps the test OBS token in the fragment, not the query", () => {
    const url = new URL(buildRouletteTestUrl("https://example.test", "public-id", "secret token"));
    expect(url.pathname).toBe("/obs/roulette/public-id");
    expect(url.search).toBe("?controls=1");
    expect(url.hash).toBe("#sessionToken=secret+token");
  });
});
