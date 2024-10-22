// Utilities
import { SnackBar } from "@/types";
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
    showFNet: localStorage.getItem("showFNet") === "true",
  }),
  getters: {},
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
