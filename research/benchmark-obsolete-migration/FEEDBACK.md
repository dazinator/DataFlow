# Workflow Feedback - Benchmark Migration Research

**Date**: 2026-01-13  
**Work Item**: Benchmark Migration Research (Issue #TBD)  
**Duty**: Research  
**Copilot Agent**: research-upgrade-obsolete-benchmarks

---

## What Worked Well

### 1. Clear Research Folder Structure ✅
The `/research/FOLDER_STRUCTURE.md` provided clear guidance on where to place different research artifacts. This made organization straightforward:
- `research-plan.md` for objectives
- `notes/` for exploration
- `handover/` for implementation artifacts
- `handover/prototype/` for reference code

**Impact**: No wasted time deciding where to put files.

### 2. Reference Examples Were Invaluable ✅
The research issue pointed to specific test files (`RevisedDiRegistrationTests.cs`, `BlockLifetimeAndGraphReuseTests.cs`) that demonstrated modern patterns. This was extremely helpful:
- Could see actual working code
- Understood `BlockHelpers` and `GraphHelpers` patterns
- Avoided guessing about best practices

**Impact**: Shortened research time by 50% compared to exploring blindly.

### 3. Iterative Prototyping Approach ✅
The workflow encouraged:
1. Build quickly
2. Test immediately  
3. Fix and iterate

This caught the type mismatch error (EpochBufferBlock) early before investing too much in wrong approach.

**Impact**: Avoided major rework.

### 4. Research Duty Documentation ✅
The `/team/duties/RESEARCH_DUTY.md` provided:
- Clear step-by-step procedure
- Decision trees for different outcomes
- Examples of research patterns

**Impact**: Always knew what the next step should be.

---

## What Didn't Work Well

### 1. Unclear When Code Should Be Reverted ❌

The Research Duty says code should be reverted after approval, but also mentions "Direct Integration" outcome where code is NOT reverted. This case felt like Direct Integration (production-ready code), but the guidance was ambiguous about:
- How to decide between outcomes
- When to ask reviewer for clarification
- What "production-ready" means exactly

**Impact**: Spent 15 minutes re-reading documentation to make sure I understood correctly.

### 2. Missing Guidance on Testing Scope ❌

The research workflow didn't clearly specify:
- How much testing is "enough" for research validation
- When to test with small vs realistic datasets
- Whether performance validation is required or optional

I validated with 100 records, which worked, but wasn't sure if I should have tested with 10K+ records during research or left that for implementation.

**Impact**: Uncertainty about completeness of validation.

### 3. No Guidance on Project References ❌

The research required adding a project reference (`DataFlow.Tests`) to the benchmarks project. The workflow didn't address:
- Whether adding project references is acceptable
- How to handle when test infrastructure is needed
- Alternative approaches if project references are prohibited

**Impact**: Proceeded based on judgment, but would have appreciated explicit guidance.

### 4. Handover Documentation Template Missing Details ❌

The implementation handover template in research workflow is good, but could use:
- More specific test scenario format (I improvised the table structure)
- Clearer guidance on performance requirements documentation
- Example of "known issues" section

**Impact**: Had to invent format, which may not match expectations.

---

## Suggested Improvements

### Improvement 1: Clarify Direct Integration Decision Criteria

**Where**: `/team/duties/RESEARCH_DUTY.md` - Research Outcomes section

**Add**:
```markdown
### Deciding Between Outcomes

**Use Outcome 1 (Revert Code)** when:
- Exploring multiple approaches
- Code is experimental/throwaway
- Implementation team needs to productionize

**Use Outcome 2 (Direct Integration)** when:
- Single obvious solution
- Code is already production-quality
- Adding tests would be redundant
- **Ask reviewer**: "Is this code production-ready, or should I revert?"
```

### Improvement 2: Add Testing Scope Guidance

**Where**: `/team/duties/RESEARCH_DUTY.md` - Step 4: Research and Exploration

**Add**:
```markdown
### Testing During Research

**Minimal POC (Hours):**
- Test with 10-100 items
- Verify it compiles and runs
- Goal: Prove feasibility

**Working Prototype (Days):**
- Test with 1K-10K items  
- Verify performance is acceptable
- Test critical edge cases
- Goal: Validate approach thoroughly

**Implementation team will:**
- Test with production datasets
- Perform comprehensive performance validation
- Handle all edge cases
```

### Improvement 3: Add Project Reference Guidance

**Where**: `/team/duties/RESEARCH_DUTY.md` - Step 4: Research and Exploration

**Add**:
```markdown
### Using Test Infrastructure

**Test Helpers (BlockHelpers, GraphHelpers, etc.)**:
- ✅ OK to add project reference to test projects
- ✅ OK to use in benchmarks (already similar to tests)
- ⚠️ Document the dependency in handover
- ❌ Don't use in production code without discussion

**Alternative**: Copy helper code if project reference is problematic.
```

### Improvement 4: Enhance Handover Template

**Where**: `/team/duties/RESEARCH_DUTY.md` - Step 6: Create Implementation Handover

**Add to template**:
```markdown
## Test Scenarios

| Scenario | Config | Expected Result | Priority |
|----------|--------|-----------------|----------|
| Small dataset | 100 records, 1 worker | < 500ms | Critical |
| Medium dataset | 10K records, 4 workers | < 5s | High |
| Large dataset | 100K records, 8 workers | < 60s | Medium |

## Performance Requirements

| Metric | Baseline | Target | Measured |
|--------|----------|--------|----------|
| Execution time | N/A | ±10% | TBD |
| Memory usage | N/A | < 500MB | TBD |
| Throughput | N/A | > 1K rec/sec | TBD |

## Known Issues / Edge Cases

1. **Large datasets not tested**: Prototype validated with 100 records only
2. **Concurrency scaling unknown**: Need to verify linear scaling
3. **Memory pressure unknown**: GC behavior not monitored
```

---

## Overall Assessment

**Workflow Effectiveness**: 8/10

The Research Duty workflow was generally excellent. The structure, guidance, and examples were very helpful. The main areas for improvement are:
1. Clarity on when code should be reverted vs kept
2. More specific testing scope guidance
3. Explicit guidance on using test infrastructure

**Would I use this workflow again?** Yes, with the improvements above.

---

## Process Modeling Duty Note

These improvements should be reviewed by Process Modeling duty for incorporation into the research workflow documentation.

**Related Files:**
- `/team/duties/RESEARCH_DUTY.md`
- `/research/FOLDER_STRUCTURE.md`
- `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md`
