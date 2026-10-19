import { CheckCircleOutlined, CopyOutlined, SafetyCertificateOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Result, Typography, message } from "antd";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { takePendingRecoveryCodes } from "../providers/recoveryCodeMemory";

export default function RecoveryCodesPage() {
  const navigate = useNavigate();
  const loaded = useRef(false);
  const [codes, setCodes] = useState<string[] | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  useEffect(() => {
    if (loaded.current) return;
    loaded.current = true;
    setCodes(takePendingRecoveryCodes());
  }, []);

  const copyAll = async () => {
    if (!codes) return;
    try { await navigator.clipboard.writeText(codes.join("\n")); await messageApi.success("Коды скопированы"); }
    catch { await messageApi.error("Не удалось скопировать коды"); }
  };

  if (!codes) return <main className="center-page"><Card className="auth-card"><Result status="info" title="Коды уже закрыты" subTitle="Резервные коды показываются один раз. Если вы не успели их сохранить, создайте новый набор в настройках безопасности." extra={<Button type="primary" onClick={() => navigate("/security", { replace: true })}>Открыть настройки безопасности</Button>} /></Card></main>;

  return <main className="center-page">{contextHolder}<Card className="auth-card auth-card-wide">
    <div className="auth-mark"><SafetyCertificateOutlined /></div>
    <Typography.Title level={2}>Сохраните резервные коды</Typography.Title>
    <Typography.Paragraph type="secondary">Каждый код можно использовать один раз, если приложение-аутентификатор недоступно. Сохраните их в надёжном месте: повторно этот список не появится.</Typography.Paragraph>
    <Alert type="warning" showIcon message="Не передавайте эти коды другим людям" style={{ marginBottom: 20 }} />
    <div className="recovery-code-grid" aria-label="Резервные коды">{codes.map(code => <code key={code}>{code}</code>)}</div>
    <div className="recovery-actions"><Button icon={<CopyOutlined />} onClick={copyAll}>Скопировать коды</Button><Button className="primary-button" type="primary" icon={<CheckCircleOutlined />} onClick={() => { setCodes(null); navigate("/", { replace: true }); }}>Я сохранил коды</Button></div>
  </Card></main>;
}
