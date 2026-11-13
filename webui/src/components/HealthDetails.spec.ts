import { afterEach, describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import type { NodeHealth } from "@/types";
import { nodeHealth } from "@/utils/health.fixtures";
import HealthDetails from "./HealthDetails.vue";

const now = new Date("2026-11-11T23:30:00Z");

function show(values: Partial<NodeHealth> = {}) {
  return mount(HealthDetails, {
    props: {
      now,
      health: nodeHealth({
        version: "5.0.5",
        startedAt: "2026-11-11T20:18:00Z",
        peers: [
          {
            endpoint: "r-is.algorand-mainnet.network:4160",
            host: "mr-dal.example.net",
            incoming: false,
            connectedSeconds: 7919,
          },
          { address: "203.0.113.9:51234", incoming: true, connectedSeconds: 30 },
        ],
        ...values,
      }),
    },
    global: { plugins: [createVuetify()] },
  });
}

describe("HealthDetails", () => {
  let wrapper: ReturnType<typeof show>;
  afterEach(() => wrapper.unmount());

  it("lists peers without their port", () => {
    wrapper = show();
    const text = wrapper.text();

    expect(text).toContain("r-is.algorand-mainnet.network");
    expect(text).not.toContain(":4160");
    expect(text).toMatch(/Out ·\s*2h 11m/);
    expect(text).toMatch(/203\.0\.113\.9\s*In ·\s*30s/);
    // Only peers: errors and uptime are shown elsewhere.
    expect(text).not.toMatch(/error|algod/i);
  });

  it("shortens relays that share a domain", () => {
    wrapper = show({
      peers: ["r-is", "r-6f"].map((name) => ({
        endpoint: `${name}.algorand-mainnet.network:4160`,
        incoming: false,
        connectedSeconds: 60,
      })),
    });
    const names = wrapper.findAll(".peer-name");

    expect(names.map((n) => n.text())).toEqual(["r-is", "r-6f"]);
    expect(names[0].attributes("title")).toBe("r-is.algorand-mainnet.network:4160");
  });

  it("says when there is nothing to list", () => {
    wrapper = show({ peers: [] });

    expect(wrapper.text()).toContain("Not listed yet");
  });
});
