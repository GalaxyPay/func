import { describe, expect, it } from "vitest";
import { barRects, domain, linePaths, lineY, slotAt, slotX } from "./sparkline";

describe("domain", () => {
  it("starts bars at zero", () => {
    expect(domain([3, null, 8], true)).toEqual([0, 8]);
    expect(domain([0, 0], true)).toEqual([0, 1]);
  });

  it("gives a line room around its own range", () => {
    expect(domain([2700, 2800, null], false)).toEqual([2690, 2810]);
  });

  it("never goes below zero or collapses to nothing", () => {
    expect(domain([0, 10], false)).toEqual([0, 11]);
    expect(domain([4, 4, 4], false)).toEqual([3.6, 4.4]);
    expect(domain([0, 0], false)).toEqual([0, 1]);
    expect(domain([null, undefined], false)).toEqual([0, 1]);
    expect(domain([], true)).toEqual([0, 1]);
  });
});

describe("slots", () => {
  it("centers each interval in its share of the width", () => {
    expect(slotX(0, 4, 200)).toBe(25);
    expect(slotX(3, 4, 200)).toBe(175);
  });

  it("finds the interval under the pointer", () => {
    expect(slotAt(0, 4, 200)).toBe(0);
    expect(slotAt(49, 4, 200)).toBe(0);
    expect(slotAt(50, 4, 200)).toBe(1);
    expect(slotAt(199, 4, 200)).toBe(3);
    // Just outside the chart still counts as its nearest edge.
    expect(slotAt(-5, 4, 200)).toBe(0);
    expect(slotAt(260, 4, 200)).toBe(3);
    expect(slotAt(10, 0, 200)).toBe(0);
  });
});

describe("barRects", () => {
  it("scales bars to the tallest and stands them on the baseline", () => {
    const bars = barRects([5, 10, 0, null], 200, 50);

    expect(bars.map((b) => b.index)).toEqual([0, 1]);
    expect(bars[0]).toMatchObject({ height: 25, y: 25 });
    expect(bars[1]).toMatchObject({ height: 50, y: 0 });
    // 50px slots: capped at 24px wide and centered.
    expect(bars[0]).toMatchObject({ width: 24, x: 13 });
  });

  it("keeps a gap between narrow bars", () => {
    const bars = barRects(Array(100).fill(1), 300, 40);

    expect(bars).toHaveLength(100);
    expect(bars[0].width).toBe(2);
    expect(bars[1].x - bars[0].x).toBe(3);
  });

  it("keeps a small count visible next to a large one", () => {
    const [small] = barRects([1, 5000], 100, 40);

    expect(small.height).toBe(1);
  });
});

describe("linePaths", () => {
  it("joins consecutive values", () => {
    const line = linePaths([0, 10], 100, 44, 0);

    // The domain is 0 to 11, so 10 sits a little below the top.
    expect(line.paths).toEqual(["M25,44L75,4"]);
    expect(line.dots).toEqual([]);
  });

  it("breaks the line where nothing was measured", () => {
    const line = linePaths([1, 2, null, 3, null, 4, 5], 70, 40);

    expect(line.paths).toHaveLength(2);
    expect(line.paths[0]).toMatch(/^M5,[\d.]+L15,[\d.]+$/);
    expect(line.paths[1]).toMatch(/^M55,[\d.]+L65,[\d.]+$/);
    expect(line.dots.map((d) => d.index)).toEqual([3]);
  });

  it("keeps the line clear of the edges by the inset", () => {
    const values = [0, 10];

    expect(lineY(0, values, 50, 5)).toBe(45);
    expect(lineY(11, values, 50, 5)).toBe(5);
    expect(linePaths([null, null], 100, 50, 5)).toEqual({ paths: [], dots: [] });
  });
});
