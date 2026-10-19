import { CheckOutlined, CopyOutlined, LockOutlined, PlusOutlined, ReloadOutlined, SafetyCertificateOutlined, SearchOutlined, UserAddOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Form, Input, Modal, Popconfirm, Space, Table, Tag, Typography, message } from "antd";
import { useCallback, useEffect, useState } from "react";
import { apiClient } from "../../providers/apiClient";
import type { InvitationCreated, PendingInvitation, TeacherAccount } from "../../types/api";

interface InvitationForm { email: string }

export default function PlatformTeachersPage() {
  const [teachers, setTeachers] = useState<TeacherAccount[]>([]);
  const [invitations, setInvitations] = useState<PendingInvitation[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [inviteOpen, setInviteOpen] = useState(false);
  const [invitePending, setInvitePending] = useState(false);
  const [inviteError, setInviteError] = useState("");
  const [created, setCreated] = useState<InvitationCreated>();
  const [resetLink, setResetLink] = useState("");
  const [actionPending, setActionPending] = useState<string>();
  const [messageApi, contextHolder] = message.useMessage();

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";
      const [accounts, pending] = await Promise.all([
        apiClient.request(`/api/platform/teachers${query}`),
        apiClient.request("/api/platform/invitations"),
      ]);
      setTeachers(accounts as TeacherAccount[]);
      setInvitations(pending as PendingInvitation[]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось загрузить список преподавателей.");
    } finally { setLoading(false); }
  }, [search]);

  useEffect(() => { const timer = window.setTimeout(() => void load(), 180); return () => window.clearTimeout(timer); }, [load]);

  const invite = async ({ email }: InvitationForm) => {
    setInvitePending(true);
    setInviteError("");
    try {
      const result = await apiClient.request("/api/platform/invitations", { method: "POST", body: JSON.stringify({ email }) }) as InvitationCreated;
      setCreated(result);
      await load();
    } catch (reason) {
      setInviteError(reason instanceof Error ? reason.message : "Не удалось создать приглашение.");
    } finally { setInvitePending(false); }
  };

  const teacherAction = async (teacher: TeacherAccount, action: "block" | "reactivate" | "password-reset" | "two-factor/reset") => {
    const key = `${teacher.id}:${action}`;
    setActionPending(key);
    setError("");
    try {
      const result = await apiClient.request(`/api/platform/teachers/${encodeURIComponent(teacher.id)}/${action}`, { method: "POST" });
      if (action === "password-reset") setResetLink((result as { resetUrl: string }).resetUrl);
      else await load();
      await messageApi.success(action === "block" ? "Доступ преподавателя закрыт" : action === "reactivate" ? "Доступ восстановлен" : action === "two-factor/reset" ? "Настройка 2FA сброшена" : "Ссылка для смены пароля готова");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось выполнить действие.");
    } finally { setActionPending(undefined); }
  };

  const revokeInvitation = async (invitation: PendingInvitation) => {
    try {
      await apiClient.request(`/api/platform/invitations/${encodeURIComponent(invitation.id)}/revoke`, { method: "POST" });
      await load();
      await messageApi.success("Приглашение отозвано");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось отозвать приглашение."); }
  };

  return <div className="platform-page">
    {contextHolder}
    <div className="platform-page-heading">
      <div><Typography.Text className="eyebrow">КОМАНДА ПЛАТФОРМЫ</Typography.Text><Typography.Title level={2}>Преподаватели</Typography.Title><Typography.Paragraph type="secondary">Управляйте доступом, профилями и защитой аккаунтов.</Typography.Paragraph></div>
      <Button className="primary-button heading-action" icon={<UserAddOutlined />} onClick={() => { setCreated(undefined); setInviteError(""); setInviteOpen(true); }}>Добавить преподавателя</Button>
    </div>
    {error && <Alert type="error" showIcon message={error} closable onClose={() => setError("")} style={{ marginBottom: 18 }} />}

    <div className="metric-strip">
      <Card className="metric-card"><Typography.Text type="secondary">Аккаунты</Typography.Text><Typography.Title level={3}>{teachers.length}</Typography.Title><Typography.Text type="secondary">преподавателей</Typography.Text></Card>
      <Card className="metric-card"><Typography.Text type="secondary">Активные</Typography.Text><Typography.Title level={3}>{teachers.filter(item => item.isActive).length}</Typography.Title><Typography.Text type="secondary">доступ открыт</Typography.Text></Card>
      <Card className="metric-card"><Typography.Text type="secondary">Требуют внимания</Typography.Text><Typography.Title level={3}>{teachers.filter(item => !item.isActive || !item.twoFactorEnabled).length}</Typography.Title><Typography.Text type="secondary">блокировка или 2FA</Typography.Text></Card>
      <Card className="metric-card"><Typography.Text type="secondary">Приглашения</Typography.Text><Typography.Title level={3}>{invitations.length}</Typography.Title><Typography.Text type="secondary">ожидают активации</Typography.Text></Card>
    </div>

    <Card className="platform-table-card" title={<div><Typography.Title level={4}>Список преподавателей</Typography.Title><Typography.Text type="secondary">Откройте профиль, чтобы управлять доступом и настройками безопасности.</Typography.Text></div>} extra={<Input allowClear prefix={<SearchOutlined />} value={search} onChange={event => setSearch(event.target.value)} placeholder="Имя или почта" className="teacher-search" />}>
      <Table<TeacherAccount> rowKey="id" loading={loading} dataSource={teachers} pagination={{ pageSize: 10, showSizeChanger: false }} locale={{ emptyText: "Пока нет приглашённых преподавателей" }} columns={[
        { title: "Преподаватель", dataIndex: "displayName", render: (value: string, teacher) => <div className="teacher-cell"><span className="teacher-avatar">{(teacher.firstName?.[0] ?? teacher.email[0] ?? "П").toLocaleUpperCase("ru-RU")}</span><span><Typography.Text strong>{value || "Без имени"}</Typography.Text><br /><Typography.Text type="secondary">{teacher.teacherDescription || "Описание не добавлено"}</Typography.Text></span></div> },
        { title: "Почта", dataIndex: "email", responsive: ["md"] },
        { title: "Статус", render: (_, teacher) => <Space direction="vertical" size={4}><Tag color={teacher.isActive ? "green" : "red"}>{teacher.isActive ? "Активен" : "Заблокирован"}</Tag><Tag color={teacher.twoFactorEnabled ? "blue" : "orange"}>{teacher.twoFactorEnabled ? "2FA включена" : "Настроить 2FA"}</Tag></Space> },
        { title: "Действия", align: "right", render: (_, teacher) => <Space wrap className="teacher-actions">
          <Popconfirm title="Создать ссылку для смены пароля?" description="Ссылка будет действительна 24 часа и появится один раз." okText="Создать ссылку" cancelText="Отмена" onConfirm={() => teacherAction(teacher, "password-reset")}><Button size="small" icon={<ReloadOutlined />} loading={actionPending === `${teacher.id}:password-reset`}>Сбросить пароль</Button></Popconfirm>
          <Popconfirm title="Сбросить 2FA?" description="При следующем входе преподаватель настроит приложение заново." okText="Сбросить" cancelText="Отмена" onConfirm={() => teacherAction(teacher, "two-factor/reset")}><Button size="small" icon={<SafetyCertificateOutlined />} loading={actionPending === `${teacher.id}:two-factor/reset`}>Сбросить 2FA</Button></Popconfirm>
          {teacher.isActive
            ? <Popconfirm title="Заблокировать аккаунт?" description="Активные сессии преподавателя будут закрыты." okText="Заблокировать" cancelText="Отмена" onConfirm={() => teacherAction(teacher, "block")}><Button size="small" danger icon={<LockOutlined />} loading={actionPending === `${teacher.id}:block`}>Блокировать</Button></Popconfirm>
            : <Popconfirm title="Восстановить доступ?" okText="Восстановить" cancelText="Отмена" onConfirm={() => teacherAction(teacher, "reactivate")}><Button size="small" icon={<CheckOutlined />} loading={actionPending === `${teacher.id}:reactivate`}>Разблокировать</Button></Popconfirm>}
        </Space> },
      ]} />
    </Card>

    <Card className="platform-table-card invitation-card" title={<div><Typography.Title level={4}>Ожидают активации</Typography.Title><Typography.Text type="secondary">Приглашение действует семь дней. Ссылку можно получить только при создании.</Typography.Text></div>}>
      <Table<PendingInvitation> rowKey="id" loading={loading} dataSource={invitations} pagination={false} locale={{ emptyText: "Нет активных приглашений" }} columns={[
        { title: "Почта", dataIndex: "email", render: value => <Typography.Text strong>{value}</Typography.Text> },
        { title: "Действует до", dataIndex: "expiresAt", render: value => new Date(value).toLocaleString("ru-RU") },
        { title: "", align: "right", render: (_, invitation) => <Popconfirm title="Отозвать приглашение?" okText="Отозвать" cancelText="Назад" onConfirm={() => revokeInvitation(invitation)}><Button danger size="small">Отозвать</Button></Popconfirm> },
      ]} />
    </Card>

    <Modal title={created ? "Ссылка на регистрацию готова" : "Новое приглашение"} open={inviteOpen} onCancel={() => { setInviteOpen(false); setCreated(undefined); }} footer={null} destroyOnClose>
      {created ? <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Alert type="warning" showIcon message="Скопируйте ссылку сейчас. Повторно получить её нельзя." description={`Срок действия до ${new Date(created.expiresAt).toLocaleString("ru-RU")}.`} />
        <Space.Compact style={{ width: "100%" }}><Input readOnly value={created.invitationUrl} /><Button icon={<CopyOutlined />} onClick={() => navigator.clipboard.writeText(created.invitationUrl).then(() => messageApi.success("Ссылка скопирована"))}>Копировать</Button></Space.Compact>
        <Button type="primary" onClick={() => { setInviteOpen(false); setCreated(undefined); }}>Готово</Button>
      </Space> : <Form layout="vertical" onFinish={invite}>
        <Typography.Paragraph type="secondary">Преподаватель задаст пароль, заполнит имя и фамилию, а затем подключит двухфакторную защиту.</Typography.Paragraph>
        {inviteError && <Alert type="error" showIcon message={inviteError} style={{ marginBottom: 16 }} />}
        <Form.Item name="email" label="Рабочая почта" rules={[{ required: true, type: "email", message: "Введите корректную почту" }]}><Input autoComplete="email" size="large" placeholder="teacher@school.ru" /></Form.Item>
        <Button className="primary-button" type="primary" htmlType="submit" loading={invitePending} icon={<PlusOutlined />}>Создать приглашение</Button>
      </Form>}
    </Modal>

    <Modal title="Ссылка для смены пароля" open={Boolean(resetLink)} onCancel={() => setResetLink("")} footer={<Button type="primary" onClick={() => setResetLink("")}>Закрыть</Button>}>
      <Alert type="warning" showIcon message="Скопируйте ссылку и передайте преподавателю. Она действует 24 часа." />
      <Space.Compact style={{ width: "100%", marginTop: 16 }}><Input readOnly value={resetLink} /><Button icon={<CopyOutlined />} onClick={() => navigator.clipboard.writeText(resetLink).then(() => messageApi.success("Ссылка скопирована"))}>Копировать</Button></Space.Compact>
    </Modal>
  </div>;
}
