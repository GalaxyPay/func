import { afterEach, describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import type { NodeHealth } from "@/types";
import { nodeHealth, point } from "@/utils/health.fixtures";
import Health from "./Health.vue";

function show(values: Partial<NodeHealth> = {}, range = "24h") {
  return mount(Health, {
    attachTo: document.body,
    props: {
      range,
      health: nodeHealth({
        points: [
          point({ t: 1794439800, rounds: 20, votes: 31 }),
          point({ t: 1794440700, rounds: 29, votes: 26, proposals: 2, wins: 1 }),
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

  it("shows votes and proposals for the range", () => {
    wrapper = show();
    const text = wrapper.text();

    expect(text).toMatch(/Votes Sent\s*in 49 blocks\s*57/);
    expect(text).toMatch(/Blocks Proposed\s*1 won\s*2/);
    // Only what a user can act on: no round times, traffic, peers or warnings.
    expect(text).not.toMatch(/Time per block|Data received|Peers|Warnings/);
  });

  it("says when the node proposed nothing", () => {
    wrapper = show({ points: [] });
    expect(wrapper.text()).toMatch(/Blocks Proposed\s*none\s*0/);
  });

  it("switches to another time range", async () => {
    wrapper = show({}, "7d");
    const hour = wrapper.findAll("button").find((b) => b.text() === "Hour")!;

    await hour.trigger("click");

    expect(wrapper.emitted("update:range")).toEqual([["1h"]]);
  });
});
