import { LockOutlined } from "@ant-design/icons";
import { useLogin } from "@refinedev/core";
import { Alert, Button, Card, Form, Input, Typography } from "antd";
import { useState } from "react";

export default function LoginPage() {
  const { mutate: login, isPending, error } = useLogin();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  return <main className="center-page"><Card className="auth-card"><Typography.Title level={2}>Synapse Blocks</Typography.Title><Typography.Paragraph type="secondary">Кабинет учителя</Typography.Paragraph><Form layout="vertical" onFinish={() => login({ email, password })}>
    {error && <Alert type="error" showIcon message="Не удалось войти" description={error.message ?? "Проверьте введённые данные."} style={{ marginBottom: 20 }} />}
    <Form.Item label="Почта" name="email" rules={[{ required: true, type: "email", message: "Введите корректную почту" }]}><Input autoComplete="username" size="large" value={email} onChange={e => setEmail(e.target.value)} placeholder="teacher@school.ru" /></Form.Item>
    <Form.Item label="Пароль" name="password" rules={[{ required: true, message: "Введите пароль" }]}><Input.Password prefix={<LockOutlined />} autoComplete="current-password" size="large" value={password} onChange={e => setPassword(e.target.value)} /></Form.Item>
    <Button className="primary-button" type="primary" htmlType="submit" loading={isPending}>Войти в кабинет</Button>
  </Form></Card></main>;
}
