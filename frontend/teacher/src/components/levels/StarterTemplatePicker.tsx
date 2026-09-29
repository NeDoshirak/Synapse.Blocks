import { Select, Typography } from "antd";
import type { TeacherLevel } from "../../types/api";

export function StarterTemplatePicker({ levels, value, onChange }: { levels: TeacherLevel[]; value?: string; onChange: (level?: TeacherLevel) => void }) {
  return <div style={{ marginBottom: 20 }}><Typography.Text strong>Начать с готового уровня (необязательно)</Typography.Text><Select allowClear showSearch optionFilterProp="label" value={value} onChange={id => onChange(levels.find(level => level.id === id))} style={{ display: "block", marginTop: 8 }} placeholder="Пустой уровень" options={levels.map(level => ({ value: level.id, label: `${level.title} · версия ${level.version}` }))} /></div>;
}
