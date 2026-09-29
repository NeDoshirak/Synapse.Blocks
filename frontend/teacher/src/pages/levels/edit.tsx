import { Alert, Button, Card, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { LevelDefinitionForm } from "../../components/levels/LevelDefinitionForm";
import { apiClient } from "../../providers/apiClient";
import type { TeacherLevel } from "../../types/api";
import type { LevelDefinition as LevelDefinitionType } from "../../types/api";
import { StarterTemplatePicker } from "../../components/levels/StarterTemplatePicker";
import { cloneLevelDefinition } from "../../components/levels/levelDefinition";
export default function LevelEdit({create=false}:{create?:boolean}) {
 const {id}=useParams();const navigate=useNavigate();const [level,setLevel]=useState<TeacherLevel>();const[templates,setTemplates]=useState<TeacherLevel[]>([]);const[initial,setInitial]=useState<LevelDefinitionType>();const [error,setError]=useState("");
 useEffect(()=>{if(!create&&id)apiClient.request(`/api/teacher/levels/${id}`).then(v=>setLevel(v as TeacherLevel)).catch(e=>setError(e.message));if(create)apiClient.request("/api/teacher/levels").then(v=>setTemplates(v as TeacherLevel[])).catch(e=>setError(e.message));},[create,id]);
 return <><Button type="link" href="/admin/levels">← Все уровни</Button><Typography.Title level={2}>{create?"Новый уровень":level?.title??"Загрузка…"}</Typography.Title>{error?<Alert type="error" message={error} />:<Card>{create&&<StarterTemplatePicker levels={templates} value={templates.find(x=>x.definition.id===initial?.id)?.id} onChange={template=>setInitial(template?cloneLevelDefinition(template.definition):undefined)} />}{(create||level)&&<LevelDefinitionForm key={level?.id??initial?.id??"new"} level={level} initialDefinition={initial} onSaved={saved=>create?navigate(`/levels/${saved.id}/edit`):setLevel(saved)} />}</Card>}{level&&<Typography.Text type="secondary">Сохранена версия {level.version}. Уже опубликованные наборы сохраняют прежнюю версию уровня.</Typography.Text>}<Link to="/levels">Назад</Link></>;
}
