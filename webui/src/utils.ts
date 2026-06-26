import { networks } from "./data";
import FUNC from "./services/api";
import algosdk, { modelsv2 } from "algosdk";

export async function delay(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function formatAddr(addr: string | undefined, length: number = 5) {
  if (!addr) return "";
  return `${addr?.substring(0, length)}...${addr?.substring(58 - length)}`;
}

export function b64(val: Buffer | Uint8Array | undefined) {
  return val ? Buffer.from(val).toString("base64") : undefined;
}

// Resolve a reset entry to its effective start date. Relative modes
// (year/month/today) are recomputed from the current date so the filter
// stays live and refreshes with each block, rather than being frozen at
// the moment the filter was selected. Custom mode keeps its stored date.
export function effectiveResetDate(entry?: {
  date: string;
  mode?: "year" | "month" | "today" | "custom";
}): string | undefined {
  if (!entry?.date) return undefined;
  const now = new Date();
  switch (entry.mode) {
    case "year":
      return new Date(now.getFullYear(), 0, 1).toLocaleDateString();
    case "month":
      return new Date(
        now.getFullYear(),
        now.getMonth(),
        1
      ).toLocaleDateString();
    case "today":
      return new Date(
        now.getFullYear(),
        now.getMonth(),
        now.getDate()
      ).toLocaleDateString();
    default:
      return entry.date;
  }
}

export async function execAtc(
  atc: algosdk.AtomicTransactionComposer,
  algodClient: algosdk.Algodv2,
  success: string
) {
  const store = useAppStore();
  store.setSnackbar("Awaiting Signatures...", "info", -1);
  await atc.gatherSignatures();
  store.setSnackbar("Processing...", "info", -1);
  await atc.execute(algodClient, 10);
  store.setSnackbar(success, "success");
  store.refreshPart++;
}

async function getCatchpoint(name: string): Promise<string | undefined> {
  const resp = await axios({
    url: networks.find((n) => n.title === name)?.catchpointUrl,
  });
  return resp.data["last-catchpoint"];
}

export async function checkCatchup(
  algodStatus: modelsv2.NodeStatusResponse | undefined,
  name: string
) {
  if (algodStatus?.catchupTime) {
    const catchpoint = await getCatchpoint(name);
    if (!catchpoint) throw Error("Invald Catchpoint");
    const [round, label] = catchpoint.split("#");
    const isCatchingUp = algodStatus?.catchpoint === catchpoint;
    const needsCatchUp = BigInt(round) - algodStatus?.lastRound > 20000n;
    if (!isCatchingUp && needsCatchUp) {
      await FUNC.api.post(`${name}/catchup`, { round, label });
    }
  }
}
