# Henco's Markbook Analysis — Design_Markbook_25.xlsx

---
date: 2026-07-06
source: Design_Markbook_25.xlsx (Randfontein High School, 2025)
subject: Design (Grades 10, 11, 12)
---

## What this file is
Henco's **live 2025 Design markbook** — real working data, not a template. Three sheets (Grade 10: 39 learners, Grade 11: 25, Grade 12: 24). Term 1 and Term 2 data.

This is the product specification for the Marks Capture feature.

## Critical findings for the data model

### 1. Tasks have multiple criteria (rubric-based)
Each task is scored at the **criteria level**, not just a total. A Design theory task has 5 criteria, each /10 (total 50):
1. Expression of intention and rationale/concept
2. Evidence of research, experimentation and reflection
3. Evidence of detailed planning, drawing skills and presentation
4. Evidence of final drawing/collage/maquette related to final concept
5. Research: Design in a business context

Practical tasks also have 5 criteria /10:
1. Creativity/Originality/Interpretation
2. Evidence of design involvement
3. Technique/Craftsmanship/Method
4. Time management
5. Professional presentation and functionality

**Implication:** The system must support criteria-level entry. A "mark out of 50" field alone won't work.

### 2. Absent learners are recorded as -1 (not zero)
Zero = present, scored nothing. -1 = absent. This distinction affects SBA calculations.
**Implication:** Add `IsAbsent` boolean to Grade entity. Never treat absent as zero.

### 3. Task types in Design
- TASK 1: Test (Theory)
- TASK 2: Practical (Process)
- TASK PAT (Product 1 & 2) — **year-long CAPS project**
- TASK Practical 1 & 2 (Process)
- TASK 6: Theory

**PAT (Practical Assessment Task)** is unique — cumulative, year-long, special CAPS weighting in Grade 12.

### 4. The LEVEL column
Every task total has a LEVEL column next to it — the CAPS achievement level (1-7). Auto-derived from percentage. Must be automatic in the system.

### 5. Level distribution at the bottom
Each class has level distribution rows (how many learners at each level 1-7). This is the moderation tool — HODs use it to compare classes.

### 6. Grade-level differences
Grade 10 Design ≠ Grade 12 Design. Different task structures, different PAT configurations, different criteria weighting. The system must support per-grade assessment programmes.

### 7. Year view, not term view
The markbook shows the whole year on one sheet. The system needs a year view for SBA running total alongside the term view.

### 8. Version tracking
Header: `EMIS | Date | 700270488 | v25.0.0 | 2025/03/12` — the school EMIS number and markbook version. The system should handle academic year clearly.

## The percentage → CAPS Level conversion
```
0-29%  → Code 1 (Not Achieved)
30-39% → Code 2 (Elementary)
40-49% → Code 3 (Moderate)
50-59% → Code 4 (Adequate)
60-69% → Code 5 (Substantial)
70-79% → Code 6 (Meritorious)
80-100% → Code 7 (Outstanding)
```

## Data model implications
```
AssessmentTask
  + HasRubric (bool)
  + PAT added to TaskType enum

AssessmentCriteria (new)
  - CriteriaName, MaxMark, DisplayOrder

Grade (extend)
  + IsAbsent (bool)
  + CriteriaScores[]

CriteriaScore (new)
  - CriteriaId, Score
```

## The bottom line
"Every spreadsheet a teacher builds identifies a problem that existing software has failed to solve." — Henco

This markbook is that spreadsheet. SchoolPort needs to make it unnecessary.

## Related
- [[Henco Interview — Design Teacher]]
- [[Sprint 1.5.2.5 — Marks Capture]]
