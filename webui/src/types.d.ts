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
