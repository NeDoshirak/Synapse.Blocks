import { SafetyCertificateOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Popconfirm, Space, Typography, message } from "antd";
import { useState } from "react";
import { useNavigate } from "react-router";
import { apiClient } from "../providers/apiClient";
import { setPendingRecoveryCodes } from "../providers/recoveryCodeMemory";
import type { RecoveryCodes } from "../types/api";

export default function SecuritySettingsPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const regenerateRecoveryCodes = async () => {
    setLoading(true);
    try {
      const result = await apiClient.request("/api/auth/recovery-codes/regenerate", { method: "POST" }) as RecoveryCodes;
      setPendingRecoveryCodes(result.recoveryCodes);
      navigate("/recovery-codes", { replace: true });
    } catch (reason) {
      await messageApi.error(reason instanceof Error ? reason.message : "Не удалось создать новые резервные коды.");
    } finally {
      setLoading(false);
    }
  };

  return <div className="platform-page">
    {contextHolder}
    <div className="platform-page-heading">
      <div>
        <Typography.Text className="eyebrow">АККАУНТ</Typography.Text>
        <Typography.Title level={2}>Безопасность</Typography.Title>
        <Typography.Paragraph type="secondary">Управляйте дополнительной защитой входа и резервным доступом.</Typography.Paragraph>
      </div>
    </div>

    <Card className="platform-table-card" title={<Space><SafetyCertificateOutlined /><Typography.Title level={4} style={{ margin: 0 }}>Приложение-аутентификатор</Typography.Title></Space>}>
      <Typography.Paragraph>При входе используйте шестизначный код из Google Authenticator или другого совместимого приложения.</Typography.Paragraph>
      <Alert type="success" showIcon message="Двухфакторная защита включена" style={{ marginBottom: 20 }} />
      <Typography.Title level={5}>Резервные коды</Typography.Title>
      <Typography.Paragraph type="secondary">Резервный код поможет войти, если приложение недоступно. Создание нового набора сразу отменит все оставшиеся коды, а список появится только один раз.</Typography.Paragraph>
      <Popconfirm
        title="Создать новые резервные коды?"
        description="Все коды из предыдущего набора сразу перестанут работать."
        okText="Создать новые"
        cancelText="Отмена"
        onConfirm={() => void regenerateRecoveryCodes()}
      >
        <Button type="primary" className="primary-button" loading={loading}>Создать новые резервные коды</Button>
      </Popconfirm>
    </Card>
  </div>;
}
