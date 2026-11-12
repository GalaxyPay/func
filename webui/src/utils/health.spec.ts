import { describe, expect, it } from "vitest";
import { formatDuration, healthStatus, total, votesPerRound } from "./health";
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

describe("healthStatus", () => {
  const now = new Date("2026-11-11T23:30:00Z");
  const minutesAgo = (m: number) => new Date(now.getTime() - m * 60000).toISOString();
  const large = { stake: 1e14, online: 2e15 };
  const small = { stake: 1e9, online: 2e15 };
  const healthy = nodeHealth({
    peersOut: 4,
    peersIn: 0,
    startedAt: minutesAgo(600),
    lastVoteAt: minutesAgo(0),
  });

  it("waits for data", () => {
    expect(healthStatus(undefined, now)).toMatchObject({ color: "icon" });
  });

  it("reports a healthy voting node", () => {
    expect(healthStatus(healthy, now, large)).toEqual({
      color: "success",
      text: "Healthy",
      detail: "Voting, 4 peers",
    });
  });

  it("doesn't mention voting without online accounts", () => {
    expect(healthStatus(healthy, now)).toMatchObject({ color: "success", detail: "4 peers" });
    expect(healthStatus(nodeHealth(), now).detail).toBeUndefined();
  });

  it("explains a log FUNC can't read", () => {
    const status = healthStatus({ ...healthy, source: "stale" }, now, large);
    expect(status).toMatchObject({ color: "warning", text: "Can't read the node's log" });
    expect(status.hint).toContain("BaseLoggerDebugLevel");
    expect(healthStatus({ ...healthy, source: "noEvents" }, now).hint).toContain("TelemetryToLog");
    expect(healthStatus({ ...healthy, source: "noLog" }, now).hint).toContain("node.log");
  });

  it("flags a node with no peers", () => {
    const status = healthStatus({ ...healthy, peersOut: 0 }, now, large);
    expect(status).toMatchObject({ color: "error", text: "Not connected to the network" });
  });

  it("warns about too few peers", () => {
    const status = healthStatus({ ...healthy, peersOut: 2 }, now);
    expect(status).toMatchObject({ color: "warning", text: "Only 2 peers" });
  });

  it("flags online accounts that stopped voting", () => {
    const status = healthStatus({ ...healthy, lastVoteAt: minutesAgo(20) }, now, large);
    expect(status).toMatchObject({ color: "error", text: "Not voting" });
    expect(status.hint).toContain("hasn't voted for 20m");
  });

  it("gives new keys and a restarted node time to start voting", () => {
    expect(healthStatus({ ...healthy, lastVoteAt: minutesAgo(14) }, now, large).color).toBe("success");
    const restarted = { ...healthy, lastVoteAt: minutesAgo(60), startedAt: minutesAgo(10) };
    expect(healthStatus(restarted, now, large).color).toBe("success");
    const neverVoted = { ...healthy, lastVoteAt: undefined, startedAt: minutesAgo(20) };
    expect(healthStatus(neverVoted, now, large).text).toBe("Not voting");
  });

  it("waits longer for a small account picked less often", () => {
    // About 20 votes take 9,000 rounds, or 7 hours.
    expect(healthStatus({ ...healthy, lastVoteAt: minutesAgo(60) }, now, small).color).toBe("success");
    expect(healthStatus({ ...healthy, lastVoteAt: minutesAgo(8 * 60) }, now, small).text).toBe("Not voting");
  });

  it("counts votes in the history when the last one wasn't recorded", () => {
    const t = (m: number) => Date.parse(minutesAgo(m)) / 1000;
    const older = { ...healthy, lastVoteAt: undefined, startedAt: minutesAgo(600) };
    const recent = { ...older, points: [point({ t: t(30), votes: 5 }), point({ t: t(10), votes: 9 }), point({ t: t(0) })] };
    expect(healthStatus(recent, now, large).color).toBe("success");
    const stopped = { ...older, points: [point({ t: t(30), votes: 5 }), point({ t: t(10) })] };
    expect(healthStatus(stopped, now, large).text).toBe("Not voting");
  });

  it("doesn't judge voting without knowing when the node started", () => {
    const unknown = { ...healthy, lastVoteAt: undefined, startedAt: undefined };
    expect(healthStatus(unknown, now, large).color).toBe("success");
  });
});
