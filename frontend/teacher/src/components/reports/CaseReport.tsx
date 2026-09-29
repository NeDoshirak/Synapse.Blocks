import { Card, List, Tag, Typography } from "antd";
import type { CaseReport as ReportDto } from "../../types/api";

export function CaseReport({ report, levelCount }: { report: ReportDto; levelCount: number }) {
  if (!report.participants.length) return <Typography.Text type="secondary">Попыток пока нет</Typography.Text>;
  return <>{report.participants.map(p => <Card type="inner" key={p.displayName} title={p.displayName} extra={<Tag color="blue">Лучший прогресс: {p.bestProgress} из {levelCount}</Tag>} style={{ marginBottom: 12 }}><List dataSource={p.attempts} renderItem={a => <List.Item>{new Date(a.startedAt).toLocaleString("ru-RU")} · {a.status} · пройдено {a.completedLevelCount} уровней</List.Item>} /></Card>)}</>;
}
