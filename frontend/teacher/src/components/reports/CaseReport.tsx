import { Card, List, Tag, Typography } from "antd";
import type { CaseReport as ReportDto } from "../../types/api";

export function CaseReport({ report, itemCount, levelCount }: { report: ReportDto; itemCount?: number; levelCount?: number }) {
  if (!report.participants.length) return <Typography.Text type="secondary">Попыток пока нет</Typography.Text>;
  const total = itemCount ?? levelCount ?? 0;
  return <>{report.participants.map(p => <Card type="inner" key={p.displayName} title={p.displayName} extra={<Tag color="blue">Лучший прогресс: {p.bestProgress} из {total}</Tag>} style={{ marginBottom: 12 }}><List dataSource={p.attempts} renderItem={a => <List.Item>{new Date(a.startedAt).toLocaleString("ru-RU")} · {a.status} · пройдено {a.completedStepCount} шагов</List.Item>} /></Card>)}</>;
}
