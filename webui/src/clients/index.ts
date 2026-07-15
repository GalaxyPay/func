import { networks } from "@/data";
import { NodeStatus } from "@/types";
import { Algodv2 } from "algosdk";

export class AlgodFunc extends Algodv2 {
  constructor(name: string, nodeStatus: NodeStatus) {
    const port =
      location.protocol === "https:"
        ? networks.find((n) => n.title === name)?.yarpAlgodPort
        : nodeStatus.port;
    super(
      nodeStatus.token,
      `${location.protocol}//${import.meta.env.VITE_HOSTNAME}`,
      port
    );
  }
}
