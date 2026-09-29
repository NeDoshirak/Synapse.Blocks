import { apiClient } from "./apiClient";
import type { CurrentUser } from "../types/api";

export const authProvider = {
  login: async ({ email, password }: { email: string; password: string }) => {
    try {
      await apiClient.request("/api/auth/sign-in", { method: "POST", body: JSON.stringify({ email, password }) });
      return { success: true, redirectTo: "/" };
    } catch { return { success: false, error: new Error("Проверьте почту и пароль") }; }
  },
  logout: async () => {
    try { await apiClient.request("/api/auth/sign-out", { method: "POST" }); } finally { apiClient.invalidateCsrf(); }
    return { success: true, redirectTo: "/login" };
  },
  check: async () => {
    try { await apiClient.request("/api/auth/me"); return { authenticated: true }; }
    catch { return { authenticated: false, redirectTo: "/login", logout: true }; }
  },
  getIdentity: async (): Promise<CurrentUser> => {
    const user = await apiClient.request("/api/auth/me") as CurrentUser;
    return { ...user, id: user.userId };
  },
  onError: async (error: { statusCode?: number }) => error.statusCode === 401 ? { logout: true, redirectTo: "/login" } : {},
};
