import type { DataProvider } from "@refinedev/core";
import { apiClient } from "./apiClient";

const root = (resource: string) => `/api/teacher/${resource}`;
export const dataProvider: DataProvider = {
  getList: async ({ resource }) => ({ data: await apiClient.request(root(resource)) as never[], total: 0 }),
  getOne: async ({ resource, id }) => ({ data: await apiClient.request(`${root(resource)}/${id}`) as never }),
  create: async ({ resource, variables }) => ({ data: await apiClient.request(root(resource), { method: "POST", body: JSON.stringify(variables) }) as never }),
  update: async ({ resource, id, variables }) => ({ data: await apiClient.request(`${root(resource)}/${id}`, { method: "PUT", body: JSON.stringify(variables) }) as never }),
  deleteOne: async ({ resource, id }) => ({ data: await apiClient.request(`${root(resource)}/${id}`, { method: "DELETE" }) as never }),
  getApiUrl: () => "/api/teacher",
};
