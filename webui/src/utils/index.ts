import { couldBeCurvePoint } from "./ed25519-check";
import { networks } from "@/data";
import algosdk, { Address, modelsv2 } from "algosdk";

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

// Requests that never reached their target: the process isn't listening, the
// connection was refused, or the browser blocked or timed out the call. The
// browser reports these as a bare "Failed to fetch" (fetch/algosdk) or
// "Network Error" (axios), which says nothing about which process failed.
const networkCodes = [
  "ERR_NETWORK",
  "ERR_CONNECTION_REFUSED",
  "ECONNREFUSED",
  "ECONNRESET",
  "ECONNABORTED",
  "ETIMEDOUT",
  "EAI_AGAIN",
];

const networkMessages =
  /failed to fetch|network\s?error|load failed|fetch failed|connection refused|connection was reset|err_connection|err_network/i;

export function isNetworkError(err: any): boolean {
  if (!err) return false;
  // Anything carrying a response or status was reached and answered.
  if (err.response || err.status) return false;
  if (networkCodes.includes(err.code)) return true;
  if (["TimeoutError", "AbortError", "NetworkError"].includes(err.name))
    return true;
  return networkMessages.test(String(err.message ?? ""));
}

// The FUNC service also serves this page, so with no explicit origin
// configured its API lives at the page's own origin.
const funcOrigin = (() => {
  try {
    return new URL(import.meta.env.VITE_ORIGIN || location.origin).origin;
  } catch {
    return location.origin;
  }
})();

// Recover the origin an axios request was aimed at. Network failures from
// fetch/algosdk carry no request info, hence the optional caller-supplied
// target in errorMessage().
function requestOrigin(err: any): string | undefined {
  const config = err?.config;
  if (!config) return undefined;
  try {
    return new URL(config.url ?? "", config.baseURL || location.origin).origin;
  } catch {
    return undefined;
  }
}

function describeTarget(err: any, target?: string): string {
  if (target) return target;
  const origin = requestOrigin(err);
  if (!origin) return "the server";
  return origin === funcOrigin ? `the FUNC service (${origin})` : origin;
}

function errorDetail(err: any): string {
  const data = err?.response?.data;
  if (typeof data === "string" && data.trim()) return data.trim();
  if (data && typeof data === "object") {
    return data.message || data.error || JSON.stringify(data);
  }
  return err?.message || String(err);
}

// Build a message that names the process that failed. A network failure has no
// useful detail of its own, so name the service that couldn't be reached
// instead of passing the browser's opaque wording on to the user.
export function errorMessage(
  err: any,
  process: string,
  target?: string
): string {
  if (isNetworkError(err)) {
    return `${process} failed: no response from ${describeTarget(
      err,
      target
    )} - check that it is running and not blocked by a firewall or network issue.`;
  }
  return `${process} failed: ${errorDetail(err)}`;
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
    const store = useAppStore();
    const catchpoint = await getCatchpoint(name);
    if (!catchpoint) throw Error("Invald Catchpoint");
    const [round, label] = catchpoint.split("#");
    const isCatchingUp = algodStatus?.catchpoint === catchpoint;
    const needsCatchUp = BigInt(round) - algodStatus?.lastRound > 20000n;
    if (!isCatchingUp && needsCatchUp) {
      await store.api.post(`${name}/catchup`, { round, label });
    }
  }
}

export async function getSuggestedParams(
  algodClient: algosdk.Algodv2
): Promise<algosdk.SuggestedParams> {
  const store = useAppStore();
  if (!store.account) throw Error("Invalid Account");
  const authAddrPk =
    store.account.authAddr?.publicKey ??
    Address.fromString(store.account.address).publicKey;
  const sp = await algodClient.getTransactionParams().do();
  if (!couldBeCurvePoint(authAddrPk)) sp.minFee = 3000n;
  return sp;
}
