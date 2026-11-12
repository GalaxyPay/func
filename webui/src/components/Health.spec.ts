import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import type { NodeHealth } from "@/types";
import { nodeHealth, point } from "@/utils/health.fixtures";
import Health from "./Health.vue";

const api = { get: vi.fn() };
const setSnackbar = vi.fn();
vi.mock("@/stores/app", () => ({
  useAppStore: () => ({ api, setSnackbar }),
}));

const now = new Date("2026-11-11T23:30:00Z");

function respond(health: Partial<NodeHealth>) {
  api.get.mockResolvedValue({
    data: nodeHealth({
      version: "5.0.5",
      lastRound: 66867307,
      lastRoundAt: "2026-11-11T23:29:58Z",
      peersIn: 0,
      peersOut: 4,
      points: [
        point({ t: 1794439800, rounds: 20, roundMs: 2770, roundMsMax: 2900, votes: 31, blocks: 20, arrivalMs: 71, arrivalMsMax: 350 }),
        point({ t: 1794440700, rounds: 29, roundMs: 2760, roundMsMax: 3400, votes: 26, proposals: 2, blocks: 29, arrivalMs: 80, arrivalMsMax: 900, warnings: 1 }),
      ],
      notes: [
        { time: "2026-11-11T23:13:40Z", level: "warning", text: "dbatomic: tx surpassed expected deadline", count: 6 },
      ],
      peers: [
        { endpoint: "r-is.algorand-mainnet.network:4160", host: "mr-dal.example.net", incoming: false, connectedSeconds: 7919 },
      ],
      ...health,
    }),
  });
}

async function open(props: { visible?: boolean; running?: boolean } = {}) {
  const wrapper = mount(Health, {
    props: { visible: true, name: "Algorand", running: true, ...props },
    global: { plugins: [createVuetify()] },
  });
  await flushPromises();
  return wrapper;
}

// The dialog renders into the page body, outside the component's own element.
const page = () => document.body.textContent ?? "";

describe("Health", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date", "setInterval", "clearInterval"] });
    vi.setSystemTime(now);
    api.get.mockReset();
    setSnackbar.mockReset();
    respond({});
  });
  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = "";
  });

  it("loads the node's health when opened", async () => {
    await open();

    expect(api.get).toHaveBeenCalledExactlyOnceWith("Algorand/health", {
      params: { range: "24h" },
    });
    expect(page()).toContain("Connected to 4 peers");
    expect(page()).toContain("57 votes sent");
    expect(page()).toContain("2.76 s");
    expect(page()).toContain("Slowest 3.40 s");
    expect(page()).toContain("dbatomic: tx surpassed expected deadline");
    expect(page()).toContain("(6 times)");
    expect(page()).toContain("r-is.algorand-mainnet.network:4160");
    expect(page()).toContain("Outgoing, 2h 11m");
    expect(page()).toContain("algod 5.0.5");
  });

  it("does nothing while closed", async () => {
    await open({ visible: false });
    await vi.advanceTimersByTimeAsync(60000);

    expect(api.get).not.toHaveBeenCalled();
  });

  it("refreshes while open and stops when closed", async () => {
    const wrapper = await open();

    await vi.advanceTimersByTimeAsync(20000);
    expect(api.get).toHaveBeenCalledTimes(3);

    await wrapper.setProps({ visible: false });
    await vi.advanceTimersByTimeAsync(60000);
    expect(api.get).toHaveBeenCalledTimes(3);
  });

  it("stops refreshing when removed from the page", async () => {
    const wrapper = await open();

    wrapper.unmount();
    await vi.advanceTimersByTimeAsync(60000);

    expect(api.get).toHaveBeenCalledTimes(1);
  });

  it("reloads for another time range", async () => {
    await open();
    const hour = [...document.body.querySelectorAll("button")].find(
      (b) => b.textContent?.trim() === "Hour"
    )!;

    hour.click();
    await flushPromises();

    expect(api.get).toHaveBeenLastCalledWith("Algorand/health", {
      params: { range: "1h" },
    });
  });

  it("shows traffic only when the node's metrics are available", async () => {
    await open();
    expect(page()).not.toContain("Data received");
    document.body.innerHTML = "";

    respond({
      metrics: true,
      points: [point({ t: 1794440700, bytesIn: 350_000, bytesOut: 11_000, dropped: 2 })],
    });
    await open();

    expect(page()).toContain("Data received");
    expect(page()).toContain("350 KB");
    expect(page()).toContain("Sent 11 KB, 2 messages dropped");
  });

  it("explains a running node that isn't logging", async () => {
    respond({ source: "stale" });

    await open({ running: true });

    expect(page()).toContain("Set BaseLoggerDebugLevel to 4 or higher");
  });

  it("doesn't nag about a quiet log while the node is stopped", async () => {
    respond({ source: "stale" });

    await open({ running: false });

    expect(page()).toContain("Nothing new is being written");
    expect(page()).not.toContain("BaseLoggerDebugLevel");
  });

  it("reports a failed load and keeps showing earlier data", async () => {
    await open();
    api.get.mockRejectedValue(new Error("boom"));

    await vi.advanceTimersByTimeAsync(10000);

    // A background refresh that fails is retried quietly.
    expect(setSnackbar).not.toHaveBeenCalled();
    expect(page()).toContain("57 votes sent");

    document.body.innerHTML = "";
    await open();
    expect(setSnackbar).toHaveBeenCalledExactlyOnceWith(
      "Loading node health failed: boom",
      "error"
    );
  });
});
