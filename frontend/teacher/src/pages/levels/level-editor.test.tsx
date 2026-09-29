import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { LevelDefinitionForm } from "../../components/levels/LevelDefinitionForm";
import { apiClient } from "../../providers/apiClient";
import type { TeacherLevel } from "../../types/api";

const level: TeacherLevel = { id:"l1",title:"Первый",currentVersionId:"v1",version:1,definition:{id:"l1",title:"Первый",description:"",objective:"",allowedBlocks:[0,1],requiredBlocks:[],tests:[{id:"t1",name:"Видимый",input:"1",expectedOutput:"1",hidden:false},{id:"t2",name:"Скрытый",input:"9",expectedOutput:"secret",hidden:true}],introSteps:[]} };
afterEach(()=>{vi.restoreAllMocks();apiClient.invalidateCsrf();});

describe("level editor recovery",()=>{
 it("retains the teacher's title and hidden checks after a stale-version conflict",async()=>{
   apiClient.invalidateCsrf();
   vi.spyOn(globalThis,"fetch").mockResolvedValueOnce(new Response(JSON.stringify({requestToken:"csrf"}),{status:200})).mockResolvedValueOnce(new Response(JSON.stringify({error:"Reload required"}),{status:409}));
   render(<LevelDefinitionForm level={level} onSaved={()=>{}}/>);
   const title=await screen.findByLabelText("Название уровня");
   fireEvent.change(title,{target:{value:"Мой новый заголовок"}});
   fireEvent.submit(title.closest("form")!);
   expect(await screen.findByRole("alert")).toBeInTheDocument();
   expect(title).toHaveValue("Мой новый заголовок");
   await waitFor(()=>expect(screen.getByText("Сохранить новую версию")).toBeEnabled());
 });
});
