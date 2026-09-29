import { beforeEach, describe, expect, it, vi } from "vitest";
import { authProvider } from "./authProvider";
import { apiClient } from "./apiClient";

const response = (status: number, body?: unknown) => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

describe("teacher cookie authentication", () => {
  beforeEach(() => { vi.restoreAllMocks(); apiClient.invalidateCsrf(); });
  it("signs in with same-origin cookie credentials", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(response(200, { requestToken: "csrf" })).mockResolvedValueOnce(response(200));
    await expect(authProvider.login({ email: "teacher@example.test", password: "secret" })).resolves.toEqual({ success: true, redirectTo: "/" });
    expect(fetchMock.mock.calls.at(-1)?.[0]).toBe("/api/auth/sign-in");
    expect(fetchMock.mock.calls.at(-1)?.[1]?.credentials).toBe("include");
    expect(new Headers(fetchMock.mock.calls.at(-1)?.[1]?.headers).get("RequestVerificationToken")).toBe("csrf");
  });
  it("returns a recoverable sign-in error", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(response(200, { requestToken: "csrf" })).mockResolvedValueOnce(response(401, { message: "Invalid credentials" }));
    await expect(authProvider.login({ email: "a", password: "b" })).resolves.toMatchObject({ success: false });
  });
  it("redirects expired sessions to login", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(response(401));
    await expect(authProvider.check()).resolves.toEqual({ authenticated: false, redirectTo: "/login", logout: true });
  });
  it("loads the current identity and signs out", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(response(200, { userId: "u1", email: "t@example.test", roles: ["Teacher"] })).mockResolvedValueOnce(response(200, { requestToken: "csrf" })).mockResolvedValueOnce(response(204));
    await expect(authProvider.getIdentity?.()).resolves.toMatchObject({ id: "u1", email: "t@example.test" });
    await expect(authProvider.logout()).resolves.toMatchObject({ success: true });
    expect(fetchMock.mock.calls[2]?.[0]).toBe("/api/auth/sign-out");
  });
});
