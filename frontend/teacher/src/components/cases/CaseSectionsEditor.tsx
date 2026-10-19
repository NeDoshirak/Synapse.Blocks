import { Button, Card, Input, Select, Space, Typography } from "antd";
import { useState } from "react";
import type { CaseSectionDraft, TeacherLevel } from "../../types/api";
import {
  addSectionLevel,
  createCaseSection,
  moveSection,
  moveSectionLevel,
  moveSectionLevelTo,
  removeEmptySection,
  removeSectionLevel,
} from "../../resources/caseOrder";

interface Props {
  levels: TeacherLevel[];
  sections: CaseSectionDraft[];
  canonicalTitleByVersion?: Readonly<Record<string, string>>;
  disabled?: boolean;
  onChange: (sections: CaseSectionDraft[]) => void;
}

export function CaseSectionsEditor({ levels, sections, canonicalTitleByVersion = {}, disabled = false, onChange }: Props) {
  const [pendingVersionBySection, setPendingVersionBySection] = useState<Record<string, string>>({});
  const selectedIds = new Set(sections.flatMap(section => section.levels.map(level => level.levelVersionId)));
  const levelByVersion = new Map<string, TeacherLevel>(levels.map(level => [level.currentVersionId, level] as const));

  return <Space direction="vertical" size="middle" style={{ width: "100%" }}>
    {sections.map((section, sectionIndex) => <Card
      key={section.clientId}
      size="small"
      title={<Space><Typography.Text strong>Раздел {sectionIndex + 1}</Typography.Text>{section.title.trim() && <Typography.Text type="secondary">· {section.title}</Typography.Text>}</Space>}
      extra={<Space size="small">
        <Button type="link" aria-label="Переместить раздел выше" disabled={disabled || sectionIndex === 0} onClick={() => onChange(moveSection(sections, sectionIndex, -1))}>↑</Button>
        <Button type="link" aria-label="Переместить раздел ниже" disabled={disabled || sectionIndex === sections.length - 1} onClick={() => onChange(moveSection(sections, sectionIndex, 1))}>↓</Button>
        <Button type="link" danger disabled={disabled || section.levels.length > 0 || sections.length === 1} onClick={() => onChange(removeEmptySection(sections, section.clientId))}>Удалить пустой</Button>
      </Space>}
    >
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Input
          value={section.title}
          maxLength={120}
          disabled={disabled}
          placeholder="Название раздела (необязательно)"
          onChange={event => onChange(sections.map(item => item.clientId === section.clientId ? { ...item, title: event.target.value } : item))}
        />
        <Select
          showSearch
          allowClear
          value={pendingVersionBySection[section.clientId] ?? ""}
          disabled={disabled || !levels.length}
          placeholder="Добавить задание в этот раздел"
          optionFilterProp="label"
          options={levels.map(level => ({
            value: level.currentVersionId,
            label: `${level.title} · v${level.version}`,
            disabled: selectedIds.has(level.currentVersionId),
          }))}
          onChange={versionId => {
            if (!versionId) return;
            const level = levelByVersion.get(versionId);
            if (level) {
              onChange(addSectionLevel(sections, section.clientId, { levelVersionId: versionId, displayTitle: "" }));
              setPendingVersionBySection(current => ({ ...current, [section.clientId]: "" }));
            }
          }}
        />
        {section.levels.length === 0
          ? <Typography.Text type="secondary">В разделе пока нет заданий</Typography.Text>
          : <Space direction="vertical" size="small" style={{ width: "100%" }}>
            {section.levels.map((assignment, index) => {
              const level = levelByVersion.get(assignment.levelVersionId);
              const canonicalTitle = level?.title ?? canonicalTitleByVersion[assignment.levelVersionId] ?? "Недоступное задание";
              return <Card key={assignment.levelVersionId} size="small" styles={{ body: { padding: 12 } }}>
                <Space direction="vertical" size="small" style={{ width: "100%" }}>
                  <Space wrap style={{ width: "100%", justifyContent: "space-between" }}>
                    <Typography.Text strong>{index + 1}. {canonicalTitle}{level ? ` · v${level.version}` : ""}</Typography.Text>
                    <Space size="small">
                      <Button type="link" aria-label="Переместить задание выше" disabled={disabled || index === 0} onClick={() => onChange(moveSectionLevel(sections, section.clientId, index, -1))}>↑</Button>
                      <Button type="link" aria-label="Переместить задание ниже" disabled={disabled || index === section.levels.length - 1} onClick={() => onChange(moveSectionLevel(sections, section.clientId, index, 1))}>↓</Button>
                      {sections.length > 1 && <Select
                        size="small"
                        value={undefined}
                        disabled={disabled}
                        placeholder="Переместить в раздел…"
                        style={{ minWidth: 190 }}
                        options={sections.filter(item => item.clientId !== section.clientId).map(item => ({
                          value: item.clientId,
                          label: item.title.trim() || `Раздел ${sections.indexOf(item) + 1}`,
                        }))}
                        onChange={targetId => onChange(moveSectionLevelTo(sections, section.clientId, targetId, assignment.levelVersionId))}
                      />}
                      <Button type="link" danger disabled={disabled} onClick={() => onChange(removeSectionLevel(sections, section.clientId, assignment.levelVersionId))}>Убрать</Button>
                    </Space>
                  </Space>
                  <Input
                    value={assignment.displayTitle}
                    maxLength={200}
                    disabled={disabled}
                    placeholder="Название для учеников (по умолчанию исходное)"
                    onChange={event => onChange(sections.map(item => item.clientId === section.clientId
                      ? { ...item, levels: item.levels.map(row => row.levelVersionId === assignment.levelVersionId ? { ...row, displayTitle: event.target.value } : row) }
                      : item))}
                  />
                </Space>
              </Card>;
            })}
          </Space>}
      </Space>
    </Card>)}
    <Button disabled={disabled} onClick={() => onChange([...sections, createCaseSection()])}>+ Добавить раздел</Button>
  </Space>;
}
