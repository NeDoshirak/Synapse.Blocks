import { PlusOutlined, SearchOutlined } from "@ant-design/icons";
import { Button, Card, Input, Select, Space, Table, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link } from "react-router";
import { apiClient } from "../../../providers/apiClient";
import type { PlatformCase, TeacherAccount } from "../../../types/api";

export default function PlatformCasesList() {
  const [rows, setRows] = useState<PlatformCase[]>([]);
  const [teachers, setTeachers] = useState<TeacherAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [ownerId, setOwnerId] = useState<string>();
  const [state, setState] = useState<string>();
  const [error, setError] = useState("");

  useEffect(() => { void apiClient.request("/api/platform/teachers").then(data => setTeachers(data as TeacherAccount[])); }, []);
  useEffect(() => {
    let active = true;
    const timer = window.setTimeout(() => {
      setLoading(true);
      const query = new URLSearchParams();
      if (search.trim()) query.set("search", search.trim());
      if (ownerId) query.set("ownerId", ownerId);
      if (state) query.set("state", state);
      const suffix = query.size ? `?${query.toString()}` : "";
      void apiClient.request(`/api/platform/cases${suffix}`)
        .then(data => { if (active) { setRows(data as PlatformCase[]); setError(""); } })
        .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : "Не удалось загрузить наборы."); })
        .finally(() => { if (active) setLoading(false); });
    }, 160);
    return () => { active = false; window.clearTimeout(timer); };
  }, [search, ownerId, state]);

  return <div className="platform-page">
    <div className="platform-page-heading"><div><Typography.Text className="eyebrow">ОБУЧАЮЩИЕ МАТЕРИАЛЫ</Typography.Text><Typography.Title level={2}>Наборы</Typography.Title><Typography.Paragraph type="secondary">Просматривайте наборы всех преподавателей и редактируйте их, сохраняя владельца.</Typography.Paragraph></div><Button className="primary-button heading-action" type="primary" icon={<PlusOutlined />} href="/admin/platform/cases/create">Создать набор</Button></div>
    {error && <Typography.Text type="danger">{error}</Typography.Text>}
    <Card className="platform-table-card platform-cases-card" title={<div><Typography.Title level={4}>Все наборы</Typography.Title><Typography.Text type="secondary">Черновики, опубликованные материалы и архив.</Typography.Text></div>}>
      <div className="platform-filters">
        <Input allowClear prefix={<SearchOutlined />} value={search} onChange={event => setSearch(event.target.value)} placeholder="Название или преподаватель" />
        <Select allowClear value={ownerId} onChange={setOwnerId} placeholder="Все преподаватели" options={teachers.map(teacher => ({ value: teacher.id, label: `${teacher.displayName} · ${teacher.email}` }))} />
        <Select allowClear value={state} onChange={setState} placeholder="Любой статус" options={[{ value: "draft", label: "Черновик" }, { value: "published", label: "Опубликован" }, { value: "archived", label: "В архиве" }]} />
      </div>
      <Table<PlatformCase> rowKey="id" loading={loading} dataSource={rows} pagination={{ pageSize: 12, showSizeChanger: false }} locale={{ emptyText: "Наборы не найдены" }} columns={[
        { title: "Набор", dataIndex: "title", render: (title: string, item) => <div><Typography.Text strong>{title}</Typography.Text><br /><Typography.Text type="secondary">{item.itemCount} материалов · обновлён {new Date(item.updatedAt).toLocaleDateString("ru-RU")}</Typography.Text></div> },
        { title: "Владелец", render: (_, item) => <div><Typography.Text strong>{item.ownerName}</Typography.Text><br /><Typography.Text type="secondary">{item.ownerEmail}</Typography.Text></div> },
        { title: "Статус", render: (_, item) => <Tag color={item.archived ? "default" : item.isPublished ? "green" : "orange"}>{item.archived ? "В архиве" : item.isPublished ? "Опубликован" : "Черновик"}</Tag> },
        { title: "QR", render: (_, item) => item.shareLinkActive ? <Tag color="blue">Активен</Tag> : <Typography.Text type="secondary">Нет ссылки</Typography.Text> },
        { title: "", align: "right", render: (_, item) => <Space><Link to={`/platform/cases/${item.id}`}>Открыть</Link></Space> },
      ]} />
    </Card>
  </div>;
}
