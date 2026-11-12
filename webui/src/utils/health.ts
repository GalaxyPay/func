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

// Errors the node logged in the last day, counting each repeat.
export function recentErrors(health: NodeHealth, now: Date): number {
  const since = now.getTime() - 24 * 3600 * 1000;
  return health.notes
    .filter((n) => n.level === "error" && new Date(n.time).getTime() >= since)
    .reduce((sum, n) => sum + n.count, 0);
}

export function total(points: HealthPoint[], key: keyof HealthPoint): number {
  return points.reduce((sum, p) => sum + (p[key] ?? 0), 0);
}

// One light in the dashboard's Status group. It only turns amber or red for
// something the user can do something about, and then says what.
export interface Light {
  // A theme color for the status dot.
  color: "success" | "warning" | "error" | "icon";
  text: string;
  hint?: string;
  // Still on its way to a steady state, such as catching up.
  busy?: boolean;
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

const plural = (n: number, noun: string) =>
  `${n.toLocaleString()} ${noun}${n === 1 ? "" : "s"}`;

const checking: Light = { color: "icon", text: "Checking..." };

export interface NodeState {
  // As FUNC reports the service: Running, Stopped, Not Found or Unknown.
  serviceStatus?: string;
  // The last round algod reported, once it has.
  lastRound?: bigint;
  syncing: boolean;
  // Key generation slows the node down, so a lag behind the chain is expected.
  generatingKey: boolean;
  // Whether the service starts again when the computer boots, if known.
  startsAtBoot?: boolean | null;
}

const atBoot = {
  starts: "It will start again by itself when this computer restarts.",
  doesNot: "It won't start by itself when this computer restarts.",
};

// Node: installed, running and keeping up with the chain.
export function nodeLight(node: NodeState): Light {
  switch (node.serviceStatus) {
    case undefined:
    case "Unknown":
      return checking;
    case "Not Found":
      return { color: "error", text: "Not installed" };
    case "Running":
      break;
    case "Stopped":
      return {
        color: "error",
        text: "Stopped",
        hint:
          node.startsAtBoot == null
            ? undefined
            : `${node.startsAtBoot ? atBoot.starts : atBoot.doesNot} Start it now from Manage.`,
      };
    default:
      return { color: "error", text: node.serviceStatus };
  }
  // algod hasn't answered yet, as just after the node starts.
  if (node.lastRound == null) return { color: "icon", text: "Connecting...", busy: true };
  if (node.syncing && !node.generatingKey)
    return { color: "warning", text: "Catching up", busy: true };
  // A new node sits at block 0 until fast catchup or syncing gets going.
  if (node.lastRound === 0n) return { color: "warning", text: "Starting sync", busy: true };
  return {
    color: "success",
    text: node.syncing ? "Running" : "Running, synced",
    // Worth knowing for a node run without a service, which a reboot leaves stopped.
    hint: node.startsAtBoot === false ? atBoot.doesNot : undefined,
  };
}

export interface ConsensusState {
  running: boolean;
  synced: boolean;
  generatingKey: boolean;
  // Accounts with their keys online, once known.
  onlineAccounts?: number;
  // Their stake against all online stake, once known.
  stake?: Stake;
  health?: NodeHealth;
}

// Consensus: the node has accounts online and is voting for them.
export function consensusLight(state: ConsensusState, now: Date): Light {
  if (!state.running) return { color: "error", text: "Not participating" };
  if (state.generatingKey) return { color: "icon", text: "Generating key...", busy: true };
  if (!state.synced) return { color: "icon", text: "Waiting for sync" };
  const accounts = state.onlineAccounts;
  if (accounts == null) return checking;
  if (!accounts)
    return {
      color: "error",
      text: "No keys online",
      hint: "Register a participation key below to take part in consensus.",
    };

  const online: Light = { color: "success", text: `${plural(accounts, "account")} online` };
  const health = state.health;
  if (!health) return online;
  if (health.source !== "ok")
    return { color: "warning", text: "Can't check voting", hint: unreadable[health.source] };

  const perRound = state.stake ? votesPerRound(state.stake) : 0;
  if (!perRound) return online;
  // Measured from the last vote, or from when the node started if later. The
  // start of the latest stretch with votes stands in for a last vote FUNC
  // didn't record, as after updating from a version that didn't.
  const voted = health.points.filter((p) => p.votes).map((p) => p.t * 1000);
  const since = Math.max(
    ...[health.lastVoteAt, health.startedAt].map((t) => (t ? Date.parse(t) : 0)),
    ...voted
  );
  if (!since) return online;
  const silentMs = now.getTime() - since;
  if (silentMs > quietMs && (silentMs / blockMs) * perRound >= missingVotes)
    return {
      color: "error",
      text: "Not voting",
      hint:
        `The node hasn't voted for ${formatDuration(silentMs / 1000)} although its accounts are online. ` +
        "Try restarting it from Manage. If that doesn't help, look for errors under Manage > View Log.",
    };
  const lastVote = health.lastVoteAt ? Date.parse(health.lastVoteAt) : 0;
  return now.getTime() - lastVote < quietMs
    ? { color: "success", text: `${plural(accounts, "account")} voting` }
    : online;
}

const unreadable: Record<string, string> = {
  noLog:
    "FUNC found no node.log in the node's data directory. Check that the node's LogSizeLimit setting isn't 0.",
  noEvents:
    "The node's log has no telemetry events in it. Check that TelemetryToLog isn't set to false in the node's config.json.",
  stale:
    "The node isn't writing to its log. Set BaseLoggerDebugLevel to 4 or higher under Manage > Configure.",
};

// After starting, a node takes a while to settle on its full set of peers.
const settleMs = 10 * 60 * 1000;

// Network: connected to enough peers. settling: the node is starting or
// syncing, when few peers are normal and nothing to worry a user about.
export function networkLight(
  health: NodeHealth | undefined,
  now: Date,
  settling = false
): Light {
  if (!health || (health.peersIn == null && health.peersOut == null))
    return settling ? { color: "icon", text: "Connecting...", busy: true } : checking;
  const peers = (health.peersIn ?? 0) + (health.peersOut ?? 0);
  const justStarted = !!health.startedAt && now.getTime() - Date.parse(health.startedAt) < settleMs;
  if (settling || justStarted)
    return peers
      ? { color: "icon", text: plural(peers, "peer") }
      : { color: "icon", text: "Connecting...", busy: true };
  if (!peers)
    return {
      color: "error",
      text: "No peers",
      hint: "The node isn't connected to the network. Check this computer's internet connection and firewall.",
    };
  if (peers < fewPeers)
    return {
      color: "warning",
      text: `Only ${plural(peers, "peer")}`,
      hint: `Nodes normally keep at least ${fewPeers} connections. Check this computer's internet connection.`,
    };
  return { color: "success", text: plural(peers, "peer") };
}
