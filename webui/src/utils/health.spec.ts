import { describe, expect, it } from "vitest";
import {
  consensusLight,
  formatDuration,
  networkLight,
  nodeLight,
  recentErrors,
  total,
  votesPerRound,
} from "./health";
import type { ConsensusState } from "./health";
import { nodeHealth, point } from "./health.fixtures";

describe("formatDuration", () => {
  it("shows durations in their two largest units", () => {
    expect(formatDuration(30)).toBe("30s");
    expect(formatDuration(42 * 60 + 10)).toBe("42m");
    expect(formatDuration(7919)).toBe("2h 11m");
    expect(formatDuration(3 * 86400 + 4 * 3600 + 59)).toBe("3d 4h");
    expect(formatDuration(2 * 86400)).toBe("2d");
    expect(formatDuration(-5)).toBe("0s");
  });
});

describe("total", () => {
  it("adds a count across points", () => {
    expect(total([point({ votes: 3 }), point({ votes: 4 }), point()], "votes")).toBe(7);
  });
});

describe("recentErrors", () => {
  it("counts each repeat of an error from the last day", () => {
    const now = new Date("2026-11-11T23:30:00Z");
    const note = (time: string, count: number, level: "error" | "warning" = "error") => ({ time, level, text: "x", count });
    const health = nodeHealth({
      notes: [
        note("2026-11-11T23:00:00Z", 6),
        note("2026-11-11T01:00:00Z", 1),
        note("2026-11-11T22:00:00Z", 4, "warning"),
        note("2026-11-10T23:00:00Z", 9),
      ],
    });
    expect(recentErrors(health, now)).toBe(7);
    expect(recentErrors(nodeHealth(), now)).toBe(0);
  });
});

describe("votesPerRound", () => {
  it("is near one per committee for a large account", () => {
    expect(votesPerRound({ stake: 1e14, online: 2e15 })).toBeCloseTo(2, 5);
  });

  it("is rare for a small account", () => {
    // 1,000 ALGO of 2 billion online.
    expect(votesPerRound({ stake: 1e9, online: 2e15 })).toBeCloseTo(0.00224, 4);
  });

  it("is zero without stake", () => {
    expect(votesPerRound({ stake: 0, online: 2e15 })).toBe(0);
    expect(votesPerRound({ stake: 1e9, online: 0 })).toBe(0);
  });
});

describe("nodeLight", () => {
  const node = { serviceStatus: "Running", lastRound: 100n, syncing: false, generatingKey: false };

  it("reports a running, synced node", () => {
    expect(nodeLight(node)).toMatchObject({ color: "success", text: "Running, synced" });
    expect(nodeLight(node).hint).toBeUndefined();
  });

  it("explains a node that isn't running", () => {
    expect(nodeLight({ ...node, serviceStatus: "Unknown" }).color).toBe("icon");
    expect(nodeLight({ ...node, serviceStatus: "Not Found" }).text).toBe("Not installed");
    expect(nodeLight({ ...node, serviceStatus: "Stopped" })).toEqual({ color: "error", text: "Stopped" });
  });

  it("says whether a stopped node comes back after a reboot", () => {
    const stopped = { ...node, serviceStatus: "Stopped" };
    expect(nodeLight({ ...stopped, startsAtBoot: true }).hint).toBe(
      "It will start again by itself when this computer restarts. Start it now from Manage."
    );
    expect(nodeLight({ ...stopped, startsAtBoot: false }).hint).toContain("won't start by itself");
  });

  it("warns that a running node without a service won't survive a reboot", () => {
    expect(nodeLight({ ...node, startsAtBoot: false })).toMatchObject({ color: "success", hint: expect.stringContaining("won't start") });
    expect(nodeLight({ ...node, startsAtBoot: true }).hint).toBeUndefined();
  });

  it("shows catching up as in progress", () => {
    expect(nodeLight({ ...node, syncing: true })).toEqual({ color: "warning", text: "Catching up", busy: true });
    // Generating a key slows the node; a lag then is expected.
    expect(nodeLight({ ...node, syncing: true, generatingKey: true }).color).toBe("success");
  });

  it("doesn't call a node synced before algod answers", () => {
    expect(nodeLight({ ...node, lastRound: undefined })).toMatchObject({ color: "icon", text: "Connecting...", busy: true });
  });

  it("shows a new node at block 0 as starting, not failed", () => {
    expect(nodeLight({ ...node, lastRound: 0n })).toEqual({ color: "warning", text: "Starting sync", busy: true });
  });
});

describe("consensusLight", () => {
  const now = new Date("2026-11-11T23:30:00Z");
  const minutesAgo = (m: number) => new Date(now.getTime() - m * 60000).toISOString();
  const large = { stake: 1e14, online: 2e15 };
  const small = { stake: 1e9, online: 2e15 };
  const health = nodeHealth({ startedAt: minutesAgo(600), lastVoteAt: minutesAgo(0) });
  const state: ConsensusState = {
    running: true,
    synced: true,
    generatingKey: false,
    onlineAccounts: 2,
    stake: large,
    health,
  };
  const light = (values: Partial<ConsensusState>) => consensusLight({ ...state, ...values }, now);
  const withHealth = (values: Partial<typeof health>) => light({ health: { ...health, ...values } });

  it("reports accounts that are voting", () => {
    expect(light({})).toEqual({ color: "success", text: "2 accounts voting" });
    expect(light({ onlineAccounts: 1 }).text).toBe("1 account voting");
  });

  it("says online, not voting, until a vote is seen", () => {
    expect(light({ health: undefined }).text).toBe("2 accounts online");
    // A small account goes long stretches between votes without anything wrong.
    const quiet = { ...health, lastVoteAt: minutesAgo(60) };
    expect(light({ stake: small, health: quiet })).toEqual({ color: "success", text: "2 accounts online" });
  });

  it("explains why the node can't take part", () => {
    expect(light({ running: false }).color).toBe("error");
    // Not alarming: syncing is a normal stage, and the Node light shows it.
    expect(light({ synced: false })).toEqual({ color: "icon", text: "Waiting for sync" });
    expect(light({ generatingKey: true, synced: false }).text).toBe("Generating key...");
    expect(light({ onlineAccounts: undefined }).color).toBe("icon");
    expect(light({ onlineAccounts: 0 })).toMatchObject({ color: "error", text: "No keys online" });
  });

  it("explains a log FUNC can't read", () => {
    const status = withHealth({ source: "stale" });
    expect(status).toMatchObject({ color: "warning", text: "Can't check voting" });
    expect(status.hint).toContain("BaseLoggerDebugLevel");
    expect(withHealth({ source: "noEvents" }).hint).toContain("TelemetryToLog");
    expect(withHealth({ source: "noLog" }).hint).toContain("node.log");
  });

  it("flags online accounts that stopped voting", () => {
    const status = withHealth({ lastVoteAt: minutesAgo(20) });
    expect(status).toMatchObject({ color: "error", text: "Not voting" });
    expect(status.hint).toContain("hasn't voted for 20m");
  });

  it("gives new keys and a restarted node time to start voting", () => {
    expect(withHealth({ lastVoteAt: minutesAgo(60), startedAt: minutesAgo(10) }).color).toBe("success");
    expect(withHealth({ lastVoteAt: undefined, startedAt: minutesAgo(20) }).text).toBe("Not voting");
  });

  it("waits longer for a small account picked less often", () => {
    // About 20 votes take 9,000 rounds, or 7 hours.
    expect(consensusLight({ ...state, stake: small, health: { ...health, lastVoteAt: minutesAgo(60) } }, now).color).toBe("success");
    expect(consensusLight({ ...state, stake: small, health: { ...health, lastVoteAt: minutesAgo(8 * 60) } }, now).text).toBe("Not voting");
  });

  it("counts votes in the history when the last one wasn't recorded", () => {
    const t = (m: number) => Date.parse(minutesAgo(m)) / 1000;
    const older = { lastVoteAt: undefined, startedAt: minutesAgo(600) };
    expect(withHealth({ ...older, points: [point({ t: t(30), votes: 5 }), point({ t: t(10), votes: 9 })] }).color).toBe("success");
    expect(withHealth({ ...older, points: [point({ t: t(30), votes: 5 }), point({ t: t(10) })] }).text).toBe("Not voting");
  });

  it("doesn't judge voting without knowing when the node started", () => {
    expect(withHealth({ lastVoteAt: undefined, startedAt: undefined }).color).toBe("success");
    expect(light({ stake: undefined, health: { ...health, lastVoteAt: minutesAgo(600) } }).color).toBe("success");
  });
});

describe("networkLight", () => {
  const now = new Date("2026-11-11T23:30:00Z");
  const settled = { startedAt: "2026-11-11T20:00:00Z" };
  const light = (values: Parameters<typeof nodeHealth>[0], settling = false) =>
    networkLight(nodeHealth({ ...settled, ...values }), now, settling);

  it("counts peers", () => {
    expect(light({ peersOut: 4, peersIn: 2 })).toEqual({ color: "success", text: "6 peers" });
  });

  it("waits until peers are reported", () => {
    expect(networkLight(undefined, now).color).toBe("icon");
    expect(light({}).color).toBe("icon");
  });

  it("flags no or few peers", () => {
    expect(light({ peersOut: 0 })).toMatchObject({ color: "error", text: "No peers" });
    expect(light({ peersOut: 2 })).toMatchObject({ color: "warning", text: "Only 2 peers" });
  });

  it("doesn't worry anyone while the node starts or syncs", () => {
    expect(light({ peersOut: 3 }, true)).toEqual({ color: "icon", text: "3 peers" });
    expect(light({ peersOut: 0 }, true)).toMatchObject({ color: "icon", text: "Connecting..." });
    expect(networkLight(undefined, now, true).text).toBe("Connecting...");
    const justStarted = { startedAt: "2026-11-11T23:25:00Z", peersOut: 2 };
    expect(networkLight(nodeHealth(justStarted), now)).toEqual({ color: "icon", text: "2 peers" });
  });
});
