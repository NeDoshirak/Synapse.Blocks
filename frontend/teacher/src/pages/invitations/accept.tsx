import { Alert, Button, Card, Form, Input, Result, Typography } from "antd";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { apiClient } from "../../providers/apiClient";

export default function AcceptInvitationPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [done, setDone] = useState(false);
  const [error, setError] = useState("");
  const token = params.get("token") ?? "";
  const accept = async ({ password, firstName, lastName, teacherDescription }: { password: string; firstName: string; lastName: string; teacherDescription?: string }) => {
    try {
      await apiClient.request("/api/platform/invitations/accept", { method: "POST", body: JSON.stringify({ token, password, firstName, lastName, teacherDescription }) });
      apiClient.invalidateCsrf();
      setDone(true);
      navigate("/invitations/accept", { replace: true });
    }
    catch (e) { setError(e instanceof Error ? e.message : "Ссылка недействительна или уже использована."); }
  };
  if (done) return <main className="center-page"><Card className="auth-card"><Result status="success" title="Кабинет создан" subTitle="Теперь войдите с почтой, на которую пришло приглашение. При первом входе настройте приложение-аутентификатор." extra={<Button type="primary" onClick={() => navigate("/login")}>Перейти ко входу</Button>} /></Card></main>;
  return <main className="center-page"><Card className="auth-card"><div className="auth-mark">ФК</div><Typography.Title level={2}>Завершите регистрацию</Typography.Title><Typography.Paragraph type="secondary">Заполните профиль преподавателя и задайте пароль.</Typography.Paragraph>{error && <Alert type="error" message={error} showIcon />}<Form layout="vertical" onFinish={accept} style={{ marginTop: 20 }}>
    <Form.Item name="firstName" label="Имя" rules={[{ required: true, whitespace: true, max: 100, message: "Укажите имя (до 100 символов)" }]}><Input autoComplete="given-name" size="large" /></Form.Item>
    <Form.Item name="lastName" label="Фамилия" rules={[{ required: true, whitespace: true, max: 100, message: "Укажите фамилию (до 100 символов)" }]}><Input autoComplete="family-name" size="large" /></Form.Item>
    <Form.Item name="teacherDescription" label="О преподавателе" rules={[{ max: 1000, message: "Не более 1000 символов" }]}><Input.TextArea autoSize={{ minRows: 2, maxRows: 5 }} maxLength={1000} showCount placeholder="Коротко о себе — необязательно" /></Form.Item>
    <Form.Item name="password" label="Новый пароль" help="Не менее 12 символов, с заглавной и строчной буквой, цифрой и специальным символом." rules={[{ required: true, min: 12, message: "Пароль должен содержать не менее 12 символов" }]}><Input.Password autoComplete="new-password" size="large" /></Form.Item>
    <Button className="primary-button" type="primary" htmlType="submit" block size="large" disabled={!token}>Создать кабинет</Button>
  </Form></Card></main>;
}
