import { describe, expect, it, vi } from "vitest";
import { createApiClient } from "./apiClient";

describe("same-origin API client", () => {
  it("sends cookies and an antiforgery token for mutations", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: "csrf+value" }), { status: 200 })).mockResolvedValueOnce(new Response("{}", { status: 200 }));
    await createApiClient().request("/api/teacher/levels", { method: "POST", body: JSON.stringify({ title: "x" }) });
    expect(fetchMock.mock.calls.at(-1)?.[0]).toBe("/api/teacher/levels");
    expect(fetchMock.mock.calls.at(-1)?.[1]?.credentials).toBe("include");
    expect(new Headers(fetchMock.mock.calls.at(-1)?.[1]?.headers).get("RequestVerificationToken")).toBe("csrf+value");
  });
});
