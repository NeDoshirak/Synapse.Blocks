import { Authenticated, Refine } from "@refinedev/core";
import { ThemedLayout, ErrorComponent } from "@refinedev/antd";
import routerProvider, { CatchAllNavigate } from "@refinedev/react-router";
import { Link, Navigate, Outlet, Route, Routes } from "react-router";
import { AppstoreOutlined, BookOutlined, QrcodeOutlined, UserAddOutlined } from "@ant-design/icons";
import { Button, Card, Col, Row, Typography } from "antd";
import { authProvider } from "./providers/authProvider";
import { dataProvider } from "./providers/dataProvider";
import LoginPage from "./pages/login";
import InvitationsPage from "./pages/platform/invitations";
import AcceptInvitationPage from "./pages/invitations/accept";
import LevelsList from "./pages/levels/list";
import LevelEdit from "./pages/levels/edit";
import CasesList from "./pages/cases/list";
import CaseEdit from "./pages/cases/edit";

function Dashboard() {
  return <><Typography.Title level={2}>Добро пожаловать</Typography.Title><Typography.Paragraph type="secondary">Создавайте задания, собирайте маршруты и следите за прогрессом учеников.</Typography.Paragraph><Row gutter={[18, 18]}>{[
    { title: "Уровни", text: "Настройте задачи и тесты для своих занятий", icon: <BookOutlined />, href: "/levels" },
    { title: "Наборы и QR", text: "Объедините уровни и выдайте QR-код классу", icon: <QrcodeOutlined />, href: "/cases" },
    { title: "Учителя", text: "Создайте доступ коллеге по приглашению", icon: <UserAddOutlined />, href: "/invitations" },
  ].map(item => <Col xs={24} md={8} key={item.title}><Card className="dashboard-card"><div className="card-icon">{item.icon}</div><Typography.Title level={4}>{item.title}</Typography.Title><Typography.Paragraph type="secondary">{item.text}</Typography.Paragraph><Button type="link" href={`/admin${item.href}`}>Открыть →</Button></Card></Col>)}</Row></>;
}

export default function App() {
  return <Refine authProvider={authProvider} dataProvider={dataProvider} routerProvider={routerProvider} resources={[
    { name: "levels", list: "/levels", meta: { label: "Уровни", icon: <BookOutlined /> } },
    { name: "cases", list: "/cases", meta: { label: "Наборы и QR", icon: <QrcodeOutlined /> } },
    { name: "invitations", list: "/invitations", meta: { label: "Приглашения", icon: <UserAddOutlined />, hide: true } },
  ]} options={{ syncWithLocation: true, warnWhenUnsavedChanges: true, title: { text: "Synapse Blocks", icon: <AppstoreOutlined /> } }}>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/invitations/accept" element={<AcceptInvitationPage />} />
      <Route element={<Authenticated key="private" fallback={<CatchAllNavigate to="/login" />}><ThemedLayout Title={() => <Link className="brand" to="/">Synapse Blocks</Link>}><Outlet /></ThemedLayout></Authenticated>}>
        <Route index element={<Dashboard />} />
        <Route path="invitations" element={<InvitationsPage />} />
        <Route path="levels" element={<LevelsList />} />
        <Route path="levels/create" element={<LevelEdit create />} />
        <Route path="levels/:id/edit" element={<LevelEdit />} />
        <Route path="cases" element={<CasesList />} />
        <Route path="cases/create" element={<CaseEdit create />} />
        <Route path="cases/:id" element={<CaseEdit />} />
        <Route path="*" element={<ErrorComponent />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  </Refine>;
}
