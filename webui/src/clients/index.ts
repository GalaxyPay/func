import { NodeStatus } from "@/types";
import { algodBaseUrl, type ProxiedTarget } from "@/utils";
import { Algodv2 } from "algosdk";

export class AlgodFunc extends Algodv2 {
  // Where this client points, so failures can name the unreachable endpoint
  // rather than reporting a bare "Failed to fetch".
  readonly baseUrl: string;

  constructor(name: string, nodeStatus: NodeStatus) {
    // No port: the base URL carries FUNC's origin and the /algod/<network> path.
    const baseUrl = algodBaseUrl(name);
    super(nodeStatus.token, baseUrl);
    this.baseUrl = baseUrl;
  }
}

// Human-readable name for the node's algod API, used in error messages. It is
// reached through FUNC's proxy, so errors can tell FUNC and the node apart.
export function algodTarget(baseUrl?: string): ProxiedTarget {
  return { proxied: `the node's algod API${baseUrl ? ` (${baseUrl})` : ""}` };
}
