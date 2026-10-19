import { ArrowLeftOutlined, CopyOutlined, QrcodeOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Input, QRCode, Select, Space, Spin, Typography, message } from "antd";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { CaseSectionsEditor } from "../../../components/cases/CaseSectionsEditor";
import { canPublishCase, createCaseSection } from "../../../resources/caseOrder";
import { apiClient } from "../../../providers/apiClient";
import type { CaseSectionDraft, PlatformCase, TeacherAccount, TeacherLevel } from "../../../types/api";

function toDrafts(item: PlatformCase): CaseSectionDraft[] {
  return item.sections.slice().sort((a, b) => a.order - b.order).map(section => ({
    clientId: section.id,
    title: section.title ?? "",
    levels: section.levels.slice().sort((a, b) => a.order - b.order).map(level => ({ levelVersionId: level.levelVersionId, displayTitle: level.displayTitle ?? "" })),
  }));
}

export default function PlatformCaseEdit({ create = false }: { create?: boolean }) {
  const { id } = useParams();
  const navigate = useNavigate();
  const [teachers, setTeachers] = useState<TeacherAccount[]>([]);
  const [ownerId, setOwnerId] = useState<string>();
  const [levels, setLevels] = useState<TeacherLevel[]>([]);
  const [item, setItem] = useState<PlatformCase>();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [sections, setSections] = useState<CaseSectionDraft[]>(() => [createCaseSection()]);
  const [shareUrl, setShareUrl] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(!create);
  const [levelsLoading, setLevelsLoading] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => { void apiClient.request("/api/platform/teachers").then(value => setTeachers(value as TeacherAccount[])).catch(reason => setError(reason instanceof Error ? reason.message : "Не удалось загрузить преподавателей.")); }, []);
  useEffect(() => {
    if (!create && id) {
      setLoading(true);
      void apiClient.request(`/api/platform/cases/${encodeURIComponent(id)}`)
        .then(value => {
          const loaded = value as PlatformCase;
          setItem(loaded); setOwnerId(loaded.ownerId); setTitle(loaded.title); setDescription(loaded.description); setSections(toDrafts(loaded));
        })
        .catch(reason => setError(reason instanceof Error ? reason.message : "Набор не найден."))
        .finally(() => setLoading(false));
    }
  }, [create, id]);
  useEffect(() => {
    if (!ownerId) { setLevels([]); return; }
    let active = true;
    setLevelsLoading(true);
    void apiClient.request(`/api/platform/teachers/${encodeURIComponent(ownerId)}/levels`)
      .then(value => { if (active) setLevels(value as TeacherLevel[]); })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : "Не удалось загрузить уровни этого преподавателя."); })
      .finally(() => { if (active) setLevelsLoading(false); });
    return () => { active = false; };
  }, [ownerId]);

  const canSave = useMemo(() => canPublishCase(title, sections), [title, sections]);
  const canonicalTitleByVersion = useMemo(() => Object.fromEntries((item?.sections ?? []).flatMap(section => section.levels.map(level => [level.levelVersionId, level.title] as const))), [item]);
  const owner = teachers.find(teacher => teacher.id === ownerId);
  const body = () => ({ caseType: "orderedLevels", title, description, sections: sections.map(section => ({ title: section.title.trim() || null, levels: section.levels.map(level => ({ levelVersionId: level.levelVersionId, displayTitle: level.displayTitle.trim() || null })) })) });

  const persist = async () => {
    if (!ownerId) throw new Error("Выберите преподавателя-владельца набора.");
    const payload = create ? { ...body(), ownerId } : body();
    const url = item ? `/api/platform/cases/${item.id}` : "/api/platform/cases";
    const response = await apiClient.request(url, { method: item ? "PUT" : "POST", body: JSON.stringify(payload) }) as PlatformCase;
    return response;
  };

  const save = async () => {
    setBusy(true); setError("");
    try {
      const saved = await persist();
      setItem(saved); setOwnerId(saved.ownerId); setSections(toDrafts(saved));
      if (create) navigate(`/platform/cases/${saved.id}`, { replace: true });
      else message.success("Набор сохранён");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось сохранить набор."); }
    finally { setBusy(false); }
  };

  const publish = async () => {
    if (!item) return;
    setBusy(true); setError("");
    try {
      await persist();
      const published = await apiClient.request(`/api/platform/cases/${item.id}/publish`, { method: "POST" }) as PlatformCase;
      setItem(published); setSections(toDrafts(published)); message.success("Набор опубликован");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось опубликовать набор."); }
    finally { setBusy(false); }
  };

  const setShare = async () => {
    if (!item) return;
    setBusy(true); setError("");
    try {
      const result = await apiClient.request(`/api/platform/cases/${item.id}/share-link`, { method: "POST" }) as { url: string };
      setShareUrl(new URL(result.url, window.location.origin).href);
      setItem({ ...item, shareLinkActive: true });
      message.success("Новая ссылка готова. Скопируйте или распечатайте QR-код.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось создать QR-ссылку."); }
    finally { setBusy(false); }
  };

  const revokeShare = async () => {
    if (!item) return;
    setBusy(true);
    try { await apiClient.request(`/api/platform/cases/${item.id}/share-link`, { method: "DELETE" }); setShareUrl(""); setItem({ ...item, shareLinkActive: false }); message.success("QR-ссылка закрыта. История попыток сохранена."); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось закрыть ссылку."); }
    finally { setBusy(false); }
  };

  const archive = async () => {
    if (!item) return;
    setBusy(true);
    try { await apiClient.request(`/api/platform/cases/${item.id}`, { method: "DELETE" }); navigate("/platform/cases"); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось архивировать набор."); }
    finally { setBusy(false); }
  };

  const restore = async () => {
    if (!item) return;
    setBusy(true);
    try { await apiClient.request(`/api/platform/cases/${item.id}/restore`, { method: "POST" }); setItem({ ...item, archived: false, isPublished: false }); message.success("Набор восстановлен как черновик"); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось восстановить набор."); }
    finally { setBusy(false); }
  };

  if (loading) return <div className="workspace-loading"><Spin size="large" /></div>;
  return <div className="platform-page platform-case-editor">
    <Button type="link" icon={<ArrowLeftOutlined />} href="/admin/platform/cases" className="back-link">Все наборы</Button>
    <div className="platform-page-heading"><div><Typography.Text className="eyebrow">{create ? "НОВЫЙ МАТЕРИАЛ" : "УПРАВЛЕНИЕ НАБОРОМ"}</Typography.Text><Typography.Title level={2}>{create ? "Создать набор" : title || "Набор"}</Typography.Title></div></div>
    {!create && item && <Card className="owner-context"><Typography.Text type="secondary">Владелец набора</Typography.Text><Typography.Title level={5}>{item.ownerName}</Typography.Title><Typography.Text>{item.ownerEmail}</Typography.Text><Typography.Text type="secondary" className="owner-hint">Владелец сохраняется при редактировании.</Typography.Text></Card>}
    {error && <Alert type="error" showIcon closable onClose={() => setError("")} message={error} style={{ marginBottom: 16 }} />}
    {item?.archived && <Alert type="warning" showIcon message="Набор в архиве. Попытки и отчёты сохранены, QR-ссылка закрыта." style={{ marginBottom: 16 }} />}
    <Card className="platform-table-card" title={<Typography.Title level={4}>Содержание набора</Typography.Title>}>
      <Space direction="vertical" style={{ width: "100%" }} size="middle">
        {create && <div><Typography.Text strong>Преподаватель-владелец</Typography.Text><Select showSearch optionFilterProp="label" value={ownerId} onChange={value => { setOwnerId(value); setSections([createCaseSection()]); }} placeholder="Выберите преподавателя" style={{ width: "100%", marginTop: 8 }} size="large" options={teachers.map(teacher => ({ value: teacher.id, label: `${teacher.displayName} · ${teacher.email}${teacher.isActive ? "" : " (заблокирован)"}` }))} /></div>}
        <Input size="large" placeholder="Название набора" value={title} disabled={item?.archived} onChange={event => setTitle(event.target.value)} maxLength={200} />
        <Input.TextArea rows={2} placeholder="Описание для учеников" value={description} disabled={item?.archived} onChange={event => setDescription(event.target.value)} maxLength={2000} />
        {levelsLoading ? <Spin /> : ownerId ? <CaseSectionsEditor levels={levels} sections={sections} canonicalTitleByVersion={canonicalTitleByVersion} disabled={item?.archived} onChange={setSections} /> : <Alert type="info" message="Сначала выберите преподавателя, чтобы загрузить его уровни." />}
        <Space wrap>
          {item?.archived
            ? <Button type="primary" onClick={restore} loading={busy}>Восстановить набор</Button>
            : <>
              <Button type="primary" onClick={save} loading={busy} disabled={!canSave || !ownerId}>Сохранить набор</Button>
              {item && <Button onClick={publish} loading={busy} disabled={!canSave || item.isPublished}>Опубликовать</Button>}
              {item && <Button danger onClick={archive} loading={busy}>В архив</Button>}
            </>}
        </Space>
        {item && <Typography.Text type="secondary">{item.archived ? "Архив · попытки сохранены" : item.isPublished ? "Опубликован · версии заданий закреплены" : "Черновик"}</Typography.Text>}
      </Space>
    </Card>
    {item && !item.archived && <Card className="platform-table-card case-qr-card" title={<Typography.Title level={4}>Ссылка и QR-код</Typography.Title>} style={{ marginTop: 18 }}>
      <Space direction="vertical">
        {shareUrl ? <>
          <QRCode value={shareUrl} size={210} />
          <Input readOnly value={shareUrl} />
          <Space wrap><Button icon={<CopyOutlined />} onClick={() => navigator.clipboard.writeText(shareUrl).then(() => message.success("Ссылка скопирована"))}>Копировать</Button><Button onClick={() => window.print()}>Распечатать QR</Button></Space>
          <Typography.Text type="secondary">Ссылка доступна только в этом окне. Сохраните её или распечатайте QR.</Typography.Text>
        </> : <>
          <Button type="primary" icon={<QrcodeOutlined />} onClick={setShare} loading={busy} disabled={!item.isPublished}>{item.shareLinkActive ? "Перевыпустить ссылку и показать новую" : "Создать QR-ссылку"}</Button>
          {item.shareLinkActive && <Alert type="warning" message="Ссылка была создана раньше и повторно не отображается. Перевыпуск закроет предыдущую." />}
        </>}
        {item.shareLinkActive && <Button danger onClick={revokeShare} loading={busy}>Закрыть ссылку</Button>}
      </Space>
    </Card>}
    {create && owner && <Typography.Text className="owner-hint">Набор будет создан от имени {owner.displayName}.</Typography.Text>}
  </div>;
}
