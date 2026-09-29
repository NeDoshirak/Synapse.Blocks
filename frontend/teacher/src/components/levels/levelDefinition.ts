import type { LevelDefinition } from "../../types/api";

export function makeLevelUpdate(previous: LevelDefinition, edited: LevelDefinition, expectedVersionId: string) {
  return { expectedVersionId, definition: { ...previous, ...edited, id: previous.id, tests: edited.tests.map(test => ({ ...previous.tests.find(old => old.id === test.id), ...test })) } };
}

export function cloneLevelDefinition(template: LevelDefinition): LevelDefinition {
  const clone = structuredClone(template);
  clone.id = crypto.randomUUID();
  clone.title = `${template.title} — копия`;
  clone.tests = clone.tests.map(test => ({ ...test, id: crypto.randomUUID() }));
  clone.introSteps = clone.introSteps.map(step => ({ ...step, id: crypto.randomUUID() }));
  return clone;
}
