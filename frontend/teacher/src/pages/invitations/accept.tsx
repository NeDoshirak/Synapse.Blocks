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
  const accept = async ({ password }: { password: string }) => {
    try { await apiClient.request("/api/platform/invitations/accept", { method: "POST", body: JSON.stringify({ token, password }) }); setDone(true); }
    catch (e) { setError(e instanceof Error ? e.message : "Ссылка недействительна или уже использована."); }
    finally { navigate("/invitations/accept", { replace: true }); }
  };
  if (done) return <Result status="success" title="Кабинет создан" subTitle="Теперь войдите с указанной почтой и новым паролем." extra={<Button type="primary" onClick={() => navigate("/login")}>Перейти ко входу</Button>} />;
  return <main className="center-page"><Card className="auth-card"><Typography.Title level={2}>Завершите регистрацию</Typography.Title><Typography.Paragraph type="secondary">Создайте пароль для кабинета учителя.</Typography.Paragraph>{error && <Alert type="error" message={error} showIcon />}<Form layout="vertical" onFinish={accept} style={{ marginTop: 20 }}><Form.Item name="password" label="Новый пароль" rules={[{ required: true, min: 8, message: "Пароль должен содержать не менее 8 символов" }]}><Input.Password autoComplete="new-password" size="large" /></Form.Item><Button type="primary" htmlType="submit" block size="large" disabled={!token}>Создать кабинет</Button></Form></Card></main>;
}
