import { modelsv2 } from "algosdk";

export interface NodeConfig {
  port: number;
  token: string;
  serviceStatus: string;
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
