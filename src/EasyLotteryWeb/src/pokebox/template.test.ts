import { describe, expect, it } from "vitest";
import { buildPokeTestUrl, clonePokeTemplate, createPokeTemplate, resizePokeGrid } from "./template";

describe("PokeBox template editor model", () => {
  it("creates the existing draft defaults and a 3 by 3 grid", () => {
    const template = createPokeTemplate();
    expect(template).toMatchObject({
      id: 0,
      gridRows: 3,
      gridColumns: 3,
      mode: 0,
      animation: 0,
      animationDurationMs: 650,
      resultDisplayDurationSeconds: 8,
      overlayWidth: 1920,
      overlayHeight: 1080,
      publicationStatus: 1
    });
    expect(template.cells).toHaveLength(9);
    expect(template.cells[0]).toMatchObject({ index: 0, title: "格子 1", revealedColor: "#cccccc", isRevealed: false, revealedAt: null });
  });

  it("deep clones cells so canceling edits cannot mutate the list template", () => {
    const source = createPokeTemplate();
    source.id = 12;
    source.cells[0].id = 55;
    source.cells[0].isRevealed = true;
    source.cells[0].revealedAt = "2026-10-10T00:00:00Z";
    const copy = clonePokeTemplate(source);
    expect(copy).toEqual(source);
    expect(copy.cells[0]).not.toBe(source.cells[0]);
    copy.cells[0].title = "暫存修改";
    expect(source.cells[0].title).toBe("格子 1");
  });

  it("sorts and preserves existing cells when resizing, then fills with defaults", () => {
    const template = createPokeTemplate();
    template.cells[2].id = 23;
    template.cells[2].title = "保留格子";
    template.cells.reverse();

    const smaller = resizePokeGrid({ ...template, gridRows: 2, gridColumns: 2 });
    expect(smaller.cells).toHaveLength(4);
    expect(smaller.cells.map(cell => cell.index)).toEqual([0, 1, 2, 3]);
    expect(smaller.cells.find(cell => cell.id === 23)?.title).toBe("保留格子");

    const larger = resizePokeGrid({ ...smaller, gridRows: 4, gridColumns: 4 });
    expect(larger.cells).toHaveLength(16);
    expect(larger.cells[15]).toMatchObject({ id: 0, index: 15, title: "格子 16", revealedColor: "#cccccc" });
  });

  it("clamps grid dimensions to the existing 1 to 10 range", () => {
    expect(resizePokeGrid({ ...createPokeTemplate(), gridRows: 0, gridColumns: 11 })).toMatchObject({ gridRows: 1, gridColumns: 10 });
  });

  it("keeps the test OBS token in the fragment, not the query", () => {
    const url = new URL(buildPokeTestUrl("https://example.test", "public-id", "secret token"));
    expect(url.pathname).toBe("/obs/pokebox/public-id");
    expect(url.search).toBe("?controls=1");
    expect(url.hash).toBe("#sessionToken=secret+token");
  });
});
