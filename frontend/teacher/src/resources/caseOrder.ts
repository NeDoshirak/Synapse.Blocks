export function addLevel(selected: string[], versionId: string) {
  return selected.includes(versionId) ? selected : [...selected, versionId];
}
export function moveLevel(selected: string[], index: number, direction: -1 | 1) {
  const next = [...selected];
  const target = index + direction;
  if (index < 0 || index >= next.length || target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target]!, next[index]!];
  return next;
}
export function canPublishCase(title: string, selected: string[]) {
  return title.trim().length > 0 && selected.length > 0 && new Set(selected).size === selected.length;
}
