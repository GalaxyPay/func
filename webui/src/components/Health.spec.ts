import { afterEach, describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import type { NodeHealth } from "@/types";
import { nodeHealth, point } from "@/utils/health.fixtures";
import Health from "./Health.vue";

const now = new Date("2026-11-11T23:30:00Z");

function show(values: Partial<NodeHealth> = {}) {
  return mount(Health, {
    props: {
      now,
      health: nodeHealth({
        version: "5.0.5",
        startedAt: "2026-11-11T20:18:00Z",
        peersIn: 0,
        peersOut: 4,
        points: [
          point({ t: 1794439800, rounds: 20, votes: 31 }),
          point({ t: 1794440700, rounds: 29, votes: 26, proposals: 2, wins: 1, peersOut: 4 }),
        ],
        peers: [
          {
            endpoint: "r-is.algorand-mainnet.network:4160",
            host: "mr-dal.example.net",
            incoming: false,
            connectedSeconds: 7919,
          },
        ],
        ...values,
      }),
    },
    global: { plugins: [createVuetify()] },
  });
}

describe("Health", () => {
  let wrapper: ReturnType<typeof show>;
  afterEach(() => wrapper.unmount());

  it("shows votes, proposals and peers for the last day", () => {
    wrapper = show();
    const text = wrapper.text();

    expect(text).toContain("Votes Sent57");
    expect(text).toContain("In 49 blocks");
    expect(text).toContain("Blocks Proposed2");
    expect(text).toContain("1 chosen by the network");
    expect(text).toContain("4 outgoing, 0 incoming");
    expect(text).toContain("r-is.algorand-mainnet.network:4160");
    expect(text).toContain("Outgoing, 2h 11m");
    expect(text).toContain("algod 5.0.5 · up 3h 12m");
    // Only what a user can act on: no round times, traffic or warnings.
    expect(text).not.toMatch(/Time per block|Data received|Warnings/);
  });

  it("lists the latest errors", () => {
    const notes = Array.from({ length: 7 }, (_, i) => ({
      time: "2026-11-11T23:13:40Z",
      level: "error" as const,
      text: `error ${i}`,
      count: i === 0 ? 6 : 1,
    }));
    wrapper = show({ notes });

    expect(wrapper.text()).toMatch(/6×\s+error 0/);
    expect(wrapper.text()).toContain("error 4");
    expect(wrapper.text()).not.toContain("error 5");
    expect(wrapper.text()).toContain("Manage > View Log");
  });

  it("says when there is nothing to list", () => {
    wrapper = show({ peers: [], peersIn: undefined, peersOut: undefined, points: [] });

    expect(wrapper.text()).toContain("No errors in the node's log.");
    expect(wrapper.text()).toContain("hasn't listed its peers yet");
    expect(wrapper.text()).toContain("Not reported yet");
    expect(wrapper.text()).toContain("None in the last 24 hours");
  });
});
