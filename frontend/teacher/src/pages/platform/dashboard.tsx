import { ArrowRightOutlined, BookOutlined, UserOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Col, Row, Skeleton, Space, Statistic, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link } from "react-router";
import { apiClient } from "../../providers/apiClient";
import type { PendingInvitation, TeacherAccount } from "../../types/api";

export default function PlatformDashboard() {
  const [teachers, setTeachers] = useState<TeacherAccount[]>([]);
  const [invitations, setInvitations] = useState<PendingInvitation[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([apiClient.request("/api/platform/teachers"), apiClient.request("/api/platform/invitations")])
      .then(([accounts, pending]) => { setTeachers(accounts as TeacherAccount[]); setInvitations(pending as PendingInvitation[]); })
      .catch(reason => setError(reason instanceof Error ? reason.message : "Не удалось загрузить обзор платформы."))
      .finally(() => setLoading(false));
  }, []);

  return <div className="platform-page">
    <div className="platform-page-heading"><div><Typography.Text className="eyebrow">ЦЕНТР УПРАВЛЕНИЯ</Typography.Text><Typography.Title level={2}>Обзор платформы</Typography.Title><Typography.Paragraph type="secondary">Учётные записи и учебные наборы — в одном месте.</Typography.Paragraph></div><Tag color="orange" className="role-tag">Суперадмин</Tag></div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 20 }} />}
    <Row gutter={[16, 16]}>
      <Col xs={24} md={8}><Card className="overview-stat"><Statistic title="Преподаватели" value={loading ? "—" : teachers.length} prefix={<UserOutlined />} /><Typography.Text type="secondary">{teachers.filter(item => item.isActive).length} активных · {teachers.filter(item => !item.isActive).length} заблокировано</Typography.Text></Card></Col>
      <Col xs={24} md={8}><Card className="overview-stat"><Statistic title="Ожидают входа" value={loading ? "—" : invitations.length} prefix={<ArrowRightOutlined />} /><Typography.Text type="secondary">активные приглашения</Typography.Text></Card></Col>
      <Col xs={24} md={8}><Card className="overview-stat"><Statistic title="Требуют внимания" value={loading ? "—" : teachers.filter(item => !item.twoFactorEnabled).length} prefix={<BookOutlined />} /><Typography.Text type="secondary">не завершили настройку 2FA</Typography.Text></Card></Col>
    </Row>
    <Row gutter={[16, 16]} style={{ marginTop: 8 }}>
      <Col xs={24} md={12}><Card className="overview-action-card"><Space direction="vertical" size="middle"><div className="card-icon"><UserOutlined /></div><div><Typography.Title level={4}>Команда преподавателей</Typography.Title><Typography.Paragraph type="secondary">Приглашения, профили, статусы и настройки безопасности аккаунтов.</Typography.Paragraph></div><Button type="primary" href="/admin/platform/teachers">Открыть преподавателей <ArrowRightOutlined /></Button></Space></Card></Col>
      <Col xs={24} md={12}><Card className="overview-action-card"><Space direction="vertical" size="middle"><div className="card-icon"><BookOutlined /></div><div><Typography.Title level={4}>Учебные наборы</Typography.Title><Typography.Paragraph type="secondary">Просматривайте и редактируйте наборы всех преподавателей, сохраняя владельца.</Typography.Paragraph></div><Button href="/admin/platform/cases">Открыть наборы <ArrowRightOutlined /></Button></Space></Card></Col>
    </Row>
    {loading && <Skeleton active style={{ marginTop: 24 }} />}
    {!loading && teachers.length > 0 && <Card className="platform-table-card" title={<Typography.Title level={4}>Недавние профили</Typography.Title>} extra={<Link to="/platform/teachers">Все преподаватели</Link>} style={{ marginTop: 24 }}><div className="recent-teachers">{teachers.slice(0, 5).map(teacher => <div className="recent-teacher" key={teacher.id}><span className="teacher-avatar">{(teacher.firstName?.[0] ?? teacher.email[0] ?? "П").toLocaleUpperCase("ru-RU")}</span><div><Typography.Text strong>{teacher.displayName}</Typography.Text><br /><Typography.Text type="secondary">{teacher.email}</Typography.Text></div><Tag color={teacher.isActive ? "green" : "red"}>{teacher.isActive ? "Активен" : "Заблокирован"}</Tag></div>)}</div></Card>}
  </div>;
}
