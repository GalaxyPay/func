import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createVuetify } from "vuetify";
import type { LogLine, LogPage } from "@/types";
import Log from "./Log.vue";

const api = { get: vi.fn() };
const setSnackbar = vi.fn();
vi.mock("@/stores/app", () => ({
  useAppStore: () => ({ api, setSnackbar }),
}));

function line(offset: number, msg: string, level = "warning"): LogLine {
  return {
    offset,
    level,
    msg,
    time: "2026-11-11T23:13:40Z",
    raw: JSON.stringify({ level, msg }),
  };
}

function page(values: Partial<LogPage> = {}): { data: LogPage } {
  return { data: { found: true, lines: [], next: null, end: 0, reset: false, ...values } };
}

async function open() {
  const wrapper = mount(Log, {
    props: { visible: true, name: "Algorand" },
    global: { plugins: [createVuetify()] },
  });
  await flushPromises();
  return wrapper;
}

const text = () => document.body.textContent ?? "";
const params = (call: number) => api.get.mock.calls[call][1].params;
const button = (label: string) =>
  [...document.body.querySelectorAll("button")].find(
    (b) => b.textContent?.trim() === label
  );
const input = (label: string) =>
  [...document.body.querySelectorAll("input")].find(
    (i) => i.closest(".v-input")?.textContent?.includes(label)
  )!;

describe("Log", () => {
  beforeEach(() => {
    vi.useFakeTimers({
      toFake: ["setInterval", "clearInterval", "setTimeout", "clearTimeout"],
    });
    api.get.mockReset();
    setSnackbar.mockReset();
  });
  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = "";
  });

  it("shows the newest warnings when opened", async () => {
    api.get.mockResolvedValue(
      page({ lines: [line(900, "disk is slow"), line(400, "peer refused")], end: 1000 })
    );

    await open();

    expect(api.get).toHaveBeenCalledExactlyOnceWith("Algorand/log", {
      params: { q: undefined, level: "warning", limit: 200 },
    });
    expect(text()).toContain("disk is slow");
    expect(text()).toContain("peer refused");
    expect(text()).toContain("2 entries. That is all of them.");
    expect(button("Load older")).toBeUndefined();
  });

  it("loads older entries from the cursor", async () => {
    const newest = Array.from({ length: 20 }, (_, i) => line(2000 - i, `newest ${i}`));
    api.get.mockResolvedValueOnce(page({ lines: newest, next: 1981, end: 3000 }));
    api.get.mockResolvedValueOnce(page({ lines: [line(400, "older")], next: null }));
    await open();
    expect(api.get).toHaveBeenCalledTimes(1);
    expect(text()).toContain("20 entries so far");

    button("Load older")!.click();
    await flushPromises();

    expect(params(1)).toMatchObject({ before: 1981, level: "warning" });
    expect(text()).toContain("newest 19");
    expect(text()).toContain("older");
    expect(text()).toContain("21 entries. That is all of them.");
    expect(button("Load older")).toBeUndefined();
  });

  it("keeps going back while matches are few and far between", async () => {
    api.get.mockResolvedValueOnce(page({ lines: [line(8000, "recent")], next: 5000, end: 9000 }));
    api.get.mockResolvedValueOnce(page({ next: 2000 }));
    api.get.mockResolvedValueOnce(page({ lines: [line(100, "long ago")], next: null }));

    await open();

    expect(api.get).toHaveBeenCalledTimes(3);
    expect(params(1).before).toBe(5000);
    expect(params(2).before).toBe(2000);
    const shown = text();
    expect(shown.indexOf("recent")).toBeLessThan(shown.indexOf("long ago"));
    expect(shown).toContain("2 entries. That is all of them.");
  });

  it("stops searching by itself after a full-size log's worth", async () => {
    let cursor = 1_000_000;
    api.get.mockImplementation(async () => page({ next: (cursor -= 1000) }));

    await open();

    expect(api.get).toHaveBeenCalledTimes(17);
    expect(text()).toContain("Nothing found yet. There is more of the log to search.");
    expect(button("Keep searching")).toBeDefined();
  });

  it("searches a moment after typing stops", async () => {
    api.get.mockResolvedValue(page());
    await open();
    const search = input("Search");

    search.value = "  dbatomic ";
    search.dispatchEvent(new Event("input"));
    await vi.advanceTimersByTimeAsync(300);
    expect(api.get).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(200);
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(params(1)).toMatchObject({ q: "dbatomic", level: "warning" });
    expect(text()).toContain("No matching entries in the log.");
  });

  it("adds new entries at the top while following", async () => {
    api.get.mockResolvedValueOnce(page({ lines: [line(400, "earlier")], end: 1000 }));
    const wrapper = await open();
    await vi.advanceTimersByTimeAsync(10000);
    expect(api.get).toHaveBeenCalledTimes(1);

    api.get.mockResolvedValue(page({ lines: [line(1000, "just now")], end: 1500 }));
    (wrapper.vm as any).follow = true;
    await vi.advanceTimersByTimeAsync(3000);

    expect(params(1)).toMatchObject({ after: 1000, level: "warning" });
    const shown = text();
    expect(shown.indexOf("just now")).toBeGreaterThan(-1);
    expect(shown.indexOf("just now")).toBeLessThan(shown.indexOf("earlier"));

    api.get.mockResolvedValue(page({ end: 1500 }));
    await vi.advanceTimersByTimeAsync(3000);
    expect(params(2).after).toBe(1500);

    await wrapper.setProps({ visible: false });
    await vi.advanceTimersByTimeAsync(10000);
    expect(api.get).toHaveBeenCalledTimes(3);
  });

  it("doesn't follow everything the node writes", async () => {
    api.get.mockResolvedValue(page({ end: 1000 }));
    const wrapper = await open();
    const vm = wrapper.vm as any;
    const followSwitch = () => input("Follow");
    const everything = { value: "info", title: "Everything" };

    expect(followSwitch().disabled).toBe(false);
    vm.follow = true;
    await flushPromises();
    expect(vm.levelProps(everything).disabled).toBe(true);
    expect(vm.levelProps({ value: "error", title: "Errors" }).disabled).toBe(false);

    vm.follow = false;
    vm.level = "info";
    await flushPromises();
    expect(vm.levelProps(everything).disabled).toBe(false);
    expect(followSwitch().disabled).toBe(true);
  });

  it("starts over when the node begins a new log", async () => {
    api.get.mockResolvedValueOnce(page({ lines: [line(400, "old file")], end: 1000 }));
    const wrapper = await open();
    api.get.mockResolvedValueOnce(page({ reset: true }));
    api.get.mockResolvedValueOnce(page({ lines: [line(0, "new file")], end: 300 }));

    (wrapper.vm as any).follow = true;
    await vi.advanceTimersByTimeAsync(3000);

    expect(params(2).after).toBeUndefined();
    expect(text()).toContain("new file");
    expect(text()).not.toContain("old file");
  });

  it("shows an entry's full JSON when clicked", async () => {
    api.get.mockResolvedValue(page({ lines: [line(900, "disk is slow")], end: 1000 }));
    await open();
    expect(text()).not.toContain('"level": "warning"');

    document.body.querySelector<HTMLElement>(".entry-head")!.click();
    await flushPromises();

    expect(document.body.querySelector("pre")?.textContent).toBe(
      '{\n  "level": "warning",\n  "msg": "disk is slow"\n}'
    );
  });

  it("says so when the node has no log", async () => {
    api.get.mockResolvedValue(page({ found: false }));

    await open();

    expect(text()).toContain("This node has no node.log yet");
    expect(button("Download")?.disabled).toBe(true);
  });

  it("reports a failed load", async () => {
    api.get.mockRejectedValue(new Error("boom"));

    await open();

    expect(setSnackbar).toHaveBeenCalledExactlyOnceWith(
      "Loading the node log failed: boom",
      "error"
    );
  });
});
