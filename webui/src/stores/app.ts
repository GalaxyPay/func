// Utilities
import { AlgodFunc } from "@/clients";
import { GoalVersion, Message, SnackBar } from "@/types";
import algosdk from "algosdk";
import { defineStore } from "pinia";

export const useAppStore = defineStore("app", {
  state: () => ({
    api: axios.create({ baseURL: import.meta.env.VITE_ORIGIN }),
    ready: false,
    overlay: false,
    snackbar: {
      text: "",
      color: "",
      timeout: 0,
      display: false,
    } as SnackBar,
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
    async setSnackbar(text: string, color = "info", timeout = 4000) {
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
      this.messages = (resp.boxes ?? []).map((box) => {
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
