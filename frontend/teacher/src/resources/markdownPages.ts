import { apiClient } from "../providers/apiClient";
import type { MarkdownAsset, MarkdownPage, MarkdownPageDetail, MarkdownPageVersion, MarkdownPreview } from "../types/api";

export function listMarkdownPages() {
  return apiClient.request("/api/teacher/markdown-pages/") as Promise<MarkdownPage[]>;
}

export function getMarkdownPage(pageId: string) {
  return apiClient.request(`/api/teacher/markdown-pages/${pageId}`) as Promise<MarkdownPageDetail>;
}

export function createMarkdownPage(title: string, markdown: string) {
  return apiClient.request("/api/teacher/markdown-pages/", {
    method: "POST",
    body: JSON.stringify({ title, markdown }),
  }) as Promise<MarkdownPageDetail>;
}

export function updateMarkdownPage(pageId: string, expectedVersionId: string, title: string, markdown: string) {
  return apiClient.request(`/api/teacher/markdown-pages/${pageId}`, {
    method: "PUT",
    body: JSON.stringify({ expectedVersionId, title, markdown }),
  }) as Promise<MarkdownPageDetail>;
}

export function listMarkdownPageVersions(pageId: string) {
  return apiClient.request(`/api/teacher/markdown-pages/${pageId}/versions`) as Promise<MarkdownPageVersion[]>;
}

export function uploadMarkdownAsset(file: File) {
  const body = new FormData();
  body.append("file", file);
  return apiClient.request("/api/teacher/markdown-assets/", { method: "POST", body }) as Promise<MarkdownAsset>;
}

export function previewMarkdown(markdown: string) {
  return apiClient.request("/api/teacher/markdown-pages/preview", {
    method: "POST",
    body: JSON.stringify({ markdown }),
  }) as Promise<MarkdownPreview>;
}

export function archiveMarkdownPage(pageId: string) {
  return apiClient.request(`/api/teacher/markdown-pages/${pageId}/archive`, { method: "POST" });
}

export function restoreMarkdownPage(pageId: string) {
  return apiClient.request(`/api/teacher/markdown-pages/${pageId}/restore`, { method: "POST" });
}
