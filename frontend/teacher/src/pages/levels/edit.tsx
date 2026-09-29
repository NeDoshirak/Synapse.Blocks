import { Alert, Button, Card, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { LevelDefinitionForm } from "../../components/levels/LevelDefinitionForm";
import { apiClient } from "../../providers/apiClient";
import type { TeacherLevel } from "../../types/api";
export default function LevelEdit({create=false}:{create?:boolean}) {
 const {id}=useParams();const navigate=useNavigate();const [level,setLevel]=useState<TeacherLevel>();const [error,setError]=useState("");
 useEffect(()=>{if(!create&&id)apiClient.request(`/api/teacher/levels/${id}`).then(v=>setLevel(v as TeacherLevel)).catch(e=>setError(e.message));},[create,id]);
 return <><Button type="link" href="/admin/levels">← Все уровни</Button><Typography.Title level={2}>{create?"Новый уровень":level?.title??"Загрузка…"}</Typography.Title>{error?<Alert type="error" message={error} />:<Card>{(create||level)&&<LevelDefinitionForm key={level?.id??"new"} level={level} onSaved={saved=>create?navigate(`/levels/${saved.id}/edit`):setLevel(saved)} />}</Card>}{level&&<Typography.Text type="secondary">Сохранена версия {level.version}. Уже опубликованные наборы сохраняют прежнюю версию уровня.</Typography.Text>}<Link to="/levels">Назад</Link></>;
}
