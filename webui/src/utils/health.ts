import type { HealthPoint, NodeHealth } from "@/types";

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

// What the Health row on the dashboard says. It only turns amber or red for
// something the user can do something about, and then says what.
export interface HealthStatus {
  // A theme color for the status dot.
  color: "success" | "warning" | "error" | "icon";
  text: string;
  detail?: string;
  hint?: string;
}

// The node's online accounts, in microalgos, against all online stake.
export interface Stake {
  stake: number;
  online: number;
}

// Committee sizes for the soft and cert votes each round. An account is picked
// for each by lottery, weighted by its share of the online stake.
const committees = [2990, 1500];
const blockMs = 2800;
// Silence that can't be the result of the lottery: a run with this many votes
// expected and none sent is a broken node, not bad luck.
const missingVotes = 20;
// New keys only start voting about 320 rounds after they go online.
const quietMs = 15 * 60 * 1000;
export const fewPeers = 4;

// How many votes the node's accounts can expect to send per round.
export function votesPerRound(stake: Stake): number {
  if (!stake.stake || !stake.online) return 0;
  const share = Math.min(1, stake.stake / stake.online);
  return committees.reduce((sum, size) => sum + 1 - Math.exp(-size * share), 0);
}

function peerCount(health: NodeHealth): number | undefined {
  if (health.peersIn == null && health.peersOut == null) return undefined;
  return (health.peersIn ?? 0) + (health.peersOut ?? 0);
}

const plural = (n: number, noun: string) =>
  `${n.toLocaleString()} ${noun}${n === 1 ? "" : "s"}`;

// health: what FUNC has gathered for a running node. stake: its online accounts,
// once known, and only while the node is synced, since it can't vote before.
export function healthStatus(
  health: NodeHealth | undefined,
  now: Date,
  stake?: Stake
): HealthStatus {
  if (!health) return { color: "icon", text: "Checking..." };

  const unreadable: Record<string, string> = {
    noLog:
      "FUNC found no node.log in the node's data directory. Check that the node's LogSizeLimit setting isn't 0.",
    noEvents:
      "The node's log has no telemetry events in it. Check that TelemetryToLog isn't set to false in the node's config.json.",
    stale:
      "The node isn't writing to its log. Set BaseLoggerDebugLevel to 4 or higher under Manage > Configure.",
  };
  if (health.source !== "ok")
    return {
      color: "warning",
      text: "Can't read the node's log",
      hint: unreadable[health.source],
    };

  const peers = peerCount(health);
  if (peers === 0)
    return {
      color: "error",
      text: "Not connected to the network",
      hint: "The node has no peers. Check this computer's internet connection and firewall.",
    };

  const perRound = stake ? votesPerRound(stake) : 0;
  if (perRound) {
    // Measured from the last vote, or from when the node started if later. The
    // start of the latest stretch with votes stands in for a last vote FUNC
    // didn't record, as after updating from a version that didn't.
    const voted = health.points.filter((p) => p.votes).map((p) => p.t * 1000);
    const since = Math.max(
      ...[health.lastVoteAt, health.startedAt].map((t) =>
        t ? new Date(t).getTime() : 0
      ),
      ...voted
    );
    const silentMs = now.getTime() - since;
    if (since && silentMs > quietMs && (silentMs / blockMs) * perRound >= missingVotes)
      return {
        color: "error",
        text: "Not voting",
        hint:
          `The node hasn't voted for ${formatDuration(silentMs / 1000)} although its accounts are online. ` +
          "Try restarting it from Manage. If that doesn't help, look for errors under Manage > View Log.",
      };
  }

  if (peers != null && peers < fewPeers)
    return {
      color: "warning",
      text: `Only ${plural(peers, "peer")}`,
      hint: `Nodes normally keep at least ${fewPeers} connections. Check this computer's internet connection.`,
    };

  const detail = [perRound ? "Voting" : "", peers == null ? "" : plural(peers, "peer")]
    .filter(Boolean)
    .join(", ");
  return { color: "success", text: "Healthy", detail: detail || undefined };
}
