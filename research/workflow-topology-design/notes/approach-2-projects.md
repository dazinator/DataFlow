# Approach 2: API-Driven with GitHub Projects v2

## Source
From issue alternative solution (user-provided design)

## Architecture Overview

### Core Concept
Use GitHub Projects v2 fields as the canonical source of truth, with GitHub labels as mirrors for human visibility. All state changes happen via API, not commits. Repository files store only specs, plans, and artifacts - never live state.

### State Model

**GitHub Project v2 Fields:**
- `designation`: single select - {triage, research, implementation, tech-debt, product, process-modeling, closed}
- `status`: single select - {queued, in-progress, blocked, awaiting-info, done}
- `priority`: number (0-100)
- `owner`: text or user
- `history`: text (append-only summary pointer, full log in comments)
- `batch`: text (optional batch ID for grouped processing)
- `version`: number (incremented on each transition for conflict detection)

**Label Mirrors (for visibility):**
- `wf/triage`, `wf/research`, `wf/implementation`, `wf/tech-debt`, `wf/product`, `wf/process-modeling`
- `st/queued`, `st/in-progress`, `st/blocked`, `st/awaiting-info`, `st/done`

**Rule:** Project fields are source of truth. Labels mirror fields. Agents change via API, not commits.

### State Transitions

**Slash Command Protocol (in comments):**
```
/handover to: research reason: "needs feasibility check"
/set status: in-progress
/set priority: 70
/block reason: "awaiting vendor reply"
/close reason: "superseded by #123"
```

**GitHub Actions parses and validates:**
1. Parse slash command from comment
2. Validate transition (allowed by workflow config)
3. Update Project fields + labels
4. Append structured comment with old→new state
5. Increment version field

**Transition Record Format:**
```json
{
  "transition_id": "trn_2025-11-09T12:04:15Z_abc123",
  "from": {"designation":"research","status":"in-progress","version":12},
  "to": {"designation":"implementation","status":"queued","version":13},
  "by": "github-actions[bot]",
  "reason": "Prototype validated, handing to build",
  "batch_id": "batch_2025-11-09_01"
}
```

### Components

#### 1. GitHub Project v2 Setup
**Required:**
- Create Project named "Workflow Control"
- Add custom fields (designation, status, priority, owner, history, batch, version)
- Add all repository issues to project

#### 2. Workflow Configuration
`.team/workflows/workflows.yaml`:
```yaml
workflows:
  triage:
    pull_designation: triage
    next_allowed: [research, product, tech-debt, process-modeling]
  research:
    pull_designation: research
    next_allowed: [implementation, product, triage, closed]
  implementation:
    pull_designation: implementation
    next_allowed: [product, tech-debt, closed, triage]
  tech-debt:
    pull_designation: tech-debt
    next_allowed: [implementation, closed, triage]
  product:
    pull_designation: product
    next_allowed: [implementation, research, closed, triage]
  process-modeling:
    pull_designation: process-modeling
    next_allowed: [any, triage, closed]

batch_policy:
  default:
    max_items: 5
    max_lines_changed: 500

selection_policy:
  sort: ["priority desc", "updated asc"]
```

#### 3. GitHub Actions Workflow
`.github/workflows/workflow-agent.yml`:
```yaml
name: Workflow Agent

on:
  workflow_dispatch:
    inputs:
      agent:
        description: Which agent to run
        required: true
        type: choice
        options: [triage, research, implementation, tech-debt, product, process-modeling]
      mode:
        description: Processing mode
        required: false
        default: smart
        type: choice
        options: [issue, single, multiple, smart]
      count:
        description: Count for multiple
        required: false
        type: number
  issue_comment:
    types: [created]
  issues:
    types: [labeled]

permissions:
  issues: write
  pull-requests: write
  projects: write
  contents: write

jobs:
  agent:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'
      - name: Install deps
        run: pip install -r .team/agents/requirements.txt
      - name: Run agent
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          PROJECT_NUMBER: 1
        run: |
          python .team/agents/run_agent.py \
            --agent "${{ github.event.inputs.agent || 'triage' }}" \
            --mode "${{ github.event.inputs.mode || 'smart' }}" \
            --count "${{ github.event.inputs.count || '0' }}"
```

#### 4. Agent Implementation (Python)
`.team/agents/run_agent.py`:

**Core Loop:**
1. Fetch candidates with `designation=X AND status IN {queued, awaiting-info}`
2. Lock selection using conditional requests with version ETag
3. Process in batches (respect `batch_policy`)
4. For each item:
   - Run workflow checks
   - Set `status=in-progress`
   - Produce artifacts
   - Handover: `/handover to: <next>` or `/set status: done` or `/block`
5. Post run summary with batch_id

**Stopping conditions:**
- `max_items=5`
- `max_lines_changed=500`

#### 5. Query Pattern (GraphQL)
```graphql
query GetWorkflowQueue($designation: String!, $status: [String!]!) {
  repository(owner: "owner", name: "repo") {
    projectsV2(first: 1) {
      nodes {
        items(first: 100) {
          nodes {
            fieldValueByName(name: "designation") {
              ... on ProjectV2ItemFieldSingleSelectValue {
                name
              }
            }
            fieldValueByName(name: "status") {
              ... on ProjectV2ItemFieldSingleSelectValue {
                name
              }
            }
            content {
              ... on Issue {
                number
                title
                url
              }
            }
          }
        }
      }
    }
  }
}
```

#### 6. Handover Pattern
Agent posts slash command comment:
```
/handover to: implementation reason: "Research validated approach X"
```

GitHub Actions workflow:
1. Parses slash command
2. Validates transition allowed
3. Updates Project fields via GraphQL mutation
4. Updates label mirrors
5. Increments version
6. Posts structured transition record

### Concurrency Model

**Optimistic Locking with Version Field:**
- Each item has `version` number
- Read version before update
- Update includes version check (conditional request)
- If version mismatch, update fails
- Agent retries with backoff or skips item

**Conflict Detection:**
```python
# Pseudo-code
current_version = item['version']
new_version = current_version + 1

mutation = update_project_item(
    item_id=item_id,
    fields={'designation': 'implementation', 'version': new_version},
    expected_version=current_version  # Conditional check
)

if mutation.failed:
    # Conflict detected
    log_conflict(item_id, current_version)
    retry_or_skip()
```

**Race Condition Handling:**
- Concurrent updates to same item: Conflict detected via version field
- Agent retries or posts comment about collision
- Batch field groups related operations for audit

### Query Performance

**GraphQL API:**
- More complex queries than labels
- Can filter by multiple fields in single query
- Pagination required for large result sets
- Rate limits: 5,000 points/hour (GraphQL consumes points based on complexity)

**Benchmark Estimate:**
- Query 100 issues with filters: 1-2 seconds (GraphQL query)
- More complex queries (multi-field filters): 2-3 seconds
- Need to manage GraphQL query complexity points

### Integration Pattern

**For Each Workflow:**

1. **Query Queue:**
   ```python
   items = query_graphql(
       designation='research',
       status=['queued', 'awaiting-info'],
       sort_by='priority desc'
   )
   ```

2. **Process Issue:**
   ```python
   for item in items[:batch_policy.max_items]:
       lock_item(item, version=item.version)
       update_status(item, 'in-progress')
       
       # Do workflow work
       
       handover_via_comment(item, to='implementation')
   ```

3. **Handover:**
   - Agent posts `/handover to: implementation`
   - GitHub Actions processes slash command
   - Updates Project fields + labels
   - Logs transition

**Bulk Processing:**
- Query supports sorting and filtering
- Batch policy enforced by agent
- Run summary includes batch_id for grouping

### Strengths

1. **✅ Rich Metadata**
   - Structured fields (priority, status, owner, etc.)
   - Complex queries (priority > 50 AND status=queued)
   - Full Project v2 feature set

2. **✅ Explicit Concurrency Control**
   - Version field provides optimistic locking
   - Conflict detection built-in
   - No last-write-wins scenarios

3. **✅ Structured Audit Trail**
   - History field + structured comments
   - Transition records with full context
   - Batch grouping for related operations

4. **✅ Flexible Queries**
   - GraphQL supports complex filtering
   - Sorting by multiple fields
   - Aggregation and grouping

5. **✅ Central Configuration**
   - Workflow rules in `.team/workflows/workflows.yaml`
   - Validation logic in one place
   - Easy to update allowed transitions

6. **✅ No File-Based State**
   - Avoids merge conflicts on state files
   - Repository files only for artifacts
   - Clean separation of state and specs

### Limitations

1. **❌ High Implementation Complexity**
   - Requires Python agent (`run_agent.py`)
   - GraphQL query/mutation logic
   - Slash command parser
   - GitHub Actions orchestration

2. **❌ Projects v2 Setup Overhead**
   - Manual project creation
   - Field configuration
   - Add all issues to project (ongoing)

3. **❌ Less Visible in GitHub UI**
   - Project fields less prominent than labels
   - Need to open project view to see state
   - Label mirrors help but add complexity

4. **❌ GraphQL Learning Curve**
   - More complex than REST API
   - Query/mutation syntax
   - Points-based rate limiting

5. **❌ Maintenance Burden**
   - Python dependencies
   - GraphQL schema changes
   - Agent logic testing
   - More moving parts

6. **❌ Migration Complexity**
   - Need to populate Project fields for existing issues
   - Deploy Python agent
   - Test slash command workflow
   - Multi-step migration

### Implementation Complexity

**High:**
- **GitHub Actions**: 1-2 complex workflows with Python execution
- **Python Agent**: 300-500 lines (query, mutation, validation, conflict handling)
- **GraphQL Queries**: 100-200 lines (query/mutation definitions)
- **Configuration**: YAML config parser and validator
- **Slash Command Parser**: 50-100 lines
- **Testing**: Test framework for agent logic, mock GraphQL, conflict scenarios

**Estimated LoC:**
- GitHub Actions: ~100 lines (YAML)
- Python agent: ~500 lines (Python)
- GraphQL queries/mutations: ~200 lines
- Configuration: ~100 lines (YAML + parser)
- Tests: ~300 lines
- Total: ~1,200 lines + documentation

**Dependencies:**
- Python 3.12+
- GraphQL client library
- YAML parser
- Testing framework

### Risk Assessment

**Medium-High Risk:**
- Complex system with many components
- GraphQL API stability dependency
- Python agent debugging overhead
- Slash command parsing edge cases
- Version conflict scenarios to test

**Failure Modes:**
- GraphQL API changes break queries
- Python dependency issues
- Slash command parsing bugs
- Conflict detection edge cases
- Rate limit exhaustion (less likely)
- Agent deployment/runtime issues

## Suitability for Requirements

### Central State Tracking: ✅ Excellent
Project fields provide rich, structured state with full metadata

### Concurrent PR Safety: ✅ Excellent
Version field provides explicit optimistic locking and conflict detection

### Formal Handover: ✅ Excellent
Slash commands + structured transition records provide clear audit trail

### Unified Queues: ✅ Excellent
GraphQL queries support complex filtering and sorting across all fields

### Audit Trail: ✅ Excellent
Structured transition records with full context and batch grouping

### Developer Experience: ⚠️ Mixed
- Power users: Excellent (rich queries, structured data)
- Casual users: Moderate (less visible in UI, requires project view)
- Learning curve: High (GraphQL, slash commands, agent concepts)

## Recommendation for API/Projects Approach

**Use when:**
- Rich metadata (priority, status, owner, batch, history) is required
- Complex queries (multi-field filters, sorting) are needed
- Explicit conflict detection is critical
- Structured audit trail is important
- Team has capacity for higher maintenance burden
- Python/GraphQL expertise available

**Avoid when:**
- Simplicity and low maintenance are priorities
- Team prefers native GitHub features
- Complex queries and rich metadata not needed
- Migration effort is constrained

## Key Trade-offs vs Label-Based

| Aspect | Labels (Approach 1) | Projects v2 (Approach 2) |
|--------|---------------------|-------------------------|
| **Complexity** | Low (~100 LoC) | High (~1,200 LoC) |
| **Metadata** | Limited (labels only) | Rich (custom fields) |
| **Queries** | Simple (label filters) | Complex (GraphQL multi-field) |
| **Concurrency** | Atomic (last-write-wins) | Optimistic locking (version) |
| **Visibility** | High (labels in UI) | Medium (project view) |
| **Audit Trail** | Manual (comments) | Structured (transition records) |
| **Maintenance** | Low (GitHub native) | High (Python agent + GraphQL) |
| **Migration** | Easy (add labels) | Complex (project setup + agent) |
| **Risk** | Low (proven tech) | Medium-High (complex system) |
