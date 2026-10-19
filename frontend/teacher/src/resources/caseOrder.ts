import type { CaseContentItemDraft, CaseSectionDraft } from "../types/api";

function moveItems<T>(items: T[], index: number, direction: -1 | 1) {
  const next = [...items];
  const target = index + direction;
  if (index < 0 || index >= next.length || target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target]!, next[index]!];
  return next;
}

export function addLevel(selected: string[], versionId: string) {
  return selected.includes(versionId) ? selected : [...selected, versionId];
}

export function moveLevel(selected: string[], index: number, direction: -1 | 1) {
  return moveItems(selected, index, direction);
}

export function canPublishCase(title: string, selected: string[] | CaseSectionDraft[]) {
  if (selected.length === 0 || typeof selected[0] === "string") {
    const versionIds = selected as string[];
    return title.trim().length > 0 && versionIds.length > 0 && versionIds.length <= 100 && new Set(versionIds).size === versionIds.length;
  }
  const sections = selected as CaseSectionDraft[];
  const items = sections.flatMap(section => section.items);
  const levelIds = items.flatMap(item => item.kind === "level" && item.levelVersionId ? [item.levelVersionId] : []);
  return title.trim().length > 0 && items.length > 0 && items.length <= 100 && new Set(levelIds).size === levelIds.length;
}

export function createCaseSection(): CaseSectionDraft {
  return { clientId: crypto.randomUUID(), title: "", items: [] };
}

export function moveSection(sections: CaseSectionDraft[], index: number, direction: -1 | 1) {
  return moveItems(sections, index, direction);
}

export function addSectionItem(sections: CaseSectionDraft[], sectionId: string, item: Omit<CaseContentItemDraft, "clientId">) {
  if (item.kind === "level" && item.levelVersionId && sections.some(section => section.items.some(existing => existing.kind === "level" && existing.levelVersionId === item.levelVersionId))) return sections;
  return sections.map(section => section.clientId === sectionId
    ? { ...section, items: [...section.items, { ...item, clientId: crypto.randomUUID() }] }
    : section);
}

export function updateSectionItem(sections: CaseSectionDraft[], sectionId: string, clientId: string, update: Partial<CaseContentItemDraft>) {
  return sections.map(section => section.clientId === sectionId
    ? { ...section, items: section.items.map(item => item.clientId === clientId ? { ...item, ...update } : item) }
    : section);
}

export function removeSectionItem(sections: CaseSectionDraft[], sectionId: string, clientId: string) {
  return sections.map(section => section.clientId === sectionId
    ? { ...section, items: section.items.filter(item => item.clientId !== clientId) }
    : section);
}

export function moveSectionItem(sections: CaseSectionDraft[], sectionId: string, index: number, direction: -1 | 1) {
  return sections.map(section => section.clientId === sectionId
    ? { ...section, items: moveItems(section.items, index, direction) }
    : section);
}

export function moveSectionItemTo(sections: CaseSectionDraft[], fromId: string, toId: string, clientId: string) {
  if (fromId === toId) return sections;
  const source = sections.find(section => section.clientId === fromId);
  const item = source?.items.find(row => row.clientId === clientId);
  if (!item || !sections.some(section => section.clientId === toId)) return sections;
  return sections.map(section => {
    if (section.clientId === fromId) return { ...section, items: section.items.filter(row => row.clientId !== clientId) };
    if (section.clientId === toId) return { ...section, items: [...section.items, item] };
    return section;
  });
}

export function removeEmptySection(sections: CaseSectionDraft[], sectionId: string) {
  if (sections.length <= 1 || sections.find(section => section.clientId === sectionId)?.items.length) return sections;
  return sections.filter(section => section.clientId !== sectionId);
}
