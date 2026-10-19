import { useCallback, useEffect, useState } from "react";
import { Button, Card, Empty, message, Space, Spin, Tag, Typography } from "antd";
import { PlusOutlined, ReadOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router";
import type { MarkdownPage } from "../../types/api";
import { archiveMarkdownPage, listMarkdownPages, restoreMarkdownPage } from "../../resources/markdownPages";

export default function MarkdownPageList() {
  const navigate = useNavigate();
  const [pages, setPages] = useState<MarkdownPage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try { setPages(await listMarkdownPages()); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Не удалось загрузить страницы."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const archive = async (page: MarkdownPage) => {
    try {
      await archiveMarkdownPage(page.id);
      message.success("Страница помещена в архив");
      await load();
    } catch (reason) { message.error(reason instanceof Error ? reason.message : "Не удалось архивировать страницу."); }
  };
  const restore = async (page: MarkdownPage) => {
    try {
      await restoreMarkdownPage(page.id);
      message.success("Страница восстановлена");
      await load();
    } catch (reason) { message.error(reason instanceof Error ? reason.message : "Не удалось восстановить страницу."); }
  };

  return <div className="markdown-library-page">
    <div className="markdown-page-heading">
      <div><Typography.Text className="eyebrow">БИБЛИОТЕКА МАТЕРИАЛОВ</Typography.Text><Typography.Title level={2}>Markdown-страницы</Typography.Title><Typography.Paragraph type="secondary">Создавайте объяснения и вставляйте их в наборы как отдельные шаги.</Typography.Paragraph></div>
      <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate("/markdown-pages/create")}>Новая страница</Button>
    </div>
    {error && <Card className="markdown-page-error">{error}<Button type="link" onClick={() => void load()}>Повторить</Button></Card>}
    {loading ? <div className="markdown-page-loading"><Spin /></div> : pages.length ? <div className="markdown-page-list">
      {pages.map(page => <Card key={page.id} className={`markdown-library-card ${page.isArchived ? "is-archived" : ""}`}>
        <div className="markdown-library-card-copy"><span className="markdown-library-icon"><ReadOutlined /></span><div><Space wrap><Typography.Title level={4}>{page.title}</Typography.Title>{page.isBuiltIn && <Tag color="orange">Встроенная</Tag>}{page.isArchived && <Tag>В архиве</Tag>}</Space><Typography.Text type="secondary">Версия {page.version}</Typography.Text></div></div>
        <Space wrap><Button disabled={page.isBuiltIn} onClick={() => navigate(`/markdown-pages/${page.id}/edit`)}>Открыть</Button>{page.isArchived ? <Button disabled={page.isBuiltIn} onClick={() => void restore(page)}>Восстановить</Button> : <Button danger disabled={page.isBuiltIn} onClick={() => void archive(page)}>В архив</Button>}</Space>
      </Card>)}
    </div> : <Card><Empty description="Страниц пока нет"><Button type="primary" onClick={() => navigate("/markdown-pages/create")}>Создать первую страницу</Button></Empty></Card>}
  </div>;
}
