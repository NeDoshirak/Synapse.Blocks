import { KeyOutlined, SafetyCertificateOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Form, Input, Segmented, Typography } from "antd";
import { useState } from "react";
import { useNavigate } from "react-router";
import { apiClient } from "../providers/apiClient";

export default function TwoFactorChallengePage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<"authenticator" | "recovery">("authenticator");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const [form] = Form.useForm();

  const verify = async ({ code }: { code: string }) => {
    setError("");
    setPending(true);
    const endpoint = mode === "recovery" ? "/api/auth/recovery-codes/sign-in" : "/api/auth/two-factor/verify";
    const payload = mode === "recovery" ? { code } : { code: code.replace(/\s/g, "") };
    try {
      await apiClient.request(endpoint, { method: "POST", body: JSON.stringify(payload) });
      apiClient.invalidateCsrf();
      navigate("/", { replace: true });
    } catch {
      setError("Код неверен или уже использован. Проверьте приложение либо введите другой резервный код.");
      form.resetFields();
    } finally { setPending(false); }
  };

  return <main className="center-page"><Card className="auth-card">
    <div className="auth-mark"><SafetyCertificateOutlined /></div>
    <Typography.Title level={2}>Подтвердите вход</Typography.Title>
    <Typography.Paragraph type="secondary">Введите одноразовый код из приложения-аутентификатора.</Typography.Paragraph>
    <Segmented block value={mode} onChange={value => { setMode(value as typeof mode); setError(""); form.resetFields(); }} options={[{ label: "Код приложения", value: "authenticator", icon: <SafetyCertificateOutlined /> }, { label: "Резервный код", value: "recovery", icon: <KeyOutlined /> }]} />
    {error && <Alert type="error" showIcon message={error} style={{ marginTop: 18 }} />}
    <Form form={form} layout="vertical" onFinish={verify} style={{ marginTop: 22 }}>
      <Form.Item name="code" label={mode === "recovery" ? "Резервный код" : "Шестизначный код"} rules={[{ required: true, message: "Введите код" }, ...(mode === "authenticator" ? [{ len: 6, message: "Введите шестизначный код" }] : [])]}>
        <Input autoComplete="one-time-code" inputMode={mode === "authenticator" ? "numeric" : "text"} maxLength={mode === "authenticator" ? 6 : 32} size="large" placeholder={mode === "authenticator" ? "000000" : "XXXX-XXXX-XXXX"} />
      </Form.Item>
      <Button className="primary-button" type="primary" htmlType="submit" loading={pending}>Подтвердить</Button>
    </Form>
    <Button type="link" className="auth-back-link" onClick={() => navigate("/login")}>Вернуться ко входу</Button>
  </Card></main>;
}
