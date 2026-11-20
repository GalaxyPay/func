// Utilities
import {
  AuthStatus,
  GoalVersion,
  Message,
  NodeUpdateMode,
  SnackBar,
} from "@/types";
import { isNewer } from "@/utils";
import { modelsv2 } from "algosdk";
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
    showAdvanced: (localStorage.getItem("showAdvanced") === "true") as
      | boolean
      | null,
    showNetworks: (localStorage.getItem("showNetworks") === "true") as
      | boolean
      | null,
    showMachineName: (localStorage.getItem("showMachineName") === "true") as
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
    // An adopted node's manager installs its binaries, always at the latest version.
    nodeAdopted(state): boolean {
      const manager = state.goalVersion?.manager;
      return !!manager && manager !== "func";
    },
    // Manual version selection only applies to binaries FUNC installs itself.
    manualNodeVersions(): boolean {
      return !this.nodeAdopted && this.goalVersion?.updates.mode === "manual";
    },
    // Something about the node's version needs the user: a release they asked to
    // hear about (notify mode, or an adopted node), or a version that will stall.
    nodeVersionAlert(state): boolean {
      if (state.goalVersion?.unsupported) return true;
      return (
        state.nodeUpdateAvailable &&
        (this.nodeAdopted || state.goalVersion?.updates.mode === "notify")
      );
    },
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
    async refreshGoalVersion() {
      const { data } = await this.api.get<GoalVersion>("goal/version");
      this.goalVersion = data;
      this.nodeUpdateAvailable = isNewer(data?.latest, data?.installed);
      await this.migrateNodeVersionsSetting();
    },
    async setNodeUpdateMode(mode: NodeUpdateMode) {
      const { data } = await this.api.put("goal/updates", { mode });
      if (this.goalVersion) this.goalVersion.updates = data;
    },
    // Manual version selection used to be a per-browser setting; it's now the
    // service's manual update mode. Carry it over once, unless already changed.
    async migrateNodeVersionsSetting() {
      if (localStorage.getItem("showNodeVersions") !== "true") return;
      try {
        if (!this.nodeAdopted && this.goalVersion?.updates.mode === "notify")
          await this.setNodeUpdateMode("manual");
        localStorage.removeItem("showNodeVersions");
      } catch (err) {
        console.error(err);
      }
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
    // The service reads them through the Algorand node and caches them; until that
    // node has synced the list is just empty. refresh skips the cache.
    async fetchMessages(refresh = false) {
      const { data } = await this.api.get<Message[]>("func/messages", {
        params: { refresh },
      });
      this.messages = data;
      // Drop read ids that no longer correspond to an existing message. An empty
      // list usually means the node isn't synced yet, so leave them alone then.
      if (!data.length) return;
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
