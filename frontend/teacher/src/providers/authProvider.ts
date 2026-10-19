import { apiClient } from "./apiClient";
import type { AuthStep, CurrentUser } from "../types/api";

export const authProvider = {
  login: async ({ email, password }: { email: string; password: string }) => {
    try {
      const result = await apiClient.request("/api/auth/sign-in", { method: "POST", body: JSON.stringify({ email, password }) }) as AuthStep;
      apiClient.invalidateCsrf();
      if (result.nextStep === "twoFactor") return { success: true, redirectTo: "/two-factor-challenge" };
      if (result.nextStep === "authenticatorSetup") return { success: true, redirectTo: "/authenticator-setup" };
      if (result.nextStep === "authenticated") return { success: true, redirectTo: "/" };
      return { success: false, error: new Error("Не удалось завершить вход") };
    } catch { return { success: false, error: new Error("Проверьте почту и пароль") }; }
  },
  logout: async () => {
    try { await apiClient.request("/api/auth/sign-out", { method: "POST" }); } finally { apiClient.invalidateCsrf(); }
    return { success: true, redirectTo: "/login" };
  },
  check: async () => {
    try {
      const user = await apiClient.request("/api/auth/me") as CurrentUser;
      return user.twoFactorEnabled
        ? { authenticated: true }
        : { authenticated: false, redirectTo: "/authenticator-setup" };
    }
    catch { return { authenticated: false, redirectTo: "/login", logout: true }; }
  },
  getIdentity: async (): Promise<CurrentUser> => {
    const user = await apiClient.request("/api/auth/me") as CurrentUser;
    return { ...user, id: user.userId };
  },
  onError: async (error: { statusCode?: number }) => error.statusCode === 401 ? { logout: true, redirectTo: "/login" } : {},
};
