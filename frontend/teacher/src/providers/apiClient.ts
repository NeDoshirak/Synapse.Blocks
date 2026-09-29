type ApiOptions = RequestInit & { headers?: HeadersInit };
let requestToken: string | undefined;

export function createApiClient() {
  const csrf = async () => {
    if (!requestToken) {
      const response = await fetch("/api/auth/csrf", { credentials: "include" });
      if (!response.ok) throw new Error("Не удалось подготовить безопасное соединение.");
      requestToken = (await response.json() as { requestToken: string }).requestToken;
    }
    return requestToken;
  };
  return {
    async request(url: string, options: ApiOptions = {}) {
      const method = (options.method ?? "GET").toUpperCase();
      const headers = new Headers(options.headers);
      if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
      if (!["GET", "HEAD", "OPTIONS"].includes(method)) headers.set("RequestVerificationToken", await csrf());
      const response = await fetch(url, { ...options, headers, credentials: "include" });
      if (response.status === 204) return undefined;
      const body = await response.json().catch(() => undefined);
      if (!response.ok) {
        if (response.status === 401) requestToken = undefined;
        throw Object.assign(new Error(body?.message ?? body?.title ?? `Ошибка запроса (${response.status})`), { statusCode: response.status, errors: body?.errors });
      }
      return body;
    },
    invalidateCsrf() { requestToken = undefined; },
  };
}

export const apiClient = createApiClient();
