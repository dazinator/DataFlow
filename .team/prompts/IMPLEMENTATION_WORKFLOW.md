# Implementation Workflow

This workflow guides implementing solutions from research handovers or direct requirements.

---

## Quick Start

**Before starting:**
1. Check if `/implementation/plan.md` exists (ongoing implementation)
2. If exists, read it and continue from current phase
3. If not, create it for multi-phase work (see below)
4. Check `.github/initiatives/active/` for relevant ongoing initiatives

**⚠️ Comment Prefix Convention:**
- Prefix ALL comments with `[Copilot-Workflow: Implementation]` to confirm you're following this workflow
- Example: `[Copilot-Workflow: Implementation] I've completed the first phase and tests are passing...`

**Core Process**
1. **Read [Document Hygiene Guide](/.github/DOCUMENT_HYGIENE.md)** if creating/updating documentation

Follow steps below

---

## Workflow Queue

**Query issues designated to this workflow:**

Using GitHub MCP tools (primary method):
```python
list_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    labels=["workflow:implementation"],
    state="OPEN"
)
```

**Or use CLI for manual queries:**
```bash
gh issue list \
  --label "workflow:implementation" \
  --state open \
  --json number,title,url
```

**Or use the query script:**
```bash
./.github/scripts/workflow/query-workflow-queue.sh implementation
```

**Entry Points:**
- From Triage workflow (ready to implement)
- From Research workflow (approach validated)
- From Product Prioritization (backlog item prioritized)
- From Tech Debt workflow (debt analysis complete)

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

---

## Label Cleanup on Entry

**⚠️ IMPORTANT**: Before starting implementation work, check for and clean up conflicting workflow labels.

### Workflow Label Validation

When you start work on an issue in this workflow:

1. **Check the issue's labels** for any workflow labels
2. **Identify conflicts**: If the issue has MULTIPLE workflow labels (e.g., both `workflow:implementation` AND `workflow:research`)
3. **Determine correct label**: 
   - If you were assigned to this issue via the implementation queue, `workflow:implementation` is correct
   - If the issue has another workflow label in addition to `workflow:implementation`, that's label pollution
4. **Remove conflicting labels**: Remove any workflow label that is NOT `workflow:implementation`
5. **Add cleanup comment** noting what was corrected

### Label Cleanup Example

**For Copilot Agents** (use MCP tools):

```python
# Example: Issue has both workflow:implementation and workflow:research labels
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    labels=["workflow:implementation"]  # Only keep the correct label
)

add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE_NUMBER,
    body="[Copilot-Workflow: Implementation] 🏷️ Label cleanup: Removed conflicting `workflow:research` label. This issue is correctly in the implementation workflow."
)
```

**Why this matters**: Issues should have exactly ONE workflow label at a time. Multiple labels create confusion about which workflow owns the issue.

**When to skip**: If the issue only has `workflow:implementation` label (no conflicts), proceed directly to implementation work.

---

## Multi-Phase Issue Check

**⚠️ ALWAYS**: Check if this issue is part of a multi-phase plan before starting work.

### Quick Check

```python
# Get current issue details
issue = issue_read(
    method="get",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=CURRENT_ISSUE_NUMBER
)

# Check if parent exists
if issue.parent:
    # This is a sub-issue - read parent context
    parent = issue_read(
        method="get",
        owner="uniun-technology",
        repo="lib-dataflow",
        issue_number=issue.parent.number
    )
    # Review parent to understand overall plan and which phase this represents
else:
    # Standalone issue - proceed with normal workflow
```

### If This is a Sub-Issue

**DO:**
1. ✅ Read parent issue to understand overall plan
2. ✅ Note which phase this represents
3. ✅ Review completed phases for context
4. ✅ Update parent description as you make progress
5. ✅ Check if this is the last sub-issue before creating PR

**Parent Update Pattern:**

When reporting progress:
```python
# Update parent issue description to reflect current status
# Example: Change "Phase 2 - NOT STARTED" to "Phase 2 - Core complete, tests passing"
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=parent_number,
    body=updated_description
)
```

**Closing Parent (Last Sub-Issue Only):**

If this is the last open sub-issue, include parent in PR description:
```markdown
Fixes #CURRENT_ISSUE
Fixes #PARENT_ISSUE
```

This ensures both issues close when PR merges.

**See:** [Multi-Phase Issue Procedures](/.team/MULTI_PHASE_ISSUES.md) for complete guidance including examples and troubleshooting.

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

**⚠️ Workflow File Check (CRITICAL):**
- [ ] Does this issue include tasks to update workflow files?
- [ ] Workflow files include: `.team/prompts/*_WORKFLOW.md`, `.github/copilot-instructions.md`, `.github/ISSUE_TEMPLATE/*.md`, `.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md`
- [ ] **Note**: For the complete and authoritative list of process modeling–owned workflow files, see the ownership section in `.github/copilot-instructions.md`
- [ ] **If YES**: These tasks MUST be separated into a Process Modeling issue
- [ ] **Action**: Create separate Process Modeling issue, remove workflow file tasks from implementation scope

**Tech Debt Verification (if implementing tech debt item):**
- [ ] Does backlog item include verification check?
- [ ] Execute verification check to confirm tech debt still exists
- [ ] If tech debt already fixed: Update backlog item status, notify team, STOP implementation
- [ ] If tech debt exists: Continue with implementation

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
- [ ] For NuGet security fixes, does handover specify version selection? (See [Dependency Update Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md))
- [ ] If sample code requiring external services (databases, OTLP, etc.), are they documented?

### What to Do When Issues Found

1. **Document the issue** in your notes
2. **Propose an alternative approach** if needed
3. **Update the implementation plan** to address the gap
4. **Add to workflow improvements** so future handovers improve

**Example**: "Handover says migrate all benchmarks but doesn't specify baseline benchmarks. Decision: Archive baseline as historical artifacts since they document the 'before' state. Create `/research/.../archived-benchmarks/` with README."

### Workflow File Updates - Separate to Process Modeling

**If the implementation issue includes workflow file updates:**

**❌ DO NOT update workflow files yourself** - even if the issue description lists them as tasks

**✅ Create a separate Process Modeling issue** for workflow changes:

```python
# Create process modeling issue for workflow updates
issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="[Process Modeling] Update Workflows for [Feature Name]",
    labels=["workflow:process-modeling"],
    body="""## Workflow File Updates Needed

**Source**: Implementation issue #XXX

**Reason**: Implemented [feature/fix], workflows need updates

**Files to Update**:
- `.team/prompts/IMPLEMENTATION_WORKFLOW.md` - [what needs updating]
- `.team/prompts/RESEARCH_WORKFLOW.md` - [what needs updating]
- `.github/copilot-instructions.md` - [what needs updating]

**Proposed Changes**:
[Describe what needs to be added/updated in each file]

**Context**:
[Explain what was implemented and why workflows need these updates]

**References**:
- Implementation PR: #XXX
- Design docs: [paths]
"""
)
```

**Then complete your implementation WITHOUT the workflow file changes**

**Why**: Workflow files require tabletop simulation testing, cross-workflow validation, and systematic refinement. Process Modeling workflow owns these files exclusively.

See `.github/copilot-instructions.md` section "Workflow File Ownership" for complete details.

### Tech Debt Verification Example

**If implementing a tech debt backlog item**, the item should include a verification check:

```markdown
## Verification Check

Before starting implementation, verify this tech debt still exists:

```bash
# Run this command - should show warnings
dotnet build 2>&1 | grep "CS0436" | wc -l
# Expected: ~15 warnings
# If result is 0, tech debt already fixed - stop and update backlog item
```
```

**Execute the verification check**:
```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
dotnet build 2>&1 | grep "CS0436" | wc -l
```

**If tech debt still exists** (result matches expectation):
- ✅ Continue with implementation

**If tech debt already fixed** (result is 0 or significantly different):
- ❌ STOP implementation
- Update backlog item:
  ```markdown
  **Status**: Resolved (Already Fixed)
  **Updated**: YYYY-MM-DD
  ```
- Archive backlog item to `/product/resolved/`
- Comment on issue: "Tech debt verification check shows this issue was already addressed. Backlog item archived."
- Wait for reviewer to assign new work

---

## Step 1: Identify Target Codebase

Check the handover/issue for:
- Explicit statement: "Target: POC" or "Target: Production" or "Target: Both"
- Context clues about which code is being modified

**⚠️ If target is unclear: STOP and ask the user to clarify.**

---

## Step 2: Read Product Backlog Item

All implementation work should reference a product backlog item. 

**See `/product/README.md` for complete product backlog system documentation.**

### If Backlog Issue Specified

Implementation issue will reference a backlog GitHub issue:
- **Backlog Issue**: #123

**Steps:**
1. **Read the backlog GitHub issue** completely
2. **Review handover assets** referenced in issue (in `/research/` or `/implementation/` folders)
3. **Update backlog issue** with comment: "Started implementation"
4. Proceed with implementation

### If "Next from Prioritization" Specified

Issue will say:
- **Backlog Item**: "Next from prioritization list"

**Steps:**
1. **Query prioritized backlog issues**:
   ```python
   # Get highest priority backlog items
   list_issues(
       owner="uniun-technology",
       repo="lib-dataflow",
       labels=["workflow:product-backlog", "priority-high"],
       state="OPEN"
   )
   ```
2. **Identify highest priority item** from results
3. **PAUSE and comment on issue**:
   ```markdown
   Selected highest priority item from backlog:
   
   **Backlog Issue**: #[N]
   **Title**: [title]
   **Priority**: High
   
   Awaiting confirmation to proceed with this item.
   ```
4. **WAIT for reviewer confirmation** - Do not start implementation
5. **After confirmation**, follow "Backlog Issue Specified" steps above

### Reading the Backlog Issue

**1. Read the backlog GitHub issue completely**:

Pay attention to:
- **Summary and Context** - What needs to be implemented and why
- **Implementation Guidance** - Recommended approach and constraints
- **Success Criteria** - What defines completion
- **References** - Links to research folders, design docs, ADRs

**2. Review handover assets** (referenced in issue body):

Check paths mentioned in issue (typically `/research/[topic]/handover/` or `/implementation/[topic]/`):
- **prototype/** - Reference implementations and code examples
  - Read prototype README for guidance
  - Understand what each file demonstrates
  - Note performance metrics achieved
- **design/** - Design documents specific to this work
- **benchmarks/** - Performance data and requirements

**3. Follow references** in backlog issue:
- Research findings (if from research team)
- Design documents
- ADRs (Architecture Decision Records)
- Related issues and PRs

**4. Update backlog issue** with comment:

```python
# Add comment to backlog issue
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=[backlog-issue-number],
    body="🚧 Implementation started\n\nImplementing in issue #[implementation-issue-number]"
)
```

### Verify Backlog Item Accuracy

**⚠️ IMPORTANT**: Before starting implementation, validate key estimates from the backlog item.

Backlog items may contain estimates for scope (file counts, line counts, complexity). These estimates can be outdated or inaccurate. Validate them upfront to set correct expectations and avoid surprises mid-implementation.

**Verification Steps**:

1. **Identify estimates** in backlog item:
   - File counts ("~50-60 files need updating")
   - Line counts ("~200 lines of code")  
   - Component counts ("5 similar classes")

2. **Validate with grep/find**:
   ```bash
   # Count files matching a pattern
   find . -name "*.cs" -exec grep -l "pattern to find" {} + | wc -l
   
   # Find specific boilerplate
   find . -name "*.cs" -exec grep -l "public ITestOutputHelper Output" {} +
   
   # Count lines of code in target files
   find src -name "*.cs" -exec wc -l {} + | tail -1
   ```

3. **Document discrepancies** in first progress report if actual scope differs significantly:
   ```markdown
   **Scope Validation**: Backlog estimated ~50-60 files, actual count is 6 files matching the pattern.
   ```

4. **Update backlog issue** if estimates are way off (10x difference):
   ```python
   add_issue_comment(
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=[backlog-issue-number],
       body="📊 Scope validation: Backlog estimated X files, found Y files. Proceeding with corrected scope."
   )
   ```

5. **Handle scope expansion** when you find more issues than documented:
   
   **If you discover additional instances** (e.g., backlog says "3 files" but build shows 6 warnings):
   
   - ✅ **Fix all instances** - Proceed with comprehensive fix for completeness
   - 📝 **Document expansion** in first progress report:
     ```markdown
     **Scope Expansion**: Backlog item documented 3 instances, but build validation found 6 CS8425 warnings. Fixing all 6 for completeness.
     ```
   - 💬 **Update backlog issue** noting expanded scope:
     ```python
     add_issue_comment(
         owner="uniun-technology",
         repo="lib-dataflow",
         issue_number=[backlog-issue-number],
         body="📊 Scope expanded: Found 6 instances (3 more than documented). Fixing all for completeness."
     )
     ```
   
   **Why fix all instances?**
   - Prevents partial fixes that leave inconsistency
   - Comprehensive solution is easier to review than selective application
   - Avoids "why wasn't this one fixed?" questions in review
   - Documents the scope accurately for future reference

**Why This Matters**: Validating estimates upfront helps:
- Set realistic expectations for reviewers
- Identify if backlog item scope changed (partial implementation already done)
- Plan work allocation appropriately
- Catch potential misunderstandings of the requirement early

### Verify Work Still Needed

**⚠️ IMPORTANT**: After validating accuracy, verify the work is still needed. Sometimes backlog items describe work that has already been completed by another PR or was addressed indirectly.

**Quick Validation Steps**:

1. **Check the specific change** described in backlog:
   ```bash
   # Example: For "modernize namespaces" check if namespaces are already modern
   grep -r "namespace.*{" src/  # Should return 0 if file-scoped namespaces used
   
   # Example: For "add EnumeratorCancellation attributes" check if they exist
   grep -l "\[EnumeratorCancellation\]" src/**/*.cs | wc -l
   ```

2. **Run build/tests** to confirm current state:
   ```bash
   dotnet build
   dotnet test
   ```

3. **Evaluate results**:
   - **Work already complete**: Validation shows change already implemented
   - **Work still needed**: Validation confirms work needs to be done

**If Work Already Complete**:

1. ✅ **This is a VALID and POSITIVE outcome** - not a failure!

2. **Close the backlog issue** with state_reason="completed":
   ```python
   issue_write(
       method="update",
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=[backlog-issue-number],
       state="closed",
       state_reason="completed"
   )
   
   add_issue_comment(
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=[backlog-issue-number],
       body="""✅ Work validation complete

**Status**: Already implemented

**Validation**: [Describe validation performed]
- grep check showed 0 instances of old pattern
- Build successful
- Tests passing

**Conclusion**: This work was completed in a previous PR or through indirect changes. No implementation needed."""
   )
   ```

3. **Report in implementation issue**:
   ```python
   add_issue_comment(
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=[current-implementation-issue],
       body="🔍 Investigation complete\n\nValidation revealed backlog item #[N] work was already complete. See backlog issue for details. Ready for next assignment."
   )
   ```

4. **Close implementation issue** (work complete, just differently than expected):
   ```python
   issue_write(
       method="update",
       owner="uniun-technology",
       repo="lib-dataflow",
       issue_number=[current-implementation-issue],
       state="closed",
       state_reason="completed"
   )
   ```

5. **STOP and wait** for reviewer to assign new work

**If Work Still Needed**:

✅ Continue with implementation following remaining steps

**Benefits of This Check**:
- Saves 15-30 minutes vs full implementation attempt
- Prevents duplicate work
- Keeps backlog accurate
- Clear communication to reviewers

### After Implementation Complete

**1. Update backlog issue** to mark as completed:

```python
# Close backlog issue
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=[backlog-issue-number],
    state="closed"
)

# Add completion comment
add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=[backlog-issue-number],
    body="✅ Implementation complete\n\nCompleted in PR #[N]"
)
```
## Status History
- **YYYY-MM-DD**: Created
- **YYYY-MM-DD**: Started implementation (PR: #[N])
- **YYYY-MM-DD**: Completed (PR: #[N])
```

**2. Archive backlog item** to resolved folder:

```bash
# Create monthly archive folder if needed
mkdir -p product/resolved/$(date +%Y-%m)

# Move backlog item file
mv product/backlog/[item-id].md product/resolved/$(date +%Y-%m)/

# Move handover folder if exists
if [ -d "product/backlog/[item-id]" ]; then
  mv product/backlog/[item-id]/ product/resolved/$(date +%Y-%m)/
fi
```

**3. Commit the archive**:

```bash
git add product/backlog/
git add product/resolved/
git commit -m "Archive completed backlog item: [item-id]"
```

See `/product/README.md` for detailed archiving procedures and best practices.

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

### Pattern Discovery Helpers

For refactoring or migration tasks, use these grep patterns to comprehensively find and count target files:

**Finding files with specific patterns:**
```bash
# Find files containing a pattern
find . -name "*.cs" -exec grep -l "public ITestOutputHelper Output" {} +

# Find files with multiple patterns (all must match)
find . -name "*.cs" -exec grep -l "pattern1" {} + | xargs grep -l "pattern2"

# Find with context (see surrounding lines)
find . -name "*.cs" -exec grep -B2 -A2 "pattern" {} +
```

**Counting matches:**
```bash
# Count files matching a pattern
find . -name "*.cs" -exec grep -l "pattern" {} + | wc -l

# Count total occurrences (not just files)
find . -name "*.cs" -exec grep -o "pattern" {} + | wc -l
```

**Finding boilerplate for cleanup:**
```bash
# Common test boilerplate patterns
find . -name "*.cs" -exec grep -l "public ITestOutputHelper Output" {} +
find . -name "*.cs" -exec grep -l "private readonly ITestOutputHelper" {} +

# Common service registration patterns  
find . -name "*.cs" -exec grep -l "Services.AddLogging" {} +

# Find usages of deprecated APIs
find src -name "*.cs" -exec grep -l "OldApiName" {} +
```

**Why use these helpers:**
- Ensures comprehensive coverage (don't miss files)
- Provides accurate scope estimates upfront (see "Verify Backlog Item Accuracy")
- Documents search methodology for PR reviewers
- Catches edge cases (different formatting, naming variations)

**Example workflow:**
```bash
# 1. Find all target files
find . -name "*.cs" -exec grep -l "old pattern" {} + > /tmp/target-files.txt

# 2. Review the list
cat /tmp/target-files.txt

# 3. Count for scope validation
wc -l /tmp/target-files.txt

# 4. Implement changes on each file
while read file; do
    # Make changes...
done < /tmp/target-files.txt
```

### Bulk Migration Strategies

When implementing changes that affect many similar files (e.g., test migrations, API updates), use appropriate automation strategies:

**Decision Criteria:**

| Files Affected | Strategy | Examples |
|----------------|----------|----------|
| 1-5 files | Manual edits | Individual file updates, high variation between files |
| 5-15 files | Helper patterns | Factory methods, shared actor templates, base classes |
| 15+ files | Scripting/automation | sed/awk scripts, code generation, refactoring tools |

**Helper Pattern Examples:**

For actor-based migrations:
```csharp
// Instead of duplicating actor classes 25 times:
public class NoOpProcessorActor<T> : IProcessor<T>
{
    public Task ProcessAsync(T item, CancellationToken ct) => Task.CompletedTask;
}

// Reuse in tests:
var processor = new NoOpProcessorActor<int>();
```

For factory patterns:
```csharp
// Create reusable factory for common test patterns:
public static class TestActorFactory
{
    public static IProcessor<T> CreateNoOpProcessor<T>() => new NoOpProcessorActor<T>();
    public static ITransformer<TIn, TOut> CreatePassthrough<TIn, TOut>() => ...;
}
```

**When to use each approach:**

- **Manual edits**: High variation between files, each needs different logic
- **Helper patterns**: Repetitive patterns but reusable abstractions make sense
- **Scripting**: Very similar mechanical changes (e.g., namespace updates, using directives)

**Best Practices:**

1. If creating helpers, add them to appropriate location (test helpers in test project)
2. Document the helper pattern so future migrations can reuse
3. Consider creating the helper first, migrating 2-3 files as proof-of-concept, then scale
4. Balance: helper creation time vs manual edit time (helpers valuable if saves >30min)

### NuGet Package Dependency Updates

**For NuGet package updates** (security fixes, version updates, dependency conflicts):

📖 **See [Dependency Update Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md)** for comprehensive guidance on:
- Dependency update patterns and package families
- Version selection (security fixes, .NET compatibility)
- Handling package conflicts and downgrade warnings
- Validation and testing approaches
- Common scenarios and best practices

**Quick reference for common tasks:**
- Check for outdated packages: `dotnet list package --outdated`
- Check for vulnerabilities: `dotnet list package --vulnerable`
- See `.team/NUGET_DEPENDENCY_UPDATES.md` for detailed patterns

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

**For NuGet package dependency updates**: See [Dependency Update Guide](../../.team/NUGET_DEPENDENCY_UPDATES.md) section on "Validation and Testing" for specific guidance on:
- When build verification is sufficient vs full test suite
- Vulnerability scanning with `dotnet list package --vulnerable`
- Handling pre-existing test failures
- Production vs dev-only dependency testing approaches

**For changes requiring external runtime dependencies** (databases, message queues, OTLP endpoints):
- See handover documentation for external dependency requirements
- Build verification confirms package compatibility even without runtime validation
- If external services unavailable, document in commit message why runtime validation was not performed
- See Step 0 (Handover Review) for checklist on external dependency documentation

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
5. Create feedback issue under `[Workflow Feedback] Tracker` parent
6. Link feedback issue using `sub_issue_write` MCP tool

**Creating Feedback Issue:**
```python
# Find parent tracker
results = search_issues(
    owner="uniun-technology",
    repo="lib-dataflow",
    query='"[Workflow Feedback] Tracker" in:title state:open'
)

# Create feedback issue
child = issue_write(
    method="create",
    owner="uniun-technology",
    repo="lib-dataflow",
    title="[Brief improvement description]",
    labels=["workflow:process-modeling"],
    body="""## Workflow Feedback Entry

**Date**: YYYY-MM-DD
**Issue/PR**: #XXX
**Workflow**: Implementation Workflow

### What Worked Well
[List positives]

### What Didn't Work Well
[List issues]

### Suggested Improvement
[Specific improvement]
"""
)

# Link to parent
sub_issue_write(
    method="add",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=results[0].number,
    sub_issue_id=child.id
)
```

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

## Handover to Next Workflow

When implementation is complete or encounters issues requiring other workflows, use the workflow topology system to transition the issue.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Implementation Complete

**When**: Implementation is successful and merged

Using GitHub MCP tools (primary method):
```python
# Close issue with comment
issue_write(
    method="update",
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE,
    state="closed"
)

add_issue_comment(
    owner="uniun-technology",
    repo="lib-dataflow",
    issue_number=ISSUE,
    body="""✅ **Implementation Complete**

Successfully implemented [feature/fix].

**Changes**:
- [Summary of changes]
- PR: #[PR_NUMBER]

**Documentation**: [docs updated]

All success criteria met.

See: `.team/prompts/IMPLEMENTATION_WORKFLOW.md`"""
)
```

**Or use CLI for manual operations:**
```bash
gh issue close $ISSUE --comment "✅ **Implementation Complete**

Successfully implemented [feature/fix].

**Changes**:
- [Summary of changes]
- PR: #[PR_NUMBER]

**Documentation**: [docs updated]

All success criteria met.

See: \`.team/prompts/IMPLEMENTATION_WORKFLOW.md\`"
```

### Handover to Tech Debt

**When**: Implementation reveals technical debt that should be addressed

```bash
gh issue edit $ISSUE \
  --remove-label "workflow:implementation" \
  --add-label "workflow:tech-debt"

gh issue comment $ISSUE --body "🔧 **Handover: Implementation → Tech Debt**

Implementation revealed technical debt that should be addressed.

**Tech Debt Identified**:
- [Description of tech debt]
- Location: [files/areas affected]

**Context**: [Why this surfaced during implementation]

See: \`.team/prompts/TECH_DEBT_WORKFLOW.md\`"
```

**Or use the handover script**:
```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE implementation tech-debt "Implementation revealed technical debt in [area]"
```

### Handover to Research

**When**: Implementation uncovers unknowns requiring research

```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE implementation research "Implementation revealed unknowns requiring validation. See comments for details."
```

### Handover to Triage

**When**: Requirements were unclear or need re-evaluation

```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE implementation triage "Requirements unclear during implementation. Needs reassessment."
```

### Handover to Product Prioritization

**When**: Implementation is paused pending prioritization decision

```bash
./.github/scripts/workflow/handover-issue.sh \
  $ISSUE implementation product-backlog "Implementation paused, needs prioritization decision"
```

---

## Related Documentation

- **Entry point**: `.github/copilot-instructions.md` - Start here
- **Research workflow**: `.team/prompts/RESEARCH_WORKFLOW.md`
- **Implementation folder**: `/implementation/README.md`
- **Workflow feedback**: Search for `[Workflow Feedback] Tracker` issue
