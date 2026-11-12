import { modelsv2 } from "algosdk";

export interface NodeStatus {
  machineName: string;
  isWindows: boolean;
  serviceStatus: string;
  port: number;
  token: string;
  telemetryStatus?: string;
  retiStatus?: DaemonStatus;
  valarStatus?: DaemonStatus;
  external?: ExternalNode;
  detected?: ExternalNode;
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
}

export interface PartDetails {
  activeKeys: number;
  activeStake: number;
  proposals: number | undefined;
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
  peersIn?: number;
  peersOut?: number;
  bucketSeconds: number;
  points: HealthPoint[];
  peers: HealthPeer[];
  peersAt?: string;
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
