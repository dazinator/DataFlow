# Implementation Workflow

This workflow guides implementing solutions from research handovers or direct requirements.

---

## Quick Start

**Before starting:**
1. Check if `/implementation/plan.md` exists (ongoing implementation)
2. If exists, read it and continue from current phase
3. If not, create it for multi-phase work (see below)
4. Check `.github/initiatives/active/` for relevant ongoing initiatives

**Core Process**
1. **Read [Document Hygiene Guide](/.github/DOCUMENT_HYGIENE.md)** if creating/updating documentation

Follow steps below

---

## Step 0: Handover Critical Review

**⚠️ CRITICAL**: If implementing from a research handover, critically evaluate it BEFORE starting work.

### Why This Matters

Handovers are potentially fallible. The research team may have:
- Underestimated implementation complexity
- Missed edge cases or dependencies
- Made assumptions that don't hold in practice
- Provided ambiguous or incomplete guidance

### Critical Review Checklist

Before implementing, ask yourself:

**Clarity & Completeness:**
- [ ] Are requirements clearly specified?
- [ ] Are success criteria measurable?
- [ ] Are edge cases documented?
- [ ] Is the target codebase explicitly stated (POC/Production/Both)?

**Feasibility:**
- [ ] Is the proposed approach practical given current codebase?
- [ ] Are there hidden dependencies or blockers?
- [ ] Is multi-phase recommendation clear and well-reasoned?
- [ ] Can this be done in phases for easier review?

**Potential Issues:**
- [ ] Are there performance implications not addressed?
- [ ] Are there breaking changes not documented?
- [ ] Are test requirements clear?
- [ ] Are migration paths specified (if breaking changes)?

**Missing Guidance:**
- [ ] Should baseline benchmarks be migrated or archived?
- [ ] Are there bulk migration patterns that need tooling?
- [ ] Is phased vs atomic implementation specified?

### What to Do When Issues Found

1. **Document the issue** in your notes
2. **Propose an alternative approach** if needed
3. **Update the implementation plan** to address the gap
4. **Add to workflow improvements** so future handovers improve

**Example**: "Handover says migrate all benchmarks but doesn't specify baseline benchmarks. Decision: Archive baseline as historical artifacts since they document the 'before' state. Create `/research/.../archived-benchmarks/` with README."

---

## Step 1: Identify Target Codebase

Check the handover/issue for:
- Explicit statement: "Target: POC" or "Target: Production" or "Target: Both"
- Context clues about which code is being modified

**⚠️ If target is unclear: STOP and ask the user to clarify.**

---

## Step 2: Read Handover Document

If implementing from research handover:

1. **Read handover completely**: `/research/[topic]/handover/github-issue-*.md`
   - Problem context and background
   - Implementation guidance and recommended approach
   - API/interface designs
   - Test scenarios and edge cases
   - Performance requirements
   - Design references and ADRs

2. **Read referenced documentation**:
   - Research findings: `/research/[topic]/README.md`
   - Design docs: `/research/[topic]/design/`
   - ADRs: `/poc/docs/adr/` or `/src/docs/adr/`
   - Prototype code: `/research/[topic]/handover/prototype/` (if available)

**If implementing from tech debt backlog** (`/research/backlog/`):

1. **Read backlog README**: `/research/backlog/README.md`
   - Understand backlog item format
   - Learn how to update item status when complete
   - Review archiving procedures

2. **Verify item is still valid**:
   - Code may have changed since item was created
   - Check if issue still exists
   - If no longer relevant, archive the item and stop

3. **Use backlog item as context**:
   - Backlog item provides problem description and context
   - Create proper implementation issue if needed for complex items
   - For simple items, backlog context may be sufficient

---

## Step 3: Check for Existing Implementation Plan

**⚠️ CRITICAL**: Before starting ANY implementation work, check for an existing plan.

```bash
# Check if there's a plan in progress
cat /home/runner/work/lib-dataflow/lib-dataflow/implementation/plan.md
```

### If Plan Exists

1. **Read the plan** to understand current phase and status
2. **Identify completed phases** (marked with ✅)
3. **Find current phase** (marked with 🚧 or next after completed)
4. **Continue from current phase** following instructions
5. **Update the plan** as work progresses

### If No Plan

**Decide if you need one based on handover guidance:**

Check the handover document for "Multi-Phase Implementation Assessment":
- **Single-Phase recommended**: No plan needed, track in issue
- **Multi-Phase recommended**: Create plan using suggested phases as starting point

**If handover doesn't include assessment**, evaluate yourself:
- Small, focused changes (< 10 files, < 500 lines) → Single-phase
- Large changes, natural break points, or high risk → Multi-phase

See [Implementation Handover Guidance](/implementation/HANDOVER_GUIDANCE.md) for assessment criteria.

---

## Step 4: Create Implementation Plan (If Multi-Phase)

For implementations spanning multiple PRs:

### Check for Ongoing Initiatives

**Before creating your plan**, check for active initiatives that may be relevant:

```bash
# List active initiatives
ls .github/initiatives/active/
```

**Read relevant initiatives** and consider incorporating them into your plan. See `.github/initiatives/README.md` for guidance on when and how to apply initiatives.

### Create /implementation/plan.md

```markdown
# Implementation Plan: [Name]

## Implementation Status

**Current Phase**: [Phase number/name]
**Issue**: #[number]
**Handover**: [path to handover doc if applicable]

## Objective

[Brief description of what's being implemented]

## Phases

### ✅ Phase 1: [Name] (COMPLETE)
**Status**: Complete
**Completed**: YYYY-MM-DD
**Deliverables**:
- [Delivered items]

**Files Modified/Created**:
- [List files]

**Verification**:
- [How phase was validated]

---

### 🚧 Phase 2: [Name] (IN PROGRESS)
**Status**: In Progress
**Objectives**:
- [What needs to be done]

**Files to Modify/Create**:
- [Target files]

**Success Criteria**:
- [ ] [Criterion 1]
- [ ] [Criterion 2]

**Instructions**:
1. [Step-by-step guidance]

---

### ⏳ Phase 3: [Name] (PENDING)
**Status**: Pending
[Same structure as Phase 2]

---

## Ongoing Initiatives (Optional)

**Note**: These are optional improvements to apply if time permits after completing primary phases.

### Initiative: [Name]  
**Reference**: `.github/initiatives/active/[name].md`  
**Status**: Not Started | In Progress | Complete

**Objectives**:
- [ ] [Task 1]
- [ ] [Task 2]

**Rationale**: [Why this initiative is relevant to this implementation]

---

## How to Continue

If resuming work in a future session:
1. Read this plan to see current phase
2. Read handover document: [path]
3. Review completed phase documentation: [references]
4. Continue from current phase above
5. Update this plan as work progresses

## References

- Handover: [path]
- Research: [path]
- Migration Guide: [path]
- Performance Validation: [path]
```

### Phased Implementation Pattern

**When to use:**
- Handover recommends multi-phase approach with clear rationale
- Natural break points exist (foundation → migration → cleanup)
- Changes can be staged without breaking changes
- Easier review with smaller PRs

See [Implementation Handover Guidance](/implementation/HANDOVER_GUIDANCE.md) for detailed assessment criteria.

**Typical Phases:**
- **Phase 1**: Foundation (add warnings, guidance, no breaking changes)
- **Phase 2**: Migration (systematic change, iterative)
- **Phase 3**: Cleanup (remove old code)

**Each phase:**
- Can be a separate PR
- Has clear success criteria
- Updates plan.md with status markers

---

## Step 5: Consider Ongoing Initiatives

**When**: After creating your implementation plan (for multi-phase work) or after understanding requirements (for single-phase work).

### Check Active Initiatives

```bash
# List active initiatives
ls .github/initiatives/active/

# Read relevant initiatives
cat .github/initiatives/active/[initiative-name].md
```

### Evaluate Relevance

For each active initiative, ask:
- ✅ **Is it relevant** to the code I'm working on?
- ✅ **Can it be applied** without significantly increasing complexity?
- ✅ **Is there time** after completing primary objectives?
- ✅ **Am I familiar** with the initiative pattern?

### When to Include

**Good candidates for inclusion:**
- Initiative directly relates to files you're modifying
- Pattern is well-documented with clear examples
- Low-risk addition (e.g., refactoring tests you're already touching)
- Adds value without complicating the PR review

**Skip when:**
- Primary implementation is complex or high-risk
- Initiative would significantly expand PR scope
- Unfamiliar with the pattern (would need learning time)
- Time constraints don't allow

### How to Include

**For multi-phase implementations:**
- Add "Ongoing Initiatives" section to `/implementation/plan.md`
- List relevant initiatives with objectives
- Mark as optional (complete after primary phases)

**For single-phase implementations:**
- Add initiative tasks to your mental checklist
- Apply opportunistically while working
- Document what was done in PR description

### Update Initiative Progress

After applying an initiative:
1. Update the initiative file's Progress Log
2. Note which files were affected
3. Update success metrics if applicable

**Example:**
```markdown
### 2025-11-08 - PR #170
- Applied test helper refactoring to ActorBlockTests.cs
- Removed custom IntCollectorActor (15 lines)
- Tests: 5/5 passing
```

---

## Step 6: Implement with Tests

Follow handover guidance (if applicable) and:

1. **Write tests first** (or alongside) to validate behavior
2. **Implement incrementally** - small, verifiable changes
3. **Test frequently** - after each meaningful change
4. **Handle edge cases** from handover documentation
5. **Document decisions** - especially when deviating from handover

### Documentation Requirements

**For POC Implementation:**
- Update `/poc/docs/POC_GLOSSARY.md` with new terminology
- Create guides in `/poc/docs/guides/` for patterns
- Document benchmarks if conducting performance analysis
- Create ADRs in `/poc/docs/adr/` for significant decisions

**For Production Implementation:**
- Update API documentation
- Update user guides if needed
- Create ADRs in `/src/docs/adr/` for significant decisions
- Update CHANGELOG for breaking changes

### Documentation Directory Decision Tree

When creating or updating documentation, use this decision tree to determine placement:

```
Where should I put this documentation?

├─ Is it research artifacts/analysis?
│  └─ YES → `/research/[topic]/`
│
├─ Is it an Architecture Decision Record?
│  ├─ For POC code → `/poc/docs/adr/`
│  └─ For Production code → `/src/docs/adr/`
│
├─ Is it POC-specific implementation guidance?
│  └─ YES → `/poc/docs/guides/`
│
├─ Is it production user-facing documentation?
│  └─ YES → `/docs/`
│
└─ Is it a module/directory README?
   └─ YES → In the directory itself (e.g., `/poc/DataFlow.POC.Tests/TestHelpers/README.md`)
```

**Examples:**
- Test helper usage guide → `/poc/docs/guides/testing-guide.md`
- ADR for ActorBlock design → `/poc/docs/adr/2025-11-07-actor-block-rotation.md`
- Research findings → `/research/testing-approaches/README.md`
- Production API docs → `/docs/api/`

### Navigation File Updates

**IMPORTANT**: After adding new documentation, update navigation/index files so users can discover it.

**Checkpoint before `report_progress`:**
- [ ] Created new guide in `/poc/docs/guides/`? → Update `/poc/docs/INDEX.md`
- [ ] Created new guide in `/docs/`? → Update main project README
- [ ] Added new module/directory? → Create README in that directory
- [ ] Added new test helpers? → Update test helpers README

**Common navigation files:**
- `/poc/docs/INDEX.md` - POC documentation index
- `/README.md` - Main project README
- `/poc/README.md` - POC overview
- `/docs/README.md` - Documentation index (if exists)

---

---

## Step 7: Validate and Document

### Testing

- Run all tests (create new tests per handover guidance)
- Handle edge cases from handover
- **Do not** fix unrelated failures

### Benchmark Validation (if performance requirements specified)

Apply these criteria based on operation duration:

**Microbenchmarks (<100ms operations):**
- Accept ±5-10% variance as normal measurement noise
- Focus on I/O-bound representative scenarios
- Extreme speeds (>500K items/sec) show high variance
- Document when proceeding despite variance (safety/features > micro-optimization)

**Longer operations (>1sec):**
- ±1% variance is achievable
- More stable measurements allow precise validation

**When to Proceed Despite Variance:**
- Safety improvements justify small performance costs
- Real-world I/O scenarios validate successfully
- Variance is random (both faster/slower), not systematic
- Feature benefits outweigh potential micro-optimization

### Documentation Updates

- Update glossary if new concepts introduced (POC only)
- Update relevant documentation
- Create ADR if significant decisions made

### Effort Estimation

For large implementations (migrations, refactorings):

**Test Migration**: Estimate ~30 minutes per test file
- Simple test files: 15-20 minutes
- Complex test files with dependencies: 45-60 minutes
- Factor in consolidation time if removing redundant tests

**Phased Decision Point**: At midpoint of large implementation:
- Assess remaining work (hours)
- Create status document with "continue now" vs "next session" decision
- If >4 hours remaining, consider breaking into separate PR
- Document current phase and next steps in plan.md

**Breaking Large Work**: Signs you should create phased implementation:
- Estimated effort >8 hours total
- Natural break points exist (foundation → migration → cleanup)
- Changes can be staged without breaking changes
- Easier code review with smaller PRs

---

---

## Step 8: Complete Implementation

### Archive Plan (If Multi-Phase)

Once ALL phases complete:

```bash
# Archive the completed plan
mv /implementation/plan.md /implementation/archive/[YYYY-MM-DD]-[short-name].md
```

Format: `/implementation/archive/2025-11-06-plain-blocks-consolidation.md`

### Self-Improvement Evaluation

**⚠️ REQUIRED** before marking PR ready for review.

See `.github/copilot-instructions.md` for detailed guidance.

Quick checklist:
1. Evaluate workflow effectiveness for this task
2. Document what worked well
3. Document what didn't work well or could improve
4. Propose specific, actionable improvements
5. Add to `.github/workflow-improvements.md`

---

## Success Criteria

Implementation complete when:
- [ ] All objectives from handover/requirements met
- [ ] Tests passing (including new tests)
- [ ] Performance requirements validated (if applicable)
- [ ] Edge cases handled (from handover)
- [ ] Documentation updated
- [ ] Plan archived (if multi-phase)
- [ ] Self-improvement evaluation completed
- [ ] Code review ready

---

## Related Documentation

- **Entry point**: `.github/copilot-instructions.md` - Start here
- **Research workflow**: `.github/workflows/RESEARCH_WORKFLOW.md`
- **Implementation folder**: `/implementation/README.md`
- **Workflow improvements**: `.github/workflow-improvements.md`
