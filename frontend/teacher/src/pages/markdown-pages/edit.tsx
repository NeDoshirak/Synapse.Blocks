import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Button, Card, Input, message, Select, Space, Spin, Typography } from "antd";
import { ArrowLeftOutlined, SaveOutlined } from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";
import { MarkdownSourceEditor, markdownByteLength } from "../../components/markdown/MarkdownSourceEditor";
import { MarkdownPreview } from "../../components/markdown/MarkdownPreview";
import { createMarkdownPage, getMarkdownPage, listMarkdownPageVersions, previewMarkdown, updateMarkdownPage, uploadMarkdownAsset } from "../../resources/markdownPages";
import type { MarkdownPage, MarkdownPageVersion } from "../../types/api";
import "./markdown-pages.css";

const maximumSourceBytes = 100 * 1024;

export default function MarkdownPageEdit({ create = false }: { create?: boolean }) {
  const { id } = useParams();
  const navigate = useNavigate();
  const [page, setPage] = useState<MarkdownPage>();
  const [versions, setVersions] = useState<MarkdownPageVersion[]>([]);
  const [versionId, setVersionId] = useState("");
  const [title, setTitle] = useState("");
  const [markdown, setMarkdown] = useState("");
  const [previewHtml, setPreviewHtml] = useState("");
  const [previewError, setPreviewError] = useState("");
  const [previewLoading, setPreviewLoading] = useState(false);
  const [loading, setLoading] = useState(!create);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const currentVersionId = page?.currentVersionId ?? "";
  const viewingHistory = Boolean(versionId && currentVersionId && versionId !== currentVersionId);
  const readOnly = Boolean(page?.isBuiltIn || page?.isArchived || viewingHistory);
  const sourceBytes = useMemo(() => markdownByteLength(markdown), [markdown]);

  useEffect(() => {
    if (create || !id) return;
    let active = true;
    setLoading(true);
    void Promise.all([getMarkdownPage(id), listMarkdownPageVersions(id)])
      .then(([detail, pageVersions]) => {
        if (!active) return;
        setPage(detail.page);
        setVersions(pageVersions);
        setVersionId(detail.currentVersion.id);
        setTitle(detail.currentVersion.title);
        setMarkdown(detail.currentVersion.markdown);
      })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : "Не удалось загрузить страницу."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [create, id]);

  useEffect(() => {
    let active = true;
    const bytes = markdownByteLength(markdown);
    if (bytes > maximumSourceBytes) {
      setPreviewHtml("");
      setPreviewLoading(false);
      setPreviewError("Сократите исходник до 100 КиБ, чтобы увидеть предпросмотр.");
      return;
    }
    setPreviewLoading(true);
    setPreviewError("");
    const timer = window.setTimeout(() => {
      void previewMarkdown(markdown)
        .then(result => { if (active) setPreviewHtml(result.html); })
        .catch(reason => { if (active) { setPreviewHtml(""); setPreviewError(reason instanceof Error ? reason.message : "Не удалось построить предпросмотр."); } })
        .finally(() => { if (active) setPreviewLoading(false); });
    }, 350);
    return () => { active = false; window.clearTimeout(timer); };
  }, [markdown]);

  const selectVersion = (nextId: string) => {
    const selected = versions.find(item => item.id === nextId);
    if (!selected) return;
    setVersionId(selected.id);
    setTitle(selected.title);
    setMarkdown(selected.markdown);
  };

  const save = useCallback(async () => {
    if (!title.trim()) { setError("Укажите название страницы."); return; }
    if (sourceBytes > maximumSourceBytes) { setError("Исходник превышает лимит 100 КиБ."); return; }
    setSaving(true);
    setError("");
    try {
      if (create) {
        const result = await createMarkdownPage(title, markdown);
        message.success("Страница создана");
        navigate(`/markdown-pages/${result.page.id}/edit`, { replace: true });
      } else if (id && currentVersionId) {
        const result = await updateMarkdownPage(id, currentVersionId, title, markdown);
        setPage(result.page);
        setVersionId(result.currentVersion.id);
        setTitle(result.currentVersion.title);
        setMarkdown(result.currentVersion.markdown);
        setVersions(await listMarkdownPageVersions(id));
        message.success(`Сохранена версия ${result.currentVersion.version}`);
      }
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось сохранить страницу."); }
    finally { setSaving(false); }
  }, [create, currentVersionId, id, markdown, navigate, sourceBytes, title]);

  if (loading) return <div className="markdown-page-loading"><Spin size="large" /></div>;

  return <div className="markdown-editor-page">
    <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate("/markdown-pages")}>К библиотеке</Button>
    <div className="markdown-editor-heading">
      <div><Typography.Text className="eyebrow">{create ? "НОВАЯ СТРАНИЦА" : "МАТЕРИАЛ ДЛЯ УЧЕНИКОВ"}</Typography.Text><Typography.Title level={2}>{create ? "Создать Markdown-страницу" : page?.title}</Typography.Title></div>
      <Space><Button onClick={() => navigate("/markdown-pages")}>Отмена</Button><Button type="primary" icon={<SaveOutlined />} loading={saving} disabled={readOnly} onClick={() => void save()}>Сохранить версию</Button></Space>
    </div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {page?.isBuiltIn && <Alert type="info" showIcon message="Это встроенная демо-страница. Её можно добавить в свой набор, но нельзя изменить или архивировать." style={{ marginBottom: 16 }} />}
    {page?.isArchived && <Alert type="warning" showIcon message="Страница находится в архиве. Восстановите её из библиотеки, чтобы изменить." style={{ marginBottom: 16 }} />}
    <Card className="markdown-editor-card">
      <div className="markdown-editor-fields">
        <label><Typography.Text strong>Название страницы</Typography.Text><Input maxLength={200} value={title} disabled={readOnly} onChange={event => setTitle(event.target.value)} placeholder="Например, Как работает сигнал" /></label>
        {!create && versions.length > 0 && <label><Typography.Text strong>Версия</Typography.Text><Select value={versionId} onChange={selectVersion} options={versions.map(item => ({ value: item.id, label: `Версия ${item.version}` }))} /></label>}
      </div>
      {viewingHistory && <Alert type="info" showIcon message="Просмотр сохранённой версии. Чтобы создать новую, выберите текущую версию." style={{ marginBottom: 14 }} />}
      <div className="markdown-editor-grid">
        <section className="markdown-editor-pane"><div className="markdown-pane-heading"><Typography.Title level={5}>Markdown</Typography.Title><Typography.Text type="secondary">Поддерживаются таблицы, формулы, диаграммы и изображения</Typography.Text></div><MarkdownSourceEditor value={markdown} onChange={setMarkdown} onUploadImage={uploadMarkdownAsset} disabled={readOnly} /></section>
        <section className="markdown-editor-pane"><div className="markdown-pane-heading"><Typography.Title level={5}>Предпросмотр</Typography.Title><Typography.Text type="secondary">Так страницу увидят ученики</Typography.Text></div><MarkdownPreview html={previewHtml} loading={previewLoading} error={previewError} /></section>
      </div>
      {sourceBytes > maximumSourceBytes && <Alert type="error" showIcon message="Исходник превышает лимит 100 КиБ." style={{ marginTop: 16 }} />}
    </Card>
  </div>;
}
