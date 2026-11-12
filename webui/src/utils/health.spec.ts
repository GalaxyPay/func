import { describe, expect, it } from "vitest";
import type { NodeHealth } from "@/types";
import {
  average,
  formatBytes,
  formatDuration,
  formatMs,
  healthChecks,
  total,
} from "./health";
import { nodeHealth, point } from "./health.fixtures";

describe("formatting", () => {
  it("shows short times in ms and longer ones in seconds", () => {
    expect(formatMs(71.4)).toBe("71 ms");
    expect(formatMs(999.4)).toBe("999 ms");
    expect(formatMs(2770)).toBe("2.77 s");
  });

  it("scales bytes", () => {
    expect(formatBytes(0)).toBe("0 B");
    expect(formatBytes(999)).toBe("999 B");
    expect(formatBytes(1500)).toBe("1.5 KB");
    expect(formatBytes(350_000)).toBe("350 KB");
    expect(formatBytes(5_707_237_983)).toBe("5.7 GB");
  });

  it("shows durations in their two largest units", () => {
    expect(formatDuration(30)).toBe("30s");
    expect(formatDuration(42 * 60 + 10)).toBe("42m");
    expect(formatDuration(7919)).toBe("2h 11m");
    expect(formatDuration(3 * 86400 + 4 * 3600 + 59)).toBe("3d 4h");
    expect(formatDuration(2 * 86400)).toBe("2d");
    expect(formatDuration(-5)).toBe("0s");
  });
});

describe("totals", () => {
  const points = [
    point({ rounds: 10, roundMs: 2000, votes: 3 }),
    point({ rounds: 30, roundMs: 3000, votes: 4 }),
    point(),
  ];

  it("adds a count across points", () => {
    expect(total(points, "votes")).toBe(7);
  });

  it("weights an average by how much each point measured", () => {
    expect(average(points, "roundMs", "rounds")).toBe(2750);
    expect(average([point()], "arrivalMs", "blocks")).toBeUndefined();
  });
});

describe("healthChecks", () => {
  const now = new Date("2026-11-11T23:30:00Z");
  const check = (health: NodeHealth, title: string) =>
    healthChecks(health, now).find((c) => c.title === title);

  it("reports a healthy node", () => {
    const health = nodeHealth({
      lastRound: 66867307,
      lastRoundAt: "2026-11-11T23:29:57Z",
      peersIn: 0,
      peersOut: 4,
      points: [point({ votes: 57 })],
    });

    expect(healthChecks(health, now).map((c) => c.color)).toEqual([
      "success",
      "success",
      "success",
      "success",
    ]);
    expect(check(health, "Blocks")?.detail).toBe(
      `Round ${(66867307).toLocaleString()}, keeping up`
    );
    expect(check(health, "Peers")?.detail).toBe("Connected to 4 peers");
    expect(check(health, "Consensus")?.detail).toBe("57 votes sent");
  });

  it.each([
    ["stale", "warning"],
    ["noEvents", "warning"],
    ["noLog", "error"],
  ] as const)("flags a %s log", (source, color) => {
    expect(check(nodeHealth({ source }), "Node log")?.color).toBe(color);
  });

  it("warns when the last block is old", () => {
    const health = nodeHealth({
      lastRound: 100,
      lastRoundAt: "2026-11-11T23:03:00Z",
    });

    expect(check(health, "Blocks")).toMatchObject({
      color: "warning",
      detail: "Round 100 was 27m ago",
    });
  });

  it("flags a node with no peers", () => {
    const health = nodeHealth({ peersIn: 0, peersOut: 0 });

    expect(check(health, "Peers")).toMatchObject({
      color: "error",
      detail: "Not connected to any peers",
    });
  });

  it("stays neutral about what it doesn't know", () => {
    const health = nodeHealth();

    // Not voting is normal for a node without participation keys.
    expect(check(health, "Blocks")?.color).toBe("icon");
    expect(check(health, "Peers")?.color).toBe("icon");
    expect(check(health, "Consensus")?.color).toBe("icon");
  });
});
