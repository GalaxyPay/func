import { modelsv2 } from "algosdk";

export interface NodeStatus {
  machineName: string;
  serviceStatus: string;
  port: number;
  token: string;
  telemetryStatus?: string;
  retiStatus?: RetiStatus;
}

export interface RetiStatus {
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

export interface GoalVersion {
  installed: string;
  latest: string;
}

export interface PartDetails {
  activeKeys: number;
  activeStake: number;
  proposals: number | undefined;
  votes: number | undefined;
}
