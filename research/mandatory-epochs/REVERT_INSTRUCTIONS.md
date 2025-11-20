# Code Revert Instructions

**⚠️ IMPORTANT**: Per research duty procedure, exploratory code should be reverted AFTER reviewer approval.

---

## Files to Revert (After Reviewer Approval)

These files were added to the POC for validation and benchmarking purposes. They should be reverted before closing the research work item, but AFTER the reviewer approves the research findings.

### Files in `/poc/` to Revert

1. `/poc/DataFlow.POC/Core/SingleEpochAdapter.cs`
   - Exploratory prototype code
   - Contains `SingleEpochExtensions` and `PlainSourceAdapter<T, TActor>`
   - **Action**: Revert (remove file)

2. `/poc/DataFlow.POC.Benchmarks/SingleEpochOverheadBenchmark.cs`
   - Benchmark code for validation
   - **Action**: Revert (remove file)

### Files to KEEP

All files in `/research/mandatory-epochs/` should be kept:
- `README.md` - Research findings
- `research-plan.md` - Research plan
- `notes/` - Analysis documents
- `design/` - Architecture design
- `benchmarks/` - Benchmark results
- `handover/` - Implementation specifications and prototype
- `benchmark-app/` - Standalone benchmark application

---

## Revert Procedure

### After Reviewer Approval:

```bash
# Revert POC files
git rm poc/DataFlow.POC/Core/SingleEpochAdapter.cs
git rm poc/DataFlow.POC.Benchmarks/SingleEpochOverheadBenchmark.cs

# Commit revert
git commit -m "Revert exploratory prototype code per research duty procedure

Co-authored-by: dazinator <3176632+dazinator@users.noreply.github.com>"

# Push changes
git push origin copilot/research-make-epochs-mandatory
```

---

## Why Revert?

Per `.team/duties/RESEARCH_DUTY.md`:

> Research produces **documentation and specifications**, not merged code. Exploratory code is for validation and learning, then gets reverted after reviewer approval.

**Purpose**:
- Research code is for validation, not production
- Prevents half-baked code from entering codebase
- Implementation team builds production version from specifications
- Clean separation between research and implementation

---

## What Happens Next

1. ✅ Research complete with findings documented
2. ⏳ Awaiting reviewer approval of research findings
3. ⏳ After approval: Revert exploratory code
4. ⏳ Submit self-improvement feedback
5. ✅ Implementation work item #506 ready for implementation duty
6. ✅ Prototype code saved in `/research/mandatory-epochs/handover/prototype/` for reference

---

## Implementation Duty Next Steps

Implementation duty should:
1. Review complete research documentation
2. Copy prototype code from `/research/mandatory-epochs/handover/prototype/` 
3. Integrate as production code (with proper code review)
4. Follow implementation checklist in #506
