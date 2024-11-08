import algosdk from "algosdk";
import AWN from "./services/api";

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

export async function execAtc(
  atc: algosdk.AtomicTransactionComposer,
  algodClient: algosdk.Algodv2,
  success: string
) {
  const store = useAppStore();
  store.setSnackbar("Awaiting Signatures...", "info", -1);
  await atc.gatherSignatures();
  store.setSnackbar("Processing...", "info", -1);
  await atc.execute(algodClient, 4);
  store.setSnackbar(success, "success");
  store.refresh++;
}

async function getCatchpoint(name: string): Promise<string | undefined> {
  const MAINNET_URL =
    "https://afmetrics.api.nodely.io/v1/delayed/catchup/label/current";
  const VOIMAIN_URL = "https://mainnet-api.voi.nodely.dev/v2/status";
  // const FNET_URL = "https://fnet-api.4160.nodely.io/v2/status";

  switch (name) {
    case "Algorand": {
      const resp = await axios({ url: MAINNET_URL });
      return resp.data["last-catchpoint"];
    }
    case "Voi": {
      const resp = await axios({ url: VOIMAIN_URL });
      return resp.data["last-catchpoint"];
    }
    case "FNet": {
      // const resp = await axios({ url: FNET_URL });
      // return resp.data["last-catchpoint"];
      return "2330000#UENIUVU6ZTMPM5ZGYYY75ESP5H77E4IHHCBQRRCC2ULN753GSQIQA";
    }
  }
}

export async function checkCatchup(algodStatus: any, name: string) {
  if (algodStatus?.["catchup-time"]) {
    const catchpoint = await getCatchpoint(name);
    if (!catchpoint) throw Error("Invald Catchpoint");
    const [round, label] = catchpoint.split("#");
    const isCatchingUp = algodStatus?.["catchpoint"] === catchpoint;
    const needsCatchUp = Number(round) - algodStatus?.["last-round"] > 20000;
    if (!isCatchingUp && needsCatchUp) {
      await AWN.api.post(`${name}/catchup`, { round, label });
    }
  }
}
