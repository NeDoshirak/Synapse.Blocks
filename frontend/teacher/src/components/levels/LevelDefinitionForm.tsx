import { Alert, Button, Card, Checkbox, Form, Input, InputNumber, Select, Space, Typography } from "antd";
import { useEffect, useState } from "react";
import type { LevelDefinition, TeacherLevel } from "../../types/api";
import { apiClient } from "../../providers/apiClient";
import { makeLevelUpdate } from "./levelDefinition";

const blockOptions = ["Input", "Operation", "Condition", "Loop", "Variable", "VariableAction", "Output"].map(label => ({ value: label, label }));
const blankDefinition = (): LevelDefinition => ({ id: crypto.randomUUID(), title: "Новый уровень", description: "", objective: "", allowedBlocks: ["Input", "Operation", "Condition", "Loop", "Variable"], requiredBlocks: [], tests: [], introSteps: [] });

export function LevelDefinitionForm({ level, initialDefinition, onSaved }: { level?: TeacherLevel; initialDefinition?: LevelDefinition; onSaved: (value: TeacherLevel) => void }) {
  const [form] = Form.useForm<LevelDefinition>();
  const [saving, setSaving] = useState(false);
  const [hasTests, setHasTests] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => { const value = level?.definition ?? initialDefinition ?? blankDefinition(); form.setFieldsValue(value as never); setHasTests(value.tests.length > 0); }, [form, level, initialDefinition]);
  const save = async (definition: LevelDefinition) => {
    setSaving(true); setError("");
    try {
      const value = level
        ? await apiClient.request(`/api/teacher/levels/${level.id}`, { method: "PUT", body: JSON.stringify(makeLevelUpdate(level.definition, { ...level.definition, ...definition }, level.currentVersionId)) })
        : await apiClient.request("/api/teacher/levels", { method: "POST", body: JSON.stringify({ definition }) });
      onSaved(value as TeacherLevel);
    } catch (e) { setError(e instanceof Error ? e.message : "Не удалось сохранить уровень. Повторите попытку."); }
    finally { setSaving(false); }
  };
  return <Form form={form} layout="vertical" onFinish={save} onValuesChange={(_, values) => setHasTests((values.tests?.length ?? 0) > 0)}>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Form.Item name="title" label="Название уровня" rules={[{ required: true, message: "Введите название" }]}><Input size="large" /></Form.Item>
    <Form.Item name="description" label="Описание для учителя"><Input.TextArea rows={3} /></Form.Item>
    <Form.Item name="objective" label="Что должен сделать ученик"><Input.TextArea rows={2} /></Form.Item>
    <Card size="small" title="Правила блоков" style={{ marginBottom: 20 }}><Form.Item name="allowedBlocks" label="Разрешённые блоки"><Select mode="multiple" options={blockOptions} /></Form.Item><Form.Item name="requiredBlocks" label="Обязательные блоки"><Select mode="multiple" options={blockOptions} /></Form.Item><Space><Form.Item name="requiredLoopCount" label="Циклов в решении"><InputNumber min={0} /></Form.Item><Form.Item name="maxOperationBlocks" label="Максимум операций"><InputNumber min={1} /></Form.Item></Space></Card>
    <Typography.Title level={4}>Тесты</Typography.Title>
    <Form.List name="tests">{(fields, { add, remove }) => <Space direction="vertical" style={{ width: "100%" }} size="middle">{fields.map(({ key, name, ...rest }) => <Card size="small" key={key} title={`Тест ${name + 1}`} extra={<Button danger type="link" onClick={() => remove(name)}>Удалить</Button>}><Form.Item {...rest} name={[name, "name"]} label="Название" rules={[{ required: true }]}><Input /></Form.Item><Space align="start" style={{ width: "100%" }}><Form.Item {...rest} name={[name, "input"]} label="Входные данные" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item><Form.Item {...rest} name={[name, "expectedOutput"]} label="Ожидаемый ответ" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item></Space><Form.Item {...rest} name={[name, "hidden"]} valuePropName="checked"><Checkbox>Скрытый тест — ответ ученику не показывается</Checkbox></Form.Item></Card>)}<Button onClick={() => add({ id: crypto.randomUUID(), name: "Новый тест", input: "", expectedOutput: "", hidden: false })}>Добавить тест</Button></Space>}</Form.List>
    <Typography.Title level={4} style={{ marginTop: 24 }}>Вводные шаги</Typography.Title>
    <Form.List name="introSteps">{(fields, { add, remove }) => <Space direction="vertical" style={{ width: "100%" }}>{fields.map(({ key, name, ...rest }) => <Card size="small" key={key} title={`Шаг ${name + 1}`} extra={<Button danger type="link" onClick={() => remove(name)}>Удалить</Button>}><Form.Item {...rest} name={[name, "title"]} label="Заголовок"><Input /></Form.Item><Form.Item {...rest} name={[name, "body"]} label="Текст"><Input.TextArea rows={3} /></Form.Item><Form.Item {...rest} name={[name, "mediaUrl"]} label="Ссылка на иллюстрацию (необязательно)"><Input /></Form.Item></Card>)}<Button onClick={() => add({ id: crypto.randomUUID(), title: "Шаг", body: "", mediaUrl: "" })}>Добавить шаг</Button></Space>}</Form.List>
    <Button type="primary" htmlType="submit" loading={saving} disabled={!level && !hasTests} style={{ marginTop: 24 }}>Сохранить новую версию</Button>
  </Form>;
}
