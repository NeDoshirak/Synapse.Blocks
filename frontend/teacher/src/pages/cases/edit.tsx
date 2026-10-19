import { Alert, Button, Card, Input, message, QRCode, Space, Typography } from "antd";
import { QrcodeOutlined } from "@ant-design/icons";
import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { CaseSectionsEditor } from "../../components/cases/CaseSectionsEditor";
import { CaseReport as CaseReportView } from "../../components/reports/CaseReport";
import { canPublishCase, createCaseSection } from "../../resources/caseOrder";
import { apiClient } from "../../providers/apiClient";
import type { CaseProgressionMode, CaseReport, CaseSectionDraft, MarkdownPage, TeacherCase, TeacherLevel } from "../../types/api";

function toDrafts(item: TeacherCase): CaseSectionDraft[] {
  return item.sections
    .slice()
    .sort((a, b) => a.order - b.order)
    .map(section => ({
      clientId: section.id,
      title: section.title ?? "",
      items: section.items.slice().sort((a, b) => a.order - b.order).map(content => ({
        clientId: content.id,
        kind: content.kind,
        ...(content.levelVersionId ? { levelVersionId: content.levelVersionId } : {}),
        ...(content.markdownPageVersionId ? { markdownPageVersionId: content.markdownPageVersionId } : {}),
        displayTitle: content.displayTitle ?? "",
        canonicalTitle: content.title,
        isBuiltIn: content.isBuiltIn,
      })),
    }));
}

export default function CaseEdit({ create = false }: { create?: boolean }) {
  const { id } = useParams();
  const navigate = useNavigate();
  const [levels, setLevels] = useState<TeacherLevel[]>([]);
  const [markdownPages, setMarkdownPages] = useState<MarkdownPage[]>([]);
  const [item, setItem] = useState<TeacherCase>();
  const [report, setReport] = useState<CaseReport>();
  const [reportError, setReportError] = useState("");
  const [reportLoading, setReportLoading] = useState(true);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [sections, setSections] = useState<CaseSectionDraft[]>(() => [createCaseSection()]);
  const [progressionMode, setProgressionMode] = useState<CaseProgressionMode>("Sequential");
  const [shareUrl, setShareUrl] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    void apiClient.request("/api/teacher/levels").then(value => setLevels(value as TeacherLevel[]));
    void apiClient.request("/api/teacher/markdown-pages/").then(value => setMarkdownPages(value as MarkdownPage[]));
    if (!create && id) {
      void apiClient.request(`/api/teacher/cases/${id}`).then(value => {
        const loaded = value as TeacherCase;
        setItem(loaded);
        setTitle(loaded.title);
        setDescription(loaded.description);
        setSections(toDrafts(loaded));
        setProgressionMode(loaded.progressionMode);
      });
    }
  }, [create, id]);

  useEffect(() => {
    if (!id || create) return;
    setReportLoading(true);
    void apiClient.request(`/api/teacher/cases/${id}/report`)
      .then(value => setReport(value as CaseReport))
      .catch((reason: unknown) => setReportError(reason instanceof Error ? reason.message : "Не удалось загрузить отчёт"))
      .finally(() => setReportLoading(false));
  }, [id, create, item?.isPublished]);

  const canSave = useMemo(() => canPublishCase(title, sections), [title, sections]);
  const buildRequest = () => ({
      caseType: "orderedLevels",
      title,
      description,
      progressionMode,
      sections: sections.map(section => ({
        title: section.title.trim() || null,
        items: section.items.map(content => ({
          kind: content.kind,
          levelVersionId: content.levelVersionId ?? null,
          markdownPageVersionId: content.markdownPageVersionId ?? null,
          displayTitle: content.displayTitle.trim() || null,
        })),
      })),
    });

  const persist = async () => {
    const body = buildRequest();
    const response = item
      ? await apiClient.request(`/api/teacher/cases/${item.id}`, { method: "PUT", body: JSON.stringify(body) })
      : await apiClient.request("/api/teacher/cases", { method: "POST", body: JSON.stringify(body) });
    return response as TeacherCase;
  };

  const save = async () => {
    setError("");
    try {
      const saved = await persist();
      setItem(saved);
      setSections(toDrafts(saved));
      setProgressionMode(saved.progressionMode);
      if (create) navigate(`/cases/${saved.id}`, { replace: true });
      else message.success("Набор сохранён");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось сохранить набор");
    }
  };

  const publish = async () => {
    if (!item) return;
    try {
      const saved = await persist();
      const published = await apiClient.request(`/api/teacher/cases/${saved.id}/publish`, { method: "POST" }) as TeacherCase;
      setItem(published);
      setSections(toDrafts(published));
      setProgressionMode(published.progressionMode);
      message.success("Набор опубликован");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось опубликовать");
    }
  };

  const share = async () => {
    if (!item) return;
    try {
      const result = await apiClient.request(`/api/teacher/cases/${item.id}/share-link`, { method: "POST" }) as { url: string };
      setShareUrl(new URL(result.url, window.location.origin).href);
      message.success("QR-ссылка готова. Скопируйте её сейчас.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось создать ссылку");
    }
  };

  const revoke = async () => {
    if (!item) return;
    await apiClient.request(`/api/teacher/cases/${item.id}/share-link`, { method: "DELETE" });
    setShareUrl("");
    setItem({ ...item, shareLinkActive: false });
    message.success("QR-ссылка закрыта. История попыток сохранена.");
  };

  const archive = async () => {
    if (!item) return;
    await apiClient.request(`/api/teacher/cases/${item.id}`, { method: "DELETE" });
    navigate("/cases");
  };

  const restore = async () => {
    if (!item) return;
    await apiClient.request(`/api/teacher/cases/${item.id}/restore`, { method: "POST" });
    setItem({ ...item, archived: false });
    message.success("Набор восстановлен как неопубликованный");
  };

  return <>
    <Button type="link" href="/admin/cases">← Все наборы</Button>
    <Typography.Title level={2}>{create ? "Новый набор" : title || "Набор уровней"}</Typography.Title>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {item?.archived && <Alert type="warning" showIcon message="Набор в архиве. Попытки и отчёты сохранены, QR-ссылка закрыта." style={{ marginBottom: 16 }} />}
    <Card title="Содержание набора">
      <Space direction="vertical" style={{ width: "100%" }} size="middle">
        <Input size="large" placeholder="Название набора" value={title} disabled={item?.archived} onChange={event => setTitle(event.target.value)} />
        <Input.TextArea rows={2} placeholder="Описание для учеников" value={description} disabled={item?.archived} onChange={event => setDescription(event.target.value)} />
        <CaseSectionsEditor levels={levels} markdownPages={markdownPages} sections={sections} progressionMode={progressionMode} onProgressionModeChange={setProgressionMode} disabled={item?.archived} onChange={setSections} />
        <Space wrap>
          {item?.archived
            ? <Button type="primary" onClick={restore}>Восстановить набор</Button>
            : <>
              <Button type="primary" onClick={save} disabled={!canSave}>Сохранить набор</Button>
              {item && <Button onClick={publish} disabled={!canSave || item.isPublished}>Опубликовать</Button>}
              {item && <Button danger onClick={archive}>В архив</Button>}
            </>}
        </Space>
        {item && <Typography.Text type="secondary">{item.archived ? "Архив · история попыток сохранена" : item.isPublished ? "Опубликован" : "Черновик"} {item.isPublished && !item.archived && "· опубликованные версии заданий закреплены"}</Typography.Text>}
      </Space>
    </Card>

    {item && !item.archived && <Card className="case-qr-card" title="QR-код" style={{ marginTop: 20 }}>
      <Space direction="vertical">
        {shareUrl
          ? <>
            <QRCode value={shareUrl} size={210} />
            <Input readOnly value={shareUrl} />
            <Space>
              <Button onClick={() => navigator.clipboard.writeText(shareUrl).then(() => message.success("Ссылка скопирована"))}>Копировать ссылку</Button>
              <Button onClick={() => window.print()}>Распечатать QR</Button>
            </Space>
            <Typography.Text type="secondary">Ссылка показывается один раз. Сохраните её или распечатайте QR-код.</Typography.Text>
          </>
          : <>
            <Button type="primary" icon={<QrcodeOutlined />} onClick={share} disabled={!item.isPublished}>{item.shareLinkActive ? "Перевыпустить QR и показать новую ссылку" : "Создать QR"}</Button>
            {item.shareLinkActive && <Alert type="warning" message="Активная ссылка была создана раньше и больше не доступна. Перевыпуск отключит предыдущий QR." />}
          </>}
        {item.shareLinkActive && <Button danger onClick={revoke}>Закрыть QR</Button>}
      </Space>
    </Card>}

    {item && <Card title="Результаты учеников" style={{ marginTop: 20 }}>
      {reportLoading
        ? <Typography.Text type="secondary">Загружаем результаты…</Typography.Text>
        : reportError
          ? <Alert type="error" message={reportError} />
          : report
            ? <CaseReportView report={report} itemCount={item.itemCount} />
            : <Typography.Text type="secondary">Попыток пока нет</Typography.Text>}
    </Card>}
  </>;
}
