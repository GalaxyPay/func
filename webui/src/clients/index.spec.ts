import { afterEach, describe, expect, it, vi } from "vitest";
import { AlgodFunc } from "./index";
import type { NodeStatus } from "@/types";

afterEach(() => vi.unstubAllGlobals());

describe("AlgodFunc", () => {
  it("reaches algod through FUNC's proxy, keeping the /algod/<network> path", async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ "last-round": 1 }), {
          headers: { "content-type": "application/json" },
        })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new AlgodFunc("Voi", { token: "abc" } as NodeStatus);
    expect(client.baseUrl).toBe("http://localhost:3536/algod/voi");

    await client.status().do().catch(() => undefined);
    expect(fetchMock).toHaveBeenCalled();
    const [url, init] = fetchMock.mock.calls[0] as unknown as [
      string | URL | Request,
      RequestInit,
    ];
    const href = url instanceof Request ? url.url : url.toString();
    expect(href).toBe("http://localhost:3536/algod/voi/v2/status");
    expect(new Headers(init?.headers).get("X-Algo-API-Token")).toBe("abc");
  });
});
