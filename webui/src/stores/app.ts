// Utilities
import { GoalVersion, SnackBar } from "@/types";
import { defineStore } from "pinia";

export const useAppStore = defineStore("app", {
  state: () => ({
    ready: false,
    overlay: false,
    snackbar: {
      text: "",
      color: "",
      timeout: 0,
      display: false,
    } as SnackBar,
    refresh: 0,
    connectMenu: false,
    stopNodeServices: false,
    funcUpdateAvailable: false,
    nodeUpdateAvailable: false,
    downloading: false,
    stoppingReti: false,
    goalVersion: undefined as GoalVersion | undefined,
    showNetworks: localStorage.getItem("showNetworks") === "true",
    isIncentiveReady: false,
  }),
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
  },
});
