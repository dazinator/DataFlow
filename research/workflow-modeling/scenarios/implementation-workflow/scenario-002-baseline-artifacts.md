# Scenario: Research Team Creates Handover with Benchmark Migration

## Context

A research team is creating an implementation handover for a performance improvement. They created baseline benchmarks to measure the "before" state, then created improved benchmarks showing the "after" state. They need to specify what the implementation team should do with the baseline benchmarks.

## Starting Point

- Research team has completed performance research
- Baseline benchmarks exist showing current performance
- Improved benchmarks exist showing new approach performance
- Research team is creating handover using `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md`
- They're at the section about test artifacts and migration

## Steps to Follow

Research team would:

1. Open `/research/IMPLEMENTATION_ISSUE_TEMPLATE.md`
2. Look for guidance on what to do with baseline/historical artifacts
3. Read section about benchmark migration or artifact handling
4. Decide: Should baselines be migrated to production or archived?
5. Document decision in handover issue
6. Specify archive location if archiving (e.g., `/research/.../archived-benchmarks/`)

Implementation team would:

1. Read the handover issue
2. See explicit guidance: "Archive baseline benchmarks to `/research/topic/archived-benchmarks/`"
3. Understand: baselines are historical artifacts, not ongoing tests
4. Migrate improved benchmarks to production test suite
5. Archive baselines with README explaining their purpose

## Expected Outcome

**With the improvement:**
- Template explicitly prompts for baseline/historical artifact handling
- Research team provides clear guidance in handover
- Implementation team knows exactly what to do with each artifact
- Distinction clear between "historical comparison artifacts" vs "ongoing tests"
- Archive location specified if applicable

**Without the improvement:**
- Template silent on baseline artifacts
- Research team might not mention them in handover
- Implementation team uncertain: migrate or archive?
- Might waste time migrating artifacts meant to be historical
- Or might archive artifacts meant to be production tests
- Inconsistent handling across different implementations

## Success Criteria

- [ ] Template includes guidance section for baseline/historical artifacts
- [ ] Guidance prompts to specify migrate vs archive
- [ ] Example archive location provided (e.g., `/research/.../archived-benchmarks/`)
- [ ] Distinction explained: historical comparison vs ongoing performance tests
- [ ] Clear enough that both research and implementation teams understand

## Test Result

**Status**: [x] PASS  [ ] FAIL

**Notes**: 
**BASELINE SIMULATION** (2025-11-08):
- No explicit guidance in IMPLEMENTATION_ISSUE_TEMPLATE.md about baseline artifacts
- Template mentions prototype code and benchmarks but doesn't distinguish types
- No guidance on when to archive vs migrate artifacts
- No example archive location pattern provided
- Research and implementation teams would be uncertain about handling
- **Needed**: Add explicit section for baseline/historical artifact handling guidance

**IMPROVED SIMULATION** (2025-11-08):
- ✅ Added "Baseline/Historical Artifacts" section to template after line 213
- Clear decision criteria: historical artifacts (archive) vs ongoing tests (migrate)
- Example archive location: `/research/[topic]/archived-benchmarks/` with README
- Explicit distinction between comparison artifacts and performance tests
- Both research and implementation teams now have clear guidance
- **Status**: PASS - All success criteria met
