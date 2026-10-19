import { SafetyCertificateOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Form, Input, QRCode, Spin, Typography } from "antd";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { apiClient } from "../providers/apiClient";
import { setPendingRecoveryCodes } from "../providers/recoveryCodeMemory";
import type { AuthenticatorSetup, RecoveryCodes } from "../types/api";

export default function AuthenticatorSetupPage() {
  const navigate = useNavigate();
  const [setup, setSetup] = useState<AuthenticatorSetup>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [rotating, setRotating] = useState(false);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    let active = true;
    apiClient.request("/api/auth/authenticator/setup")
      .then(result => { if (active) setSetup(result as AuthenticatorSetup); })
      .catch(reason => {
        if (!active) return;
        if ((reason as { statusCode?: number })?.statusCode === 401) {
          setError("Сессия завершилась. Войдите заново перед настройкой приложения-аутентификатора.");
          setSessionExpired(true);
        } else {
          setError(reason instanceof Error ? reason.message : "Не удалось подготовить настройку.");
        }
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const confirm = async ({ code }: { code: string }) => {
    setError("");
    setConfirming(true);
    try {
      const result = await apiClient.request("/api/auth/authenticator/confirm", { method: "POST", body: JSON.stringify({ code }) }) as RecoveryCodes;
      apiClient.invalidateCsrf();
      form.resetFields();
      setPendingRecoveryCodes(result.recoveryCodes);
      navigate("/recovery-codes", { replace: true });
    } catch (reason) {
      if ((reason as { statusCode?: number })?.statusCode === 401) {
        setError("Сессия завершилась. Войдите заново перед настройкой приложения-аутентификатора.");
        setSetup(undefined);
        setSessionExpired(true);
      } else {
        setError(reason instanceof Error ? reason.message : "Код не подошёл. Проверьте приложение и попробуйте ещё раз.");
      }
      form.resetFields();
    } finally { setConfirming(false); }
  };

  const rotateSetup = async () => {
    setError("");
    setRotating(true);
    try {
      const result = await apiClient.request("/api/auth/authenticator/rotate-setup", { method: "POST" }) as AuthenticatorSetup;
      setSetup(result);
      form.resetFields();
    } catch (reason) {
      if ((reason as { statusCode?: number })?.statusCode === 401) {
        setError("Сессия завершилась. Войдите заново перед настройкой приложения-аутентификатора.");
        setSetup(undefined);
        setSessionExpired(true);
      } else {
        setError(reason instanceof Error ? reason.message : "Не удалось выпустить новый ключ.");
      }
    } finally { setRotating(false); }
  };

  return <main className="center-page"><Card className="auth-card auth-card-wide">
    <div className="auth-mark"><SafetyCertificateOutlined /></div>
    <Typography.Title level={2}>Защитите вход</Typography.Title>
    <Typography.Paragraph type="secondary">Подключите Google Authenticator или другое приложение с поддержкой одноразовых кодов. Это обязательный шаг перед началом работы.</Typography.Paragraph>
    {error && <Alert type="error" showIcon message="Не удалось продолжить" description={error} style={{ marginBottom: 20 }} />}
    {loading ? <div className="auth-loading"><Spin size="large" /></div> : setup ? <>
      <div className="auth-setup-grid">
        <section className="auth-qr-panel"><QRCode value={setup.authenticatorUri} size={216} bordered={false} /><Typography.Text type="secondary">Отсканируйте QR-код в приложении</Typography.Text></section>
        <section className="auth-setup-copy"><Typography.Title level={5}>Если сканирование недоступно</Typography.Title><Typography.Paragraph type="secondary">Добавьте аккаунт вручную и введите этот ключ:</Typography.Paragraph><Typography.Text className="auth-secret" copyable>{setup.sharedKey.match(/.{1,4}/g)?.join(" ") ?? setup.sharedKey}</Typography.Text><Typography.Paragraph type="secondary" className="auth-setup-note">Затем подтвердите подключение шестизначным кодом из приложения.</Typography.Paragraph></section>
      </div>
      <Form form={form} layout="vertical" onFinish={confirm} className="auth-code-form">
        <Form.Item name="code" label="Код из приложения" rules={[{ required: true, len: 6, message: "Введите шестизначный код" }]}>
          <Input autoComplete="one-time-code" inputMode="numeric" pattern="[0-9]*" maxLength={6} size="large" placeholder="000000" />
        </Form.Item>
        <Button className="primary-button" type="primary" htmlType="submit" loading={confirming}>Подтвердить и продолжить</Button>
      </Form>
      <Typography.Paragraph type="secondary" className="auth-setup-note">Если ключ уже был раскрыт или потерян, выпустите новый. Старый ключ сразу перестанет работать.</Typography.Paragraph>
      <Button onClick={rotateSetup} loading={rotating}>Выпустить новый ключ</Button>
    </> : <Alert type="warning" showIcon message={sessionExpired ? "Нужно войти заново" : "Сначала войдите по паролю, чтобы настроить приложение-аутентификатор."} action={<Button onClick={() => navigate("/login", { replace: true })}>{sessionExpired ? "Войти" : "Ко входу"}</Button>} />}
  </Card></main>;
}
