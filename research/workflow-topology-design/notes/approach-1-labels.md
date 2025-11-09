# Approach 1: Label-Based Workflow State

## Source
From handover document: `/research/workflow-modeling/handover-workflow-topology.md`

## Architecture Overview

### Core Concept
Use GitHub Labels as the canonical source of truth for workflow state. Labels represent current workflow designation (triage, research, implementation, etc.).

### State Model
**Labels:**
- `workflow:triage` - Default for new issues, pending assessment
- `workflow:research` - Designated to Research Workflow
- `workflow:implementation` - Designated to Implementation Workflow
- `workflow:tech-debt` - Designated to Tech Debt Workflow
- `workflow:product-backlog` - Designated to Product Prioritization
- `workflow:process-modeling` - Designated to Process Modeling

**State Transitions:**
- Triage → Any workflow (based on assessment)
- Research → Implementation, Product, Triage (handover)
- Implementation → Product, Tech-Debt, Closed
- Tech-Debt → Implementation, Closed
- Product → Implementation, Research, Closed
- Process-Modeling → Any, Triage, Closed

### Components

#### 1. Auto-Label New Issues (GitHub Actions)
```yaml
name: Auto-Label New Issues
on:
  issues:
    types: [opened]

jobs:
  auto-label:
    runs-on: ubuntu-latest
    steps:
      - name: Add triage label
        uses: actions/github-script@v7
        with:
          script: |
            github.rest.issues.addLabels({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              labels: ['workflow:triage']
            })
```

#### 2. Query Pattern (GitHub CLI)
```bash
# Get issues designated to specific workflow
gh issue list \
  --label "workflow:research" \
  --state open \
  --json number,title,url,labels
```

#### 3. Handover Pattern
1. Agent changes label (remove old, add new)
2. Agent posts comment explaining transition
3. Label change is atomic operation via GitHub API

```bash
# Example handover from research to implementation
gh issue edit $ISSUE_NUMBER \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE_NUMBER \
  --body "Handover to Implementation: Research validated approach X. See /research/topic/ for details."
```

#### 4. Transition Logging (Optional)
GitHub Actions can listen to label changes and post structured comments:
```yaml
name: Log Workflow Transitions
on:
  issues:
    types: [labeled, unlabeled]

jobs:
  log-transition:
    runs-on: ubuntu-latest
    steps:
      - name: Record transition
        # Log when workflow:* labels change
        # Post comment with timestamp and transition details
```

### Concurrency Model

**GitHub API Guarantees:**
- Label updates are atomic operations
- GitHub handles concurrent label changes safely
- No explicit locking needed

**Race Condition Handling:**
- Two PRs updating labels on **different issues**: No conflict (different resources)
- Two PRs updating labels on **same issue**: GitHub API serializes, last update wins
- No ETag support for labels, but updates are atomic

**Mitigation for Same-Issue Conflicts:**
- Rare in practice (different agents work on different issues)
- If conflict occurs, comment history shows both transitions
- Human reviewer can correct if needed

### Query Performance

**GitHub API/CLI:**
- Fast label filtering (indexed)
- Supports pagination
- Can combine with state (open/closed)
- Rate limits: 5,000 requests/hour (authenticated)

**Benchmark Estimate:**
- Query 100 issues with label: < 1 second (API call)
- Filter by multiple criteria: < 2 seconds

### Integration Pattern

**For Each Workflow:**

1. **Query Queue:**
   ```bash
   WORKFLOW="research"
   gh issue list --label "workflow:$WORKFLOW" --state open
   ```

2. **Process Issue:**
   - Do workflow work
   - Create handover materials

3. **Handover:**
   ```bash
   gh issue edit $ISSUE --remove-label "workflow:research" --add-label "workflow:implementation"
   gh issue comment $ISSUE --body "Handover: Research complete. Ready for implementation."
   ```

**Bulk Processing (Process Modeling):**
- Query returns list of issues
- Process each in batch
- Update labels as transitions occur

### Strengths

1. **✅ Simple and Native**
   - Uses built-in GitHub feature
   - No external dependencies
   - Well-documented API

2. **✅ Visible in GitHub UI**
   - Labels show prominently on issues
   - Can filter by label in issue list
   - Works with GitHub Projects/milestones

3. **✅ Concurrent-Safe**
   - Atomic updates
   - Different issues = no conflicts
   - GitHub API handles serialization

4. **✅ Low Maintenance**
   - No database or state files
   - GitHub manages storage
   - Simple GitHub Actions workflows

5. **✅ Good Query Performance**
   - Fast label-based queries
   - Efficient GitHub API
   - Supports pagination

6. **✅ Easy Migration**
   - Can add labels to existing issues
   - Doesn't require file changes
   - Backward compatible

### Limitations

1. **❌ Limited Rich Metadata**
   - Labels are simple strings
   - No structured data (priority, owner, dates)
   - Need separate mechanism for rich handover docs

2. **❌ No Built-In History**
   - Label changes are events, not stored history
   - Need separate audit trail mechanism
   - Comment-based history is manual

3. **❌ Same-Issue Race Condition**
   - Two agents updating same issue = last-write-wins
   - No optimistic locking with ETags
   - Rare but possible

4. **❌ No Complex Queries**
   - Can't query "priority > 50 AND workflow=research"
   - Limited to label combinations
   - No sorting by custom fields

5. **❌ Label Proliferation**
   - Adding more metadata = more labels
   - Label namespace management
   - Can become cluttered

### Implementation Complexity

**Low to Medium:**
- **GitHub Actions**: 1-2 simple workflows (auto-label, optional logging)
- **Query Scripts**: Bash/CLI scripts, minimal complexity
- **Handover Pattern**: Document + example scripts
- **Migration**: Add labels to existing issues (one-time script)

**Estimated LoC:**
- GitHub Actions: ~50 lines (YAML)
- Query scripts: ~20 lines (Bash)
- Handover scripts: ~20 lines (Bash)
- Total: ~100 lines + documentation

### Risk Assessment

**Low Risk:**
- Proven technology (GitHub labels)
- Minimal moving parts
- Easy to understand and debug
- Easy rollback (remove labels)

**Failure Modes:**
- GitHub API outage (same as any GitHub operation)
- Rate limit exceeded (unlikely with normal usage)
- Manual label manipulation (human can fix)

## Suitability for Requirements

### Central State Tracking: ✅ Good
Labels provide clear current state, visible in UI

### Concurrent PR Safety: ✅ Good (with caveats)
Safe for different issues, last-write-wins for same issue (rare)

### Formal Handover: ✅ Good
Label change + comment provides clear handover

### Unified Queues: ✅ Excellent
Simple label-based queries work for all workflows

### Audit Trail: ⚠️ Adequate
Events exist but need GitHub Actions to log structured history

### Developer Experience: ✅ Excellent
Simple, visible, familiar GitHub feature

## Recommendation for Label-Based Approach

**Use when:**
- Simplicity and maintainability are priorities
- Rich metadata (priority, owner, etc.) not required
- Same-issue concurrent updates are rare
- Visual workflow state in GitHub UI is valuable
- Easy migration and rollback are important

**Consider enhancements:**
- Phase 2: Add optional file-based rich metadata if needed
- Use GitHub Actions to create structured audit trail in comments
- Document same-issue conflict resolution procedure
