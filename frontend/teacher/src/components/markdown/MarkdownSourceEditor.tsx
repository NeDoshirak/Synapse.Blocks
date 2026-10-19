import { useRef, useState } from "react";
import type { ClipboardEvent } from "react";
import { Alert, Button, Space, Typography } from "antd";
import {
  BoldOutlined,
  CodeOutlined,
  ItalicOutlined,
  LinkOutlined,
  OrderedListOutlined,
  PictureOutlined,
  TableOutlined,
  UnorderedListOutlined,
} from "@ant-design/icons";
import type { MarkdownAsset } from "../../types/api";

const maxMarkdownBytes = 100 * 1024;

export function markdownByteLength(value: string) {
  return new TextEncoder().encode(value).length;
}

export function MarkdownSourceEditor({
  value,
  onChange,
  onUploadImage,
  disabled = false,
}: {
  value: string;
  onChange: (value: string) => void;
  onUploadImage: (file: File) => Promise<MarkdownAsset>;
  disabled?: boolean;
}) {
  const editorRef = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const valueRef = useRef(value);
  valueRef.current = value;
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState("");
  const bytes = markdownByteLength(value);

  const insert = (before: string, after = "", emptyText = "", savedRange?: { start: number; end: number }) => {
    const editor = editorRef.current;
    const currentValue = valueRef.current;
    const start = savedRange?.start ?? editor?.selectionStart ?? currentValue.length;
    const end = savedRange?.end ?? editor?.selectionEnd ?? currentValue.length;
    const selected = currentValue.slice(start, end) || emptyText;
    const next = `${currentValue.slice(0, start)}${before}${selected}${after}${currentValue.slice(end)}`;
    valueRef.current = next;
    onChange(next);
    requestAnimationFrame(() => {
      editor?.focus();
      const caretStart = start + before.length;
      const caretEnd = caretStart + selected.length;
      editor?.setSelectionRange(caretStart, caretEnd);
    });
  };

  const insertLine = (prefix: string, emptyText: string) => insert(`\n${prefix}`, "\n", emptyText);

  const upload = async (file?: File, savedRange?: { start: number; end: number }) => {
    if (!file) return;
    setUploading(true);
    setUploadError("");
    try {
      const asset = await onUploadImage(file);
      insert(`![${file.name.replace(/\.[^.]+$/, "") || "Изображение"}](asset:${asset.id})`, "", "", savedRange);
    } catch (reason) {
      setUploadError(reason instanceof Error ? reason.message : "Не удалось загрузить изображение.");
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const handlePaste = (event: ClipboardEvent<HTMLTextAreaElement>) => {
    const item = Array.from(event.clipboardData.items).find(candidate => candidate.type.startsWith("image/"));
    const file = item?.getAsFile();
    if (!file) return;
    event.preventDefault();
    void upload(file, { start: event.currentTarget.selectionStart, end: event.currentTarget.selectionEnd });
  };

  return <div className="markdown-source-editor">
    <Space wrap size={[6, 6]} className="markdown-toolbar">
      <Button size="small" title="Заголовок" disabled={disabled} onClick={() => insert("## ", "", "Заголовок")}>H</Button>
      <Button size="small" icon={<BoldOutlined />} title="Жирный" disabled={disabled} onClick={() => insert("**", "**", "текст")} />
      <Button size="small" icon={<ItalicOutlined />} title="Курсив" disabled={disabled} onClick={() => insert("*", "*", "текст")} />
      <Button size="small" icon={<UnorderedListOutlined />} title="Маркированный список" disabled={disabled} onClick={() => insertLine("- ", "Пункт списка")} />
      <Button size="small" icon={<OrderedListOutlined />} title="Нумерованный список" disabled={disabled} onClick={() => insertLine("1. ", "Пункт списка")} />
      <Button size="small" title="Чек-лист" disabled={disabled} onClick={() => insertLine("- [ ] ", "Задача")}>☑</Button>
      <Button size="small" icon={<LinkOutlined />} title="Ссылка" disabled={disabled} onClick={() => insert("[", "](https://example.com)", "текст ссылки")} />
      <Button size="small" icon={<PictureOutlined />} title="Загрузить изображение" loading={uploading} disabled={disabled} onClick={() => fileRef.current?.click()} />
      <Button size="small" icon={<TableOutlined />} title="Таблица" disabled={disabled} onClick={() => insert("\n", "\n", "| Колонка | Колонка |\n| --- | --- |\n| Ячейка | Ячейка |")} />
      <Button size="small" icon={<CodeOutlined />} title="Блок кода" disabled={disabled} onClick={() => insert("\n```text\n", "\n```\n", "Код")} />
      <Button size="small" title="Формула" disabled={disabled} onClick={() => insert("$$\n", "\n$$\n", "E = mc^2")}><span className="formula-toolbar-icon">∑</span></Button>
      <Button size="small" title="Диаграмма Mermaid" disabled={disabled} onClick={() => insert("```mermaid\n", "\n```\n", "flowchart LR\n  A[Начало] --> B[Продолжение]")}><span className="diagram-toolbar-icon">◇</span></Button>
      <input ref={fileRef} type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={event => void upload(event.currentTarget.files?.[0])} />
    </Space>
    {uploadError && <Alert type="error" showIcon message={uploadError} style={{ margin: "8px 0" }} />}
    <textarea
      ref={editorRef}
      className="markdown-source-textarea"
      value={value}
      onChange={event => onChange(event.target.value)}
      disabled={disabled}
      spellCheck
      aria-label="Исходник Markdown"
      placeholder="# Заголовок\n\nНапишите объяснение для учеников…"
      onPaste={handlePaste}
    />
    <Typography.Text className={`markdown-byte-count ${bytes > maxMarkdownBytes ? "is-over-limit" : ""}`}>
      {(bytes / 1024).toFixed(1)} / 100 КиБ
    </Typography.Text>
  </div>;
}
