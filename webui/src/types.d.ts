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
