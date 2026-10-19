import { BookOutlined, CodeOutlined, FileMarkdownOutlined } from "@ant-design/icons";
import { Button, Card, Input, Radio, Select, Space, Tag, Typography } from "antd";
import { useState } from "react";
import type { CaseProgressionMode, CaseSectionDraft, MarkdownPage, TeacherLevel } from "../../types/api";
import {
  addSectionItem,
  createCaseSection,
  moveSection,
  moveSectionItem,
  moveSectionItemTo,
  removeEmptySection,
  removeSectionItem,
  updateSectionItem,
} from "../../resources/caseOrder";

interface Props {
  levels: TeacherLevel[];
  markdownPages: MarkdownPage[];
  sections: CaseSectionDraft[];
  progressionMode: CaseProgressionMode;
  disabled?: boolean;
  onChange: (sections: CaseSectionDraft[]) => void;
  onProgressionModeChange: (mode: CaseProgressionMode) => void;
}

export function CaseSectionsEditor({
  levels,
  markdownPages,
  sections,
  progressionMode,
  disabled = false,
  onChange,
  onProgressionModeChange,
}: Props) {
  const [pendingBySection, setPendingBySection] = useState<Record<string, string>>({});
  const selectedLevelIds = new Set(sections.flatMap(section => section.items.flatMap(item => item.kind === "level" && item.levelVersionId ? [item.levelVersionId] : [])));
  const levelByVersion = new Map(levels.map(level => [level.currentVersionId, level] as const));
  const pageByVersion = new Map(markdownPages.map(page => [page.currentVersionId, page] as const));

  const assign = (sectionId: string, kind: "level" | "markdown", versionId?: string) => {
    if (!versionId) return;
    if (kind === "level") {
      const level = levelByVersion.get(versionId);
      if (!level) return;
      onChange(addSectionItem(sections, sectionId, {
        kind,
        levelVersionId: versionId,
        displayTitle: "",
        canonicalTitle: level.title,
      }));
    } else {
      const page = pageByVersion.get(versionId);
      if (!page) return;
      onChange(addSectionItem(sections, sectionId, {
        kind,
        markdownPageVersionId: versionId,
        displayTitle: "",
        canonicalTitle: page.title,
        isBuiltIn: page.isBuiltIn,
      }));
    }
    setPendingBySection(current => ({ ...current, [`${sectionId}:${kind}`]: "" }));
  };

  return <Space direction="vertical" size="middle" style={{ width: "100%" }}>
    <Card size="small" title={<Space><BookOutlined /><Typography.Text strong>Порядок прохождения</Typography.Text></Space>}>
      <Radio.Group
        value={progressionMode}
        disabled={disabled}
        onChange={event => onProgressionModeChange(event.target.value as CaseProgressionMode)}
        options={[
          { value: "Sequential", label: "По порядку" },
          { value: "AllOpen", label: "Все сразу открыты" },
        ]}
      />
      <div><Typography.Text type="secondary">При последовательном режиме следующий шаг открывается после завершения текущего.</Typography.Text></div>
    </Card>

    {sections.map((section, sectionIndex) => <Card
      key={section.clientId}
      size="small"
      title={<Space><Typography.Text strong>Раздел {sectionIndex + 1}</Typography.Text>{section.title.trim() && <Typography.Text type="secondary">· {section.title}</Typography.Text>}</Space>}
      extra={<Space size="small">
        <Button type="link" aria-label="Переместить раздел выше" disabled={disabled || sectionIndex === 0} onClick={() => onChange(moveSection(sections, sectionIndex, -1))}>↑</Button>
        <Button type="link" aria-label="Переместить раздел ниже" disabled={disabled || sectionIndex === sections.length - 1} onClick={() => onChange(moveSection(sections, sectionIndex, 1))}>↓</Button>
        <Button type="link" danger disabled={disabled || section.items.length > 0 || sections.length === 1} onClick={() => onChange(removeEmptySection(sections, section.clientId))}>Удалить пустой</Button>
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
        <Space.Compact block>
          <Select
            showSearch
            allowClear
            value={pendingBySection[`${section.clientId}:level`] || undefined}
            disabled={disabled || !levels.length}
            placeholder="Добавить уровень"
            optionFilterProp="label"
            style={{ minWidth: 220, flex: 1 }}
            options={levels.map(level => ({
              value: level.currentVersionId,
              label: `${level.title} · v${level.version}`,
              disabled: selectedLevelIds.has(level.currentVersionId),
            }))}
            onChange={versionId => {
              setPendingBySection(current => ({ ...current, [`${section.clientId}:level`]: versionId ?? "" }));
              assign(section.clientId, "level", versionId);
            }}
          />
          <Select
            showSearch
            allowClear
            value={pendingBySection[`${section.clientId}:markdown`] || undefined}
            disabled={disabled || !markdownPages.some(page => !page.isArchived)}
            placeholder="Добавить Markdown-страницу"
            optionFilterProp="label"
            style={{ minWidth: 260, flex: 1 }}
            options={markdownPages.filter(page => !page.isArchived).map(page => ({
              value: page.currentVersionId,
              label: `${page.title} · v${page.version}${page.isBuiltIn ? " · демо" : ""}`,
            }))}
            onChange={versionId => {
              setPendingBySection(current => ({ ...current, [`${section.clientId}:markdown`]: versionId ?? "" }));
              assign(section.clientId, "markdown", versionId);
            }}
          />
        </Space.Compact>
        {section.items.length === 0
          ? <Typography.Text type="secondary">В разделе пока нет заданий или страниц</Typography.Text>
          : <Space direction="vertical" size="small" style={{ width: "100%" }}>
            {section.items.map((assignment, index) => {
              const level = assignment.kind === "level" && assignment.levelVersionId ? levelByVersion.get(assignment.levelVersionId) : undefined;
              const page = assignment.kind === "markdown" && assignment.markdownPageVersionId ? pageByVersion.get(assignment.markdownPageVersionId) : undefined;
              const canonicalTitle = level?.title ?? page?.title ?? assignment.canonicalTitle ?? "Недоступный материал";
              const version = level?.version ?? page?.version;
              return <Card key={assignment.clientId} size="small" styles={{ body: { padding: 12 } }}>
                <Space direction="vertical" size="small" style={{ width: "100%" }}>
                  <Space wrap style={{ width: "100%", justifyContent: "space-between" }}>
                    <Space>
                      {assignment.kind === "level" ? <CodeOutlined /> : <FileMarkdownOutlined />}
                      <Typography.Text strong>{index + 1}. {canonicalTitle}{version ? ` · v${version}` : ""}</Typography.Text>
                      {assignment.isBuiltIn && <Tag color="purple">Демо</Tag>}
                      <Tag>{assignment.kind === "level" ? "Уровень" : "Страница"}</Tag>
                    </Space>
                    <Space size="small">
                      <Button type="link" aria-label="Переместить материал выше" disabled={disabled || index === 0} onClick={() => onChange(moveSectionItem(sections, section.clientId, index, -1))}>↑</Button>
                      <Button type="link" aria-label="Переместить материал ниже" disabled={disabled || index === section.items.length - 1} onClick={() => onChange(moveSectionItem(sections, section.clientId, index, 1))}>↓</Button>
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
                        onChange={targetId => { if (targetId) onChange(moveSectionItemTo(sections, section.clientId, targetId, assignment.clientId)); }}
                      />}
                      <Button type="link" danger disabled={disabled} onClick={() => onChange(removeSectionItem(sections, section.clientId, assignment.clientId))}>Убрать</Button>
                    </Space>
                  </Space>
                  <Input
                    value={assignment.displayTitle}
                    maxLength={200}
                    disabled={disabled}
                    placeholder="Название для учеников (пусто — исходное)"
                    onChange={event => onChange(updateSectionItem(sections, section.clientId, assignment.clientId, { displayTitle: event.target.value }))}
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
