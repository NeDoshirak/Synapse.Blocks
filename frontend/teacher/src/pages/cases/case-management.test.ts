import { describe, expect, it } from "vitest";
import { canPublishCase, moveLevel, addLevel } from "../../resources/caseOrder";

describe("ordinary case ordering",()=>{
 it("adds each pinned version once and preserves the teacher's order",()=>{
   const first=addLevel([],"v1");
   const withSecond=addLevel(first,"v2");
   expect(addLevel(withSecond,"v1")).toEqual(["v1","v2"]);
   expect(moveLevel(withSecond,1,-1)).toEqual(["v2","v1"]);
 });
 it("does not allow publishing an empty case",()=>{
   expect(canPublishCase("Lesson",[])).toBe(false);
   expect(canPublishCase("Lesson",["v1"])).toBe(true);
   expect(canPublishCase(" ",["v1"])).toBe(false);
 });
});
