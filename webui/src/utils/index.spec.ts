import { afterEach, describe, expect, it, vi } from "vitest";
import {
  b64,
  effectiveResetDate,
  errorMessage,
  formatAddr,
  isNetworkError,
} from "./index";

// The test page's origin (see vite.config.mts), which is also the FUNC API's.
const funcOrigin = "http://localhost:3536";

describe("formatAddr", () => {
  const addr = "ABCDE" + "X".repeat(48) + "VWXYZ";

  it("shortens an address to its ends", () => {
    expect(formatAddr(addr)).toBe("ABCDE...VWXYZ");
    expect(formatAddr(addr, 3)).toBe("ABC...XYZ");
  });

  it("returns an empty string without an address", () => {
    expect(formatAddr(undefined)).toBe("");
    expect(formatAddr("")).toBe("");
  });
});

describe("b64", () => {
  it("encodes bytes as base64", () => {
    expect(b64(new Uint8Array([1, 2, 3]))).toBe("AQID");
    expect(b64(undefined)).toBeUndefined();
  });
});

describe("effectiveResetDate", () => {
  afterEach(() => vi.useRealTimers());

  it("recomputes relative modes from the current date", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 5, 17, 12));
    const date = "stored";

    expect(effectiveResetDate({ date, mode: "year" })).toBe(
      new Date(2026, 0, 1).toLocaleDateString()
    );
    expect(effectiveResetDate({ date, mode: "month" })).toBe(
      new Date(2026, 5, 1).toLocaleDateString()
    );
    expect(effectiveResetDate({ date, mode: "today" })).toBe(
      new Date(2026, 5, 17).toLocaleDateString()
    );
  });

  it("keeps a custom date as stored", () => {
    expect(effectiveResetDate({ date: "1/2/2026", mode: "custom" })).toBe(
      "1/2/2026"
    );
    expect(effectiveResetDate({ date: "1/2/2026" })).toBe("1/2/2026");
  });

  it("returns undefined without an entry", () => {
    expect(effectiveResetDate()).toBeUndefined();
    expect(effectiveResetDate({ date: "", mode: "year" })).toBeUndefined();
  });
});

describe("isNetworkError", () => {
  it.each([
    { code: "ERR_NETWORK" },
    { code: "ECONNREFUSED" },
    { name: "AbortError" },
    { name: "TimeoutError" },
    { message: "Failed to fetch" },
    { message: "Network Error" },
    { message: "Load failed" },
  ])("is true for %o", (err) => {
    expect(isNetworkError(err)).toBe(true);
  });

  it.each([
    null,
    undefined,
    { message: "Request failed with status code 400" },
    // Reached and answered, whatever the message says.
    { message: "Network Error", response: { status: 502 } },
    { message: "Failed to fetch", status: 500 },
  ])("is false for %o", (err) => {
    expect(isNetworkError(err)).toBe(false);
  });
});

describe("errorMessage", () => {
  const unreachable = { code: "ERR_NETWORK", message: "Network Error" };

  it("names the FUNC service when it can't be reached", () => {
    const err = { ...unreachable, config: { url: "algorand", baseURL: funcOrigin } };

    expect(errorMessage(err, "Start")).toBe(
      `Start failed: no response from the FUNC service (${funcOrigin}) - check that it is running and not blocked by a firewall or network issue.`
    );
  });

  it("names another origin by its address", () => {
    const err = { ...unreachable, config: { url: "https://example.org/x" } };

    expect(errorMessage(err, "Fetch")).toContain(
      "no response from https://example.org -"
    );
  });

  it("prefers the caller's description of the target", () => {
    const err = { ...unreachable, config: { url: "/v2/status", baseURL: funcOrigin } };

    expect(errorMessage(err, "Status", "the node")).toContain(
      "no response from the node -"
    );
  });

  it("falls back to a generic target", () => {
    expect(errorMessage(new TypeError("Failed to fetch"), "Status")).toContain(
      "no response from the server -"
    );
  });

  it("asks for a sign-in on 401", () => {
    const err = { response: { status: 401, data: "Sign in required" } };

    expect(errorMessage(err, "Stop")).toBe("Stop failed: sign in to FUNC first.");
  });

  it.each([
    [{ response: { status: 400, data: "  Changing Archival is not supported " } }, "Changing Archival is not supported"],
    [{ response: { status: 400, data: { message: "bad round" } } }, "bad round"],
    [{ response: { status: 400, data: { error: "no such key" } } }, "no such key"],
    [{ response: { status: 400, data: { code: 7 } } }, '{"code":7}'],
    [{ response: { status: 500, data: "" }, message: "Request failed" }, "Request failed"],
    [new Error("boom"), "boom"],
    ["plain text", "plain text"],
  ])("uses the server's detail: %#", (err, detail) => {
    expect(errorMessage(err, "Save")).toBe(`Save failed: ${detail}`);
  });
});
