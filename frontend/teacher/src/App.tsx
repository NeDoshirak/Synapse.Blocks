import { Authenticated, Refine, useGetIdentity, useLogout } from "@refinedev/core";
import { ErrorComponent, ThemedLayout } from "@refinedev/antd";
import routerProvider from "@refinedev/react-router";
import { ConfigProvider, Button, Card, Col, Layout, Menu, Row, Spin, Typography } from "antd";
import { AppstoreOutlined, BookOutlined, QrcodeOutlined, ReadOutlined, SafetyCertificateOutlined, TeamOutlined, UserOutlined, LogoutOutlined } from "@ant-design/icons";
import { Link, Navigate, Outlet, Route, Routes, useLocation, useNavigate } from "react-router";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { authProvider } from "./providers/authProvider";
import { apiClient } from "./providers/apiClient";
import { dataProvider } from "./providers/dataProvider";
import LoginPage from "./pages/login";
import AcceptInvitationPage from "./pages/invitations/accept";
import AuthenticatorSetupPage from "./pages/authenticator-setup";
import TwoFactorChallengePage from "./pages/two-factor-challenge";
import RecoveryCodesPage from "./pages/recovery-codes";
import PasswordResetPage from "./pages/password-reset";
import SecuritySettingsPage from "./pages/security";
import LevelsList from "./pages/levels/list";
import LevelEdit from "./pages/levels/edit";
import CasesList from "./pages/cases/list";
import CaseEdit from "./pages/cases/edit";
import PlatformTeachersPage from "./pages/platform/teachers";
import PlatformDashboard from "./pages/platform/dashboard";
import PlatformCasesList from "./pages/platform/cases/list";
import PlatformCaseEdit from "./pages/platform/cases/edit";
import MarkdownPageList from "./pages/markdown-pages/list";
import MarkdownPageEdit from "./pages/markdown-pages/edit";
import type { CurrentUser } from "./types/api";

function Dashboard() {
  return <><Typography.Text className="eyebrow">КАБИНЕТ ПРЕПОДАВАТЕЛЯ</Typography.Text><Typography.Title level={2}>Добро пожаловать</Typography.Title><Typography.Paragraph type="secondary">Подготовьте материалы для занятия и соберите их в набор для учеников.</Typography.Paragraph><Row gutter={[18, 18]}>{[
    { title: "Каталог уровней", text: "Настройте задачи и тесты для своих занятий", icon: <BookOutlined />, href: "/levels" },
    { title: "Мои наборы", text: "Объедините задания и выдайте ссылку или QR-код классу", icon: <QrcodeOutlined />, href: "/cases" },
    { title: "Markdown-страницы", text: "Создайте объяснение с формулами, схемами и изображениями", icon: <ReadOutlined />, href: "/markdown-pages" },
  ].map(item => <Col xs={24} md={12} key={item.title}><Card className="dashboard-card"><div className="card-icon">{item.icon}</div><Typography.Title level={4}>{item.title}</Typography.Title><Typography.Paragraph type="secondary">{item.text}</Typography.Paragraph><Button type="link" href={`/admin${item.href}`}>Открыть →</Button></Card></Col>)}</Row></>;
}

function WorkspaceSidebar({ identity }: { identity: CurrentUser }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { mutate: logout, isPending } = useLogout();
  const isPlatformAdmin = identity.roles.includes("PlatformAdmin");
  const items = isPlatformAdmin ? [
    {
      key: "teaching",
      type: "group" as const,
      label: "МОЁ ПРЕПОДАВАНИЕ",
      children: [
        { key: "/teaching", icon: <AppstoreOutlined />, label: "Мой кабинет" },
        { key: "/levels", icon: <BookOutlined />, label: "Мои уровни" },
        { key: "/cases", icon: <QrcodeOutlined />, label: "Мои наборы" },
        { key: "/markdown-pages", icon: <ReadOutlined />, label: "Мои Markdown-страницы" },
      ],
    },
    {
      key: "platform",
      type: "group" as const,
      label: "УПРАВЛЕНИЕ ПЛАТФОРМОЙ",
      children: [
        { key: "/platform/teachers", icon: <TeamOutlined />, label: "Преподаватели" },
        { key: "/platform/cases", icon: <QrcodeOutlined />, label: "Все наборы" },
        { key: "/platform/dashboard", icon: <AppstoreOutlined />, label: "Обзор платформы" },
        { key: "/security", icon: <SafetyCertificateOutlined />, label: "Безопасность" },
      ],
    },
  ] : [
    { key: "/", icon: <AppstoreOutlined />, label: "Обзор" },
    { key: "/levels", icon: <BookOutlined />, label: "Уровни" },
    { key: "/cases", icon: <QrcodeOutlined />, label: "Мои наборы" },
    { key: "/markdown-pages", icon: <ReadOutlined />, label: "Markdown-страницы" },
    { key: "/security", icon: <SafetyCertificateOutlined />, label: "Безопасность" },
  ];
  const selected = ["/teaching", "/levels", "/cases", "/markdown-pages", "/platform/teachers", "/platform/cases", "/platform/dashboard", "/security", "/"]
    .find(key => location.pathname === key || (key !== "/" && location.pathname.startsWith(`${key}/`)));
  const name = [identity.firstName, identity.lastName].filter(Boolean).join(" ") || identity.displayName || identity.email;
  return <Layout.Sider className="admin-sider" width={252} breakpoint="lg" collapsedWidth={0}>
    <Link className="sidebar-brand" to={isPlatformAdmin ? "/platform/teachers" : "/"}><span className="brand-mark">ФК</span><span><strong>ФВС/Код</strong><small>учебная платформа</small></span></Link>
    <div className="sidebar-profile"><span className="profile-avatar"><UserOutlined /></span><span className="profile-copy"><strong>{name}</strong><small>{isPlatformAdmin ? "Суперадмин" : "Преподаватель"}</small></span></div>
    <Menu className="workspace-menu" theme="dark" mode="inline" selectedKeys={selected ? [selected] : []} items={items} onClick={({ key }) => navigate(key)} />
    <div className="sidebar-bottom"><Button type="text" icon={<LogoutOutlined />} loading={isPending} onClick={() => logout()} block>Выйти</Button></div>
  </Layout.Sider>;
}

function WorkspaceLayout() {
  const { data: identity, isLoading } = useGetIdentity<CurrentUser>();
  if (isLoading) return <div className="workspace-loading"><Spin size="large" /></div>;
  if (!identity) return <Navigate to="/login" replace />;
  return <ThemedLayout Sider={() => <WorkspaceSidebar identity={identity} />} Title={() => <span className="workspace-title">ФВС/Код</span>}><Outlet /></ThemedLayout>;
}

function RequireRole({ role, children }: { role: "Teacher" | "PlatformAdmin"; children: ReactNode }) {
  const { data: identity, isLoading } = useGetIdentity<CurrentUser>();
  if (isLoading) return <div className="workspace-loading"><Spin /></div>;
  const canAccess = identity?.roles.includes(role) || (role === "Teacher" && identity?.roles.includes("PlatformAdmin"));
  if (!canAccess) return <Navigate to={identity?.roles.includes("PlatformAdmin") ? "/platform/teachers" : "/"} replace />;
  return children;
}

function RoleHome() {
  const { data: identity, isLoading } = useGetIdentity<CurrentUser>();
  if (isLoading) return <div className="workspace-loading"><Spin /></div>;
  return identity?.roles.includes("PlatformAdmin") ? <Navigate to="/platform/teachers" replace /> : <Dashboard />;
}

function AuthFallback() {
  const [destination, setDestination] = useState("");
  useEffect(() => {
    let active = true;
    void apiClient.request("/api/auth/me")
      .then(user => { if (active) setDestination((user as CurrentUser).twoFactorEnabled ? "/" : "/authenticator-setup"); })
      .catch(() => { if (active) setDestination("/login"); });
    return () => { active = false; };
  }, []);
  return destination ? <Navigate to={destination} replace /> : <div className="workspace-loading"><Spin /></div>;
}

export default function App() {
  return <ConfigProvider theme={{ token: { colorPrimary: "#F36F29", colorLink: "#B94B17", colorBgLayout: "#F7F5F1", colorText: "#262522", borderRadius: 10, fontFamily: "ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif" }, components: { Layout: { siderBg: "#242321", triggerBg: "#242321" }, Menu: { darkItemBg: "#242321", darkSubMenuItemBg: "#242321", darkItemSelectedBg: "#473328", darkItemSelectedColor: "#FF9A63" } } }}>
  <Refine authProvider={authProvider} dataProvider={dataProvider} routerProvider={routerProvider} resources={[
    { name: "levels", list: "/levels", meta: { label: "Уровни", icon: <BookOutlined /> } },
    { name: "cases", list: "/cases", meta: { label: "Наборы и QR", icon: <QrcodeOutlined /> } },
    { name: "markdown-pages", list: "/markdown-pages", meta: { label: "Markdown-страницы", icon: <ReadOutlined /> } },
    { name: "platform/teachers", list: "/platform/teachers", meta: { label: "Преподаватели", icon: <TeamOutlined /> } },
    { name: "platform/cases", list: "/platform/cases", meta: { label: "Наборы", icon: <QrcodeOutlined /> } },
  ]} options={{ syncWithLocation: true, warnWhenUnsavedChanges: true, title: { text: "ФВС/Код", icon: <AppstoreOutlined /> } }}>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/invitations/accept" element={<AcceptInvitationPage />} />
      <Route path="/authenticator-setup" element={<AuthenticatorSetupPage />} />
      <Route path="/two-factor-challenge" element={<TwoFactorChallengePage />} />
      <Route path="/recovery-codes" element={<RecoveryCodesPage />} />
      <Route path="/password-reset" element={<PasswordResetPage />} />
      <Route element={<Authenticated key="private" fallback={<AuthFallback />}><WorkspaceLayout /></Authenticated>}>
        <Route index element={<RoleHome />} />
        <Route path="teaching" element={<RequireRole role="Teacher"><Dashboard /></RequireRole>} />
        <Route path="security" element={<SecuritySettingsPage />} />
        <Route path="levels" element={<RequireRole role="Teacher"><LevelsList /></RequireRole>} />
        <Route path="levels/create" element={<RequireRole role="Teacher"><LevelEdit create /></RequireRole>} />
        <Route path="levels/:id/edit" element={<RequireRole role="Teacher"><LevelEdit /></RequireRole>} />
        <Route path="cases" element={<RequireRole role="Teacher"><CasesList /></RequireRole>} />
        <Route path="cases/create" element={<RequireRole role="Teacher"><CaseEdit create /></RequireRole>} />
        <Route path="cases/:id" element={<RequireRole role="Teacher"><CaseEdit /></RequireRole>} />
        <Route path="markdown-pages" element={<RequireRole role="Teacher"><MarkdownPageList /></RequireRole>} />
        <Route path="markdown-pages/create" element={<RequireRole role="Teacher"><MarkdownPageEdit create /></RequireRole>} />
        <Route path="markdown-pages/:id/edit" element={<RequireRole role="Teacher"><MarkdownPageEdit /></RequireRole>} />
        <Route path="invitations" element={<RequireRole role="PlatformAdmin"><Navigate to="/platform/teachers" replace /></RequireRole>} />
        <Route path="platform/dashboard" element={<RequireRole role="PlatformAdmin"><PlatformDashboard /></RequireRole>} />
        <Route path="platform/teachers" element={<RequireRole role="PlatformAdmin"><PlatformTeachersPage /></RequireRole>} />
        <Route path="platform/cases" element={<RequireRole role="PlatformAdmin"><PlatformCasesList /></RequireRole>} />
        <Route path="platform/cases/create" element={<RequireRole role="PlatformAdmin"><PlatformCaseEdit create /></RequireRole>} />
        <Route path="platform/cases/:id" element={<RequireRole role="PlatformAdmin"><PlatformCaseEdit /></RequireRole>} />
        <Route path="*" element={<ErrorComponent />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  </Refine>
  </ConfigProvider>;
}
