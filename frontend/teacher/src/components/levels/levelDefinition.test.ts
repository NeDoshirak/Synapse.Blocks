import { describe, expect, it } from "vitest";
import { cloneLevelDefinition, makeLevelUpdate } from "./levelDefinition";
import type { LevelDefinition } from "../../types/api";

const previous: LevelDefinition = { id:"level-1", title:"Old", description:"desc", objective:"goal", allowedBlocks:["Input","Operation"], requiredBlocks:["Operation"], tests:[{id:"visible",name:"Visible",input:"1",expectedOutput:"1",hidden:false},{id:"hidden",name:"Secret",input:"99",expectedOutput:"42",hidden:true}], introSteps:[{id:"intro",title:"Explain",body:"Body",mediaUrl:"/a.png",speaker:"guide"}] };

describe("level editing payload",()=>{
 it("preserves hidden test data, template-only fields, and test identifiers when editing metadata",()=>{
   const edited={...previous,title:"New title",tests:previous.tests.map(x=>({...x}))};
   const result=makeLevelUpdate(previous,edited,"version-7");
   expect(result.definition.tests.find(x=>x.hidden)).toMatchObject({id:"hidden",expectedOutput:"42"});
   expect(result.definition.introSteps[0]).toMatchObject({speaker:"guide"});
   expect(result.definition.id).toBe("level-1");
   expect(result.expectedVersionId).toBe("version-7");
 });
 it("retains the old test id when a test row is edited",()=>{
   const edited={...previous,tests:[{...previous.tests[0],expectedOutput:"2"},previous.tests[1]]};
   expect(makeLevelUpdate(previous,edited,"version-7").definition.tests[0]).toMatchObject({id:"visible",expectedOutput:"2"});
 });
 it("copies a starter definition with new ids while keeping hidden checks and intro media",()=>{
   const clone=cloneLevelDefinition(previous);
   expect(clone.id).not.toBe(previous.id);
   expect(clone.tests[1]).toMatchObject({name:"Secret",expectedOutput:"42",hidden:true});
   expect(clone.tests[1]?.id).not.toBe(previous.tests[1]?.id);
   expect(clone.introSteps[0]).toMatchObject({mediaUrl:"/a.png",speaker:"guide"});
 });
});
