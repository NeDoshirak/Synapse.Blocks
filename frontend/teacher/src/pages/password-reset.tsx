import { Alert, Button, Card, Form, Input, Result, Typography } from "antd";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { apiClient } from "../providers/apiClient";

export default function PasswordResetPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const submit = async ({ newPassword, confirmPassword }: { newPassword: string; confirmPassword: string }) => {
    setError("");
    try {
      await apiClient.request("/api/auth/password-reset/complete", { method: "POST", body: JSON.stringify({ email, token, newPassword }) });
      apiClient.invalidateCsrf();
      setDone(true);
      navigate("/password-reset", { replace: true });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Ссылка устарела или пароль не соответствует требованиям.");
    }
  };

  if (done) return <main className="center-page"><Card className="auth-card"><Result status="success" title="Пароль обновлён" subTitle="Войдите с новым паролем. Если для аккаунта ещё не настроена двухфакторная защита, система предложит подключить её." extra={<Button type="primary" onClick={() => navigate("/login", { replace: true })}>Перейти ко входу</Button>} /></Card></main>;

  return <main className="center-page"><Card className="auth-card"><div className="auth-mark">ФК</div><Typography.Title level={2}>Задайте новый пароль</Typography.Title><Typography.Paragraph type="secondary">{email ? `Аккаунт: ${email}` : "Откройте ссылку для сброса пароля, полученную у администратора."}</Typography.Paragraph>
    {error && <Alert type="error" showIcon message="Не удалось обновить пароль" description={error} style={{ marginBottom: 18 }} />}
    <Form layout="vertical" onFinish={submit}>
      <Form.Item name="newPassword" label="Новый пароль" help="Не менее 12 символов, с заглавной и строчной буквой, цифрой и специальным символом." rules={[{ required: true, min: 12, message: "Пароль должен содержать не менее 12 символов" }]}><Input.Password autoComplete="new-password" size="large" /></Form.Item>
      <Form.Item name="confirmPassword" label="Повторите пароль" dependencies={["newPassword"]} rules={[{ required: true, message: "Повторите пароль" }, ({ getFieldValue }) => ({ validator(_, value) { return !value || getFieldValue("newPassword") === value ? Promise.resolve() : Promise.reject(new Error("Пароли не совпадают")); } })]}><Input.Password autoComplete="new-password" size="large" /></Form.Item>
      <Button className="primary-button" type="primary" htmlType="submit" block disabled={!email || !token}>Обновить пароль</Button>
    </Form>
  </Card></main>;
}
