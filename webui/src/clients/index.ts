import { networks } from "@/data";
import { NodeStatus } from "@/types";
import { Algodv2 } from "algosdk";

export class AlgodFunc extends Algodv2 {
  // Where this client points, so failures can name the unreachable endpoint
  // rather than reporting a bare "Failed to fetch".
  readonly baseUrl: string;

  constructor(name: string, nodeStatus: NodeStatus) {
    const hostname = import.meta.env.VITE_HOSTNAME || location.hostname;
    const port =
      location.protocol === "https:"
        ? networks.find((n) => n.title === name)?.yarpAlgodPort
        : nodeStatus.port;
    super(nodeStatus.token, `${location.protocol}//${hostname}`, port);
    this.baseUrl = `${location.protocol}//${hostname}${port ? `:${port}` : ""}`;
  }
}

// Human-readable name for the node's algod API, used in error messages.
export function algodTarget(baseUrl?: string) {
  return `the node's algod API${baseUrl ? ` (${baseUrl})` : ""}`;
}
