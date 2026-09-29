import { PlusOutlined } from "@ant-design/icons";
import { Button, Card, Space, Table, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link } from "react-router";
import { apiClient } from "../../providers/apiClient";
import type { TeacherLevel } from "../../types/api";
export default function LevelsList() {
 const [rows,setRows]=useState<TeacherLevel[]>([]); const [loading,setLoading]=useState(true);
 useEffect(()=>{apiClient.request("/api/teacher/levels").then(data=>setRows(data as TeacherLevel[])).finally(()=>setLoading(false));},[]);
 return <><Space style={{ width:"100%",justifyContent:"space-between" }}><div><Typography.Title level={2}>Каталог уровней</Typography.Title><Typography.Text type="secondary">Уровни и тесты, доступные только вам</Typography.Text></div><Button type="primary" icon={<PlusOutlined />} href="/admin/levels/create">Новый уровень</Button></Space><Card style={{ marginTop:24 }}><Table rowKey="id" loading={loading} dataSource={rows} columns={[{title:"Уровень",dataIndex:"title"},{title:"Версия",dataIndex:"version",render:v=>`v${v}`},{title:"Тесты",render:(_,r)=>r.definition.tests.length},{title:"",render:(_,r)=><Link to={`/levels/${r.id}/edit`}>Открыть</Link>}]} /></Card></>;
}
