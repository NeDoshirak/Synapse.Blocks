import type { CaseLevelDraft, CaseSectionDraft } from "../types/api";

// Legacy flat helpers remain available for other callers while the editor uses section helpers below.
export function addLevel(selected: string[], versionId: string) {
  return selected.includes(versionId) ? selected : [...selected, versionId];
}

export function moveLevel(selected: string[], index: number, direction: -1 | 1) {
  return moveItems(selected, index, direction);
}

function moveItems<T>(items: T[], index: number, direction: -1 | 1) {
  const next = [...items];
  const target = index + direction;
  if (index < 0 || index >= next.length || target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target]!, next[index]!];
  return next;
}

export function canPublishCase(title: string, selected: string[] | CaseSectionDraft[]) {
  const versionIds = Array.isArray(selected) && typeof selected[0] === "string"
    ? selected as string[]
    : (selected as CaseSectionDraft[]).flatMap(section => section.levels.map(level => level.levelVersionId));
  return title.trim().length > 0 && versionIds.length > 0 && versionIds.length <= 100 && new Set(versionIds).size === versionIds.length;
}

export function createCaseSection(): CaseSectionDraft {
  return { clientId: crypto.randomUUID(), title: "", levels: [] };
}

export function moveSection(sections: CaseSectionDraft[], index: number, direction: -1 | 1) {
  const next = [...sections];
  const target = index + direction;
  if (index < 0 || index >= next.length || target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target]!, next[index]!];
  return next;
}

export function addSectionLevel(sections: CaseSectionDraft[], sectionId: string, level: CaseLevelDraft) {
  if (sections.some(section => section.levels.some(item => item.levelVersionId === level.levelVersionId))) return sections;
  return sections.map(section => section.clientId === sectionId ? { ...section, levels: [...section.levels, level] } : section);
}

export function removeSectionLevel(sections: CaseSectionDraft[], sectionId: string, versionId: string) {
  return sections.map(section => section.clientId === sectionId
    ? { ...section, levels: section.levels.filter(level => level.levelVersionId !== versionId) }
    : section);
}

export function moveSectionLevel(sections: CaseSectionDraft[], sectionId: string, index: number, direction: -1 | 1) {
  return sections.map(section => section.clientId === sectionId
    ? { ...section, levels: moveItems(section.levels, index, direction) }
    : section);
}

export function moveSectionLevelTo(sections: CaseSectionDraft[], fromId: string, toId: string, versionId: string) {
  if (fromId === toId) return sections;
  const source = sections.find(section => section.clientId === fromId);
  const assignment = source?.levels.find(level => level.levelVersionId === versionId);
  if (!assignment || !sections.some(section => section.clientId === toId)) return sections;
  return sections.map(section => {
    if (section.clientId === fromId) return { ...section, levels: section.levels.filter(level => level.levelVersionId !== versionId) };
    if (section.clientId === toId) return { ...section, levels: [...section.levels, assignment] };
    return section;
  });
}

export function removeEmptySection(sections: CaseSectionDraft[], sectionId: string) {
  if (sections.length <= 1 || sections.find(section => section.clientId === sectionId)?.levels.length) return sections;
  return sections.filter(section => section.clientId !== sectionId);
}
