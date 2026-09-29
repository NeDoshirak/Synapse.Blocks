import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CaseReport } from "../../components/reports/CaseReport";

describe("case report",()=>{
 it("shows each tied best attempt with best progress based only on completed levels",()=>{
   render(<CaseReport levelCount={3} report={{caseId:"c",participants:[{displayName:"Анна",bestProgress:2,attempts:[{attemptId:"a1",status:"Completed",startedAt:"2026-09-30T08:00:00Z",completedLevelCount:2},{attemptId:"a2",status:"Completed",startedAt:"2026-09-29T08:00:00Z",completedLevelCount:2},{attemptId:"a3",status:"InProgress",startedAt:"2026-09-28T08:00:00Z",completedLevelCount:1}]}]}}/>);
   expect(screen.getByText("Лучший прогресс: 2 из 3")).toBeInTheDocument();
   expect(screen.getAllByText(/пройдено 2 уровней/)).toHaveLength(2);
   expect(screen.getAllByText(/пройдено 1 уровней/)).toHaveLength(1);
 });
});
