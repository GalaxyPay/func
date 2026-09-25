// Utilities
import { AlgodFunc } from "@/clients";
import { AuthStatus, GoalVersion, Message, SnackBar } from "@/types";
import algosdk, { modelsv2 } from "algosdk";
import { defineStore } from "pinia";

const TOKEN_KEY = "session";

function readToken(): string {
  try {
    return localStorage.getItem(TOKEN_KEY) || "";
  } catch {
    return "";
  }
}

// The FUNC API requires a signed-in session (see README, "Password"). The session
// token is kept in this browser only; a 401 from any call opens the sign-in dialog.
function createApi() {
  const api = axios.create({ baseURL: import.meta.env.VITE_ORIGIN });
  const token = readToken();
  if (token) api.defaults.headers.common.Authorization = `Bearer ${token}`;
  api.interceptors.response.use(undefined, (err) => {
    if (err?.response?.status === 401) {
      useAppStore().authRequired = true;
    }
    return Promise.reject(err);
  });
  return api;
}

export const useAppStore = defineStore("app", {
  state: () => ({
    api: createApi(),
    authRequired: false,
    auth: undefined as AuthStatus | undefined,
    ready: false,
    overlay: false,
    snackbar: {
      text: "",
      color: "",
      timeout: 0,
      display: false,
    } as SnackBar,
    account: undefined as undefined | modelsv2.Account,
    refreshPart: 0,
    refreshStatus: 0,
    connectMenu: false,
    funcUpdateAvailable: false,
    nodeUpdateAvailable: false,
    downloading: false,
    stoppingReti: false,
    goalVersion: undefined as GoalVersion | undefined,
    showNetworks: (localStorage.getItem("showNetworks") === "true") as
      | boolean
      | null,
    showMachineName: (localStorage.getItem("showMachineName") === "true") as
      | boolean
      | null,
    showNodeVersions: (localStorage.getItem("showNodeVersions") === "true") as
      | boolean
      | null,
    machineName: undefined as string | undefined,
    isWindows: undefined as boolean | undefined,
    resetDates: JSON.parse(localStorage.getItem("resetDates") || "[]") as {
      name: string;
      date: string;
      mode?: "year" | "month" | "today" | "custom";
    }[],
    messages: [] as Message[],
    readMessages: JSON.parse(
      localStorage.getItem("readMessages") || "[]"
    ) as number[],
  }),
  getters: {
    unreadCount(state): number {
      const read = new Set(state.readMessages);
      return state.messages.filter((m) => !read.has(m.id)).length;
    },
  },
  actions: {
    // Ask the service whether a password exists and whether this browser is signed
    // in; opens the setup or sign-in dialog when needed.
    async checkAuth() {
      const { data } = await this.api.get("auth");
      this.auth = data;
      if (!data.hasPassword || !data.signedIn) this.authRequired = true;
      return data as AuthStatus;
    },
    async signOut(everywhere = false) {
      try {
        await this.api.post(everywhere ? "auth/logout-all" : "auth/logout");
      } catch {
        // Already signed out or unreachable; drop the local session regardless.
      }
      this.setApiToken("");
      location.reload();
    },
    setApiToken(token: string) {
      token = token.trim();
      try {
        if (token) localStorage.setItem(TOKEN_KEY, token);
        else localStorage.removeItem(TOKEN_KEY);
      } catch {
        // Private window or blocked storage: the token lives for this page load only.
      }
      if (token) {
        this.api.defaults.headers.common.Authorization = `Bearer ${token}`;
      } else {
        delete this.api.defaults.headers.common.Authorization;
      }
      this.authRequired = false;
    },
    async setSnackbar(text: string, color = "info", timeout = 4000) {
      // While the sign-in dialog is up, every polled call fails with 401; the
      // dialog already says so, so don't stack error toasts behind it.
      if (color == "error" && this.authRequired) return;
      if (color == "error") timeout = 15000;
      this.snackbar = {
        text: text,
        color: color,
        timeout: timeout,
        display: true,
      };
    },
    async fetchMessages() {
      const name = "algorand";
      const { data } = await this.api.get(name);
      const algodClient = new AlgodFunc(name, data);
      const resp = await algodClient
        .getApplicationBoxes(import.meta.env.VITE_MESSAGES_APP_ID)
        .include("values")
        .do();
      const abiType = algosdk.ABIType.from("(string,string)");
      // Message boxes have 8-byte uint64 keys; allowedSenders boxes have
      // 32-byte address keys and hold no message data.
      this.messages = (resp.boxes ?? [])
        .filter((box) => box.name.length === 8)
        .map((box) => {
          const abiData = abiType.decode(box.value!) as string[];
          return {
            id: algosdk.decodeUint64(box.name),
            title: abiData[0],
            body: abiData[1],
          };
        });
      // Drop read ids that no longer correspond to an existing message.
      const ids = new Set(this.messages.map((m) => m.id));
      this.setReadMessages(this.readMessages.filter((id) => ids.has(id)));
    },
    isRead(id: number): boolean {
      return this.readMessages.includes(id);
    },
    markRead(id: number) {
      if (!this.readMessages.includes(id)) {
        this.setReadMessages([...this.readMessages, id]);
      }
    },
    markUnread(id: number) {
      this.setReadMessages(this.readMessages.filter((r) => r !== id));
    },
    markAllRead() {
      this.setReadMessages(this.messages.map((m) => m.id));
    },
    setReadMessages(ids: number[]) {
      this.readMessages = ids;
      localStorage.setItem("readMessages", JSON.stringify(ids));
    },
  },
});
