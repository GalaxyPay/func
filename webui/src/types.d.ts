import { modelsv2 } from "algosdk";

export interface NodeStatus {
  machineName: string;
  isWindows: boolean;
  serviceStatus: string;
  // Whether the service starts again when the computer boots, even if stopped.
  startsAtBoot?: boolean | null;
  port: number;
  token: string;
  telemetryStatus?: string;
  retiStatus?: DaemonStatus;
  valarStatus?: DaemonStatus;
  external?: ExternalNode;
  detected?: ExternalNode;
  // Node binaries are being installed or updated, which stops the node for a while.
  updating?: boolean;
}

// A node installed by apt/rpm, NodeKit, Homebrew or update.sh.
export interface ExternalNode {
  manager: string;
  dataDir: string;
  binDir: string;
  service?: string;
  user?: string;
  network?: string;
}

export interface DaemonStatus {
  serviceStatus: string;
  version?: string;
  exeStatus?: string;
}

export interface SnackBar {
  text: string;
  color: string;
  timeout: number;
  display: boolean;
}

export interface Participation {
  address: string;
  id: string;
  key: modelsv2.AccountParticipation;
}

export interface AuthStatus {
  hasPassword: boolean;
  signedIn: boolean;
}

export interface GoalVersion {
  installed: string;
  latest: string;
  manager: string;
  updates: NodeUpdates;
  // Why the installed version can't follow the network (it will stall at an
  // approved consensus upgrade), or null.
  unsupported: string | null;
}

// auto: FUNC installs new releases. notify: alerts, the user installs.
// manual: stays on the version the user picked, no alerts.
export type NodeUpdateMode = "auto" | "notify" | "manual";

export interface NodeUpdates {
  mode: NodeUpdateMode;
  lastCheck: string | null;
  lastError: string | null;
  lastUpdated: string | null;
}

export interface PartDetails {
  // Keys on the node, online or not.
  keys: number;
  activeKeys: number;
  activeStake: number;
  proposals: number | undefined;
}

// A block this node created, for the dashboard's toast.
export interface CreatedBlock {
  round: bigint;
  address: string;
  // The proposer's payout, in micro units; 0 when the account isn't eligible.
  payout: bigint;
  unit: string;
}

// GET func/version: the running FUNC against its latest release.
export interface UpdateInfo {
  installed?: string;
  latest?: string;
  available: boolean;
}

// GET {name}/keys: what the service knows about each participation key.
export interface PartKey {
  id: string;
  address: string;
  lastValid: number;
  // Estimated from the recent block time.
  expires?: string;
  // The account is online with this key.
  registered: boolean;
  // The daemon that looks after this account's keys.
  manager?: "reti" | "valar" | null;
  // Registered, not managed, and runs out within a week (or already has).
  expiringSoon: boolean;
}

export interface Message {
  id: number;
  title: string;
  body: string;
}

// GET {name}/health: what FUNC has gathered from the node's log and metrics.
export interface NodeHealth {
  // "ok", "noLog", "stale" (log not growing) or "noEvents" (no telemetry in it).
  source: "ok" | "noLog" | "stale" | "noEvents";
  metrics: boolean;
  version?: string;
  startedAt?: string;
  lastRound: number;
  lastRoundAt?: string;
  // When the node last sent a vote for one of its online accounts.
  lastVoteAt?: string;
  peersIn?: number;
  peersOut?: number;
  bucketSeconds: number;
  points: HealthPoint[];
  peers: HealthPeer[];
  peersAt?: string;
  // Recent errors from the log, newest first.
  notes: HealthNote[];
}

export interface HealthPoint {
  t: number;
  rounds: number;
  roundMs?: number;
  roundMsMax: number;
  votes: number;
  proposals: number;
  // Proposals the network chose as the round's block.
  wins: number;
  blocks: number;
  arrivalMs?: number;
  arrivalMsMax: number;
  recoveries: number;
  connects: number;
  disconnects: number;
  connectFails: number;
  warnings: number;
  errors: number;
  peersIn?: number;
  peersOut?: number;
  bytesIn: number;
  bytesOut: number;
  dropped: number;
}

export interface HealthPeer {
  endpoint?: string;
  address?: string;
  host?: string;
  incoming: boolean;
  connectedSeconds: number;
}

export interface HealthNote {
  time: string;
  level: "info" | "warning" | "error";
  text: string;
  count: number;
}

// GET {name}/log: one page of a search through the node's log, newest first.
export interface LogPage {
  found: boolean;
  lines: LogLine[];
  // Pass as "before" for older lines; null at the start of the log.
  next?: number | null;
  // Pass as "after" for lines added since this page.
  end: number;
  // The log was replaced by a new one; start over.
  reset: boolean;
}

export interface LogLine {
  offset: number;
  time?: string;
  level: string;
  msg: string;
  raw: string;
}
