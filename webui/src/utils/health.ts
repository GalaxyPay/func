import type { HealthPoint, NodeHealth } from "@/types";

export function formatMs(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

export function formatBytes(bytes: number): string {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1000 && unit < units.length - 1) {
    value /= 1000;
    unit++;
  }
  return `${unit && value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}

// A length of time in its two largest units: "3d 4h", "5h 12m", "42m", "30s".
export function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  if (d) return h ? `${d}d ${h}h` : `${d}d`;
  if (h) return m ? `${h}h ${m}m` : `${h}h`;
  if (m) return `${m}m`;
  return `${s}s`;
}

export function total(points: HealthPoint[], key: keyof HealthPoint): number {
  return points.reduce((sum, p) => sum + (p[key] ?? 0), 0);
}

// The average of a per-point average, weighting each point by how many
// measurements went into it.
export function average(
  points: HealthPoint[],
  key: "roundMs" | "arrivalMs",
  weight: "rounds" | "blocks"
): number | undefined {
  let sum = 0;
  let count = 0;
  for (const p of points) {
    const value = p[key];
    if (value == null || !p[weight]) continue;
    sum += value * p[weight];
    count += p[weight];
  }
  return count ? sum / count : undefined;
}

export interface HealthCheck {
  title: string;
  // A theme color for the status dot: success, warning, error or icon (unknown).
  color: "success" | "warning" | "error" | "icon";
  detail: string;
}

// The headline answers: is FUNC getting data, is the node keeping up with the
// chain, is it connected, and is it taking part in consensus.
export function healthChecks(health: NodeHealth, now: Date): HealthCheck[] {
  const checks: HealthCheck[] = [];

  const source: Record<NodeHealth["source"], HealthCheck> = {
    ok: { title: "Node log", color: "success", detail: "Reading new entries" },
    stale: {
      title: "Node log",
      color: "warning",
      detail: "Nothing new is being written",
    },
    noEvents: {
      title: "Node log",
      color: "warning",
      detail: "No telemetry events in the log",
    },
    noLog: { title: "Node log", color: "error", detail: "No node.log found" },
  };
  checks.push(source[health.source]);

  if (!health.lastRoundAt) {
    checks.push({ title: "Blocks", color: "icon", detail: "No rounds seen yet" });
  } else {
    const age = (now.getTime() - new Date(health.lastRoundAt).getTime()) / 1000;
    const round = `Round ${health.lastRound.toLocaleString()}`;
    checks.push(
      age < 30
        ? { title: "Blocks", color: "success", detail: `${round}, keeping up` }
        : {
            title: "Blocks",
            color: "warning",
            detail: `${round} was ${formatDuration(age)} ago`,
          }
    );
  }

  if (health.peersIn == null && health.peersOut == null) {
    checks.push({ title: "Peers", color: "icon", detail: "Not reported yet" });
  } else {
    const peers = (health.peersIn ?? 0) + (health.peersOut ?? 0);
    checks.push({
      title: "Peers",
      color: peers ? "success" : "error",
      detail: peers
        ? `Connected to ${peers} ${peers === 1 ? "peer" : "peers"}`
        : "Not connected to any peers",
    });
  }

  const votes = total(health.points, "votes");
  checks.push(
    votes
      ? {
          title: "Consensus",
          color: "success",
          detail: `${votes.toLocaleString()} ${votes === 1 ? "vote" : "votes"} sent`,
        }
      : { title: "Consensus", color: "icon", detail: "No votes sent in this period" }
  );

  return checks;
}
