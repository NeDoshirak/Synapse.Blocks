import { CopyOutlined, UserAddOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Form, Input, message, Space, Typography } from "antd";
import { useState } from "react";
import { apiClient } from "../../providers/apiClient";
import { useGetIdentity } from "@refinedev/core";
import type { CurrentUser, InvitationCreated } from "../../types/api";

export default function InvitationsPage() {
  const { data: identity } = useGetIdentity<CurrentUser>();
  const [created, setCreated] = useState<InvitationCreated>();
  const [error, setError] = useState("");
  if (!identity?.roles?.includes("PlatformAdmin")) return <Alert type="error" message="Нет доступа" description="Приглашать учителей может только администратор платформы." />;
  const invite = async ({ email }: { email: string }) => {
    setError(""); setCreated(undefined);
    try { setCreated(await apiClient.request("/api/platform/invitations", { method: "POST", body: JSON.stringify({ email }) }) as InvitationCreated); }
    catch (e) { setError(e instanceof Error ? e.message : "Не удалось создать приглашение."); }
  };
  return <Space direction="vertical" size="large" style={{ width: "100%", maxWidth: 780 }}>
    <div><Typography.Title level={2}>Пригласить учителя</Typography.Title><Typography.Paragraph type="secondary">Создайте одноразовую ссылку для входа в кабинет школы.</Typography.Paragraph></div>
    <Card><Form layout="vertical" onFinish={invite}><Form.Item name="email" label="Рабочая почта" rules={[{ required: true, type: "email", message: "Введите корректную почту" }]}><Input size="large" placeholder="teacher@school.ru" /></Form.Item><Button type="primary" icon={<UserAddOutlined />} htmlType="submit">Создать ссылку</Button></Form></Card>
    {error && <Alert type="error" showIcon message={error} />}
    {created && <Card title="Ссылка готова" extra={<Typography.Text type="secondary">Действует до {new Date(created.expiresAt).toLocaleString("ru-RU")}</Typography.Text>}><Space.Compact style={{ width: "100%" }}><Input readOnly value={created.invitationUrl} /><Button icon={<CopyOutlined />} onClick={() => navigator.clipboard.writeText(created.invitationUrl).then(() => message.success("Ссылка скопирована"))}>Копировать</Button></Space.Compact><Alert style={{ marginTop: 16 }} type="warning" showIcon message="Скопируйте ссылку сейчас. После закрытия страницы её нельзя будет получить снова." /></Card>}
  </Space>;
}
