import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import BlocksCalendar from "./BlocksCalendar.vue";

// Wednesday 17 June 2026, local time.
const today = new Date(2026, 5, 17, 12);

// Unix seconds for local noon, `daysAgo` days before today.
function ts(daysAgo: number) {
  const d = new Date(today);
  d.setDate(d.getDate() - daysAgo);
  return Math.floor(d.getTime() / 1000);
}

function render(timestamps: number[]) {
  return mount(BlocksCalendar, {
    props: { name: "Algorand", timestamps },
    global: { plugins: [createVuetify()] },
  });
}

const styles = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll(".heatmap .cell").map((c) => c.attributes("style") ?? "");

describe("BlocksCalendar", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(today);
  });
  afterEach(() => vi.useRealTimers());

  it("renders nothing without blocks in the window", () => {
    expect(render([]).find("section").exists()).toBe(false);
    // The window starts 364 days back, on the Sunday of that week.
    expect(render([ts(400)]).find("section").exists()).toBe(false);
  });

  it("lays out whole weeks ending with the current one", () => {
    const cells = styles(render([ts(0)]));

    // 15 June 2025 (Sunday) to 20 June 2026 (Saturday).
    expect(cells).toHaveLength(53 * 7);
    // Thursday to Saturday of this week haven't happened yet.
    expect(cells.filter((s) => s.includes("transparent"))).toHaveLength(3);
    expect(cells.slice(-3).every((s) => s.includes("transparent"))).toBe(true);
  });

  it("counts blocks per local day", () => {
    const wrapper = render([ts(0), ts(0), ts(3), ts(366), ts(400)]);

    // 366 days back still falls in the first week shown; 400 does not.
    expect(wrapper.text()).toContain("4 blocks · last 12 months");
    const filled = styles(wrapper).filter((s) => s.includes("--v-theme-primary"));
    expect(filled).toHaveLength(3);
  });

  it("shades each day relative to the busiest one", () => {
    const cells = styles(render([ts(0), ts(0), ts(0), ts(0), ts(1), ts(2), ts(2)]));
    const day = (daysAgo: number) => cells[cells.length - 4 - daysAgo];

    expect(day(0)).toContain("rgba(var(--v-theme-primary), 1)"); // 4 of 4
    expect(day(1)).toContain("rgba(var(--v-theme-primary), 0.4375)"); // 1 of 4
    expect(day(2)).toContain("rgba(var(--v-theme-primary), 0.625)"); // 2 of 4
    expect(day(3)).toContain("--v-theme-surface-selected"); // none
  });

  const month = (year: number, index: number) =>
    new Date(year, index, 1).toLocaleDateString(undefined, { month: "short" });

  const monthLabels = () =>
    render([Math.floor(Date.now() / 1000)])
      .findAll(".month-label")
      .map((l) => l.text());

  it("labels each month at its first week", () => {
    // The window opens on 15 June 2025, three weeks before July's first column.
    const labels = monthLabels();

    expect(labels).toHaveLength(13);
    expect(labels[0]).toBe(month(2025, 5));
    expect(labels[1]).toBe(month(2025, 6));
    expect(labels[12]).toBe(month(2026, 5));
  });

  it("drops a leading month with no room for its label", () => {
    // Wednesday 1 July 2026: the window opens on 29 June 2025, one week before July.
    vi.setSystemTime(new Date(2026, 6, 1, 12));

    const labels = monthLabels();

    expect(labels).toHaveLength(12);
    expect(labels[0]).toBe(month(2025, 6));
    expect(labels[11]).toBe(month(2026, 5));
  });
});
