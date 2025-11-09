# Approach 3: Hybrid (Labels + Optional Rich Metadata)

## Core Concept

Use **GitHub Labels as primary state** (simple, visible, proven) with **optional Project v2 fields** for workflows that need rich metadata (priority, owner, batch tracking). Start simple, scale complexity only where needed.

## Architecture Overview

### Phase 1: Label-Based Foundation (MVP)

**Primary State: Labels**
- `workflow:triage`, `workflow:research`, etc. (workflow designation)
- State tracked via labels (simple, visible)
- All workflows query by labels
- Handover via label changes + comments

**Benefits:**
- ✅ Simple to implement (~100 LoC)
- ✅ Visible in GitHub UI
- ✅ Low maintenance
- ✅ Easy migration (add labels to existing issues)

### Phase 2: Rich Metadata (Optional Enhancement)

**When Needed:** Workflows that require priority, owner, batch tracking, or complex queries

**Add Project v2 with Selective Fields:**
- `priority`: number (0-100) - for Product Prioritization workflow
- `owner`: text - for assignment tracking
- `batch`: text - for bulk processing runs

**Keep Labels as Source of Truth:**
- Labels remain primary for workflow designation
- Project fields supplement with additional metadata
- Queries can use either labels (simple) or Projects (complex)

### State Model

**Labels (Always Present):**
- `workflow:triage`
- `workflow:research`
- `workflow:implementation`
- `workflow:tech-debt`
- `workflow:product-backlog`
- `workflow:process-modeling`

**Project Fields (Optional):**
- `priority`: number (default: null) - only set by Product Prioritization
- `owner`: text (default: null) - only set when assigned
- `batch_id`: text (default: null) - only set during bulk processing
- `notes`: text (default: null) - for additional context

**No `designation` field in Project** - labels are the source of truth for workflow state

**No `version` field** - rely on GitHub's atomic operations for labels

### Query Patterns

**Simple Query (Label-Based):**
```bash
# Most workflows use this
gh issue list --label "workflow:research" --state open
```

**Complex Query (Project-Based, when needed):**
```graphql
# Product Prioritization workflow
query HighPriorityImplementation {
  repository(owner: "owner", name: "repo") {
    issues(labels: ["workflow:implementation"], first: 50) {
      nodes {
        number
        title
        projectItems(first: 1) {
          nodes {
            fieldValueByName(name: "priority") {
              ... on ProjectV2ItemFieldNumberValue {
                number
              }
            }
          }
        }
      }
    }
  }
}
# Then filter/sort in code: items where priority > 70
```

### Transition Mechanism

**Phase 1 (MVP):** Simple label changes
```bash
gh issue edit $ISSUE \
  --remove-label "workflow:research" \
  --add-label "workflow:implementation"

gh issue comment $ISSUE \
  --body "Handover: Research validated. Ready for implementation."
```

**Phase 2 (Enhanced):** Optional slash commands for metadata
```
/handover to: implementation priority: 80
```

GitHub Actions can parse and update both labels and Project fields:
1. Change label (always)
2. Set priority field (if provided)
3. Post structured comment

### Implementation Complexity

**Phase 1 (MVP):**
- GitHub Actions: 1 workflow (auto-label new issues)
- Query scripts: Bash/CLI (label-based)
- Handover: Document + example scripts
- **Total: ~100 LoC**

**Phase 2 (Enhanced, if needed):**
- Add GitHub Project v2 with selective fields
- Add slash command parser (Python, ~100-200 LoC)
- Add GraphQL queries for priority-based queries (~50 LoC)
- **Additional: ~200 LoC**

### Concurrency Model

**Phase 1:** GitHub atomic label updates (same as Approach 1)
- Different issues: No conflicts
- Same issue: Last-write-wins (rare, acceptable)

**Phase 2 (if optimistic locking needed):**
- Add `version` field to Project
- Use conditional updates for workflows that need it
- Most workflows continue using simple label updates

### Migration Path

**Phase 1 (Immediate):**
1. Create workflow labels
2. Deploy auto-label GitHub Action
3. Add labels to existing issues
4. Update workflow docs with query patterns
5. **Time: 1-2 hours**

**Phase 2 (Later, if needed):**
1. Create GitHub Project v2
2. Add selective fields (priority, owner, batch)
3. Deploy slash command parser
4. Update Product Prioritization workflow to use priority field
5. **Time: 3-5 hours**

### Progressive Adoption Model

Different workflows adopt features based on their needs:

| Workflow | Phase 1 (Labels) | Phase 2 (Rich Metadata) |
|----------|------------------|-------------------------|
| **Triage** | ✅ Label queries only | ❌ Not needed |
| **Research** | ✅ Label queries + handover | ❌ Not needed |
| **Implementation** | ✅ Label queries only | ⚠️ Optional (priority queries) |
| **Tech Debt** | ✅ Label queries + handover | ❌ Not needed |
| **Product Prioritization** | ✅ Label queries | ✅ Priority field for sorting |
| **Process Modeling** | ✅ Label queries + bulk | ⚠️ Optional (batch tracking) |

**Key Insight:** Most workflows only need labels. Rich metadata is opt-in for specific workflows.

## Strengths

1. **✅ Start Simple, Scale Complexity**
   - MVP requires minimal implementation
   - Enhanced features added only when needed
   - No upfront commitment to complex system

2. **✅ Low Initial Risk**
   - Phase 1 is proven (labels)
   - Phase 2 is optional enhancement
   - Can deploy incrementally

3. **✅ Visible State (Labels)**
   - Workflow designation always visible in UI
   - Labels work with existing GitHub features
   - No dependency on Project view

4. **✅ Flexibility**
   - Workflows choose their complexity level
   - Can mix simple and complex patterns
   - Easy to add features as needs emerge

5. **✅ Easier Migration**
   - Phase 1 is simple (add labels)
   - Phase 2 can be deferred
   - Gradual adoption reduces risk

6. **✅ Maintainable**
   - Most workflows stay simple (labels only)
   - Complex features isolated to workflows that need them
   - Clear separation of concerns

## Limitations

1. **❌ Two Systems (if Phase 2 adopted)**
   - Labels + Projects adds complexity
   - Need to keep them in sync (if both used)
   - Documentation must cover both patterns

2. **❌ Partial Audit Trail**
   - Phase 1: Manual comment-based history
   - Phase 2: Structured only for workflows using Projects
   - Inconsistent across workflows

3. **❌ Query Complexity Varies**
   - Simple workflows: Easy (labels)
   - Complex workflows: Harder (GraphQL + labels)
   - Learning curve increases in Phase 2

4. **❌ Same-Issue Race Condition (Phase 1)**
   - Still last-write-wins for label updates
   - Only fixed in Phase 2 with version field
   - Acceptable for most workflows

## Recommendation for Hybrid Approach

**Best for:**
- Projects that want to start simple and iterate
- Mixed workflow complexity (some simple, some complex)
- Uncertainty about long-term metadata needs
- Teams with limited initial implementation capacity
- Want low-risk MVP with upgrade path

**Implementation Strategy:**

1. **Start with Phase 1 (MVP)**
   - Deploy label-based system
   - Validate workflow integration
   - Gather feedback on metadata needs

2. **Assess Need for Phase 2**
   - Monitor if workflows struggle with label limitations
   - Identify specific metadata requirements
   - Evaluate if complexity is justified

3. **Incrementally Adopt Phase 2**
   - Add Project fields for specific workflows
   - Keep labels as primary state
   - Document when to use Projects vs labels

**Decision Criteria for Phase 2:**
- ❓ Do we need priority-based sorting? (Product Prioritization)
- ❓ Do we need owner assignment tracking? (Team coordination)
- ❓ Do we need batch run auditing? (Process Modeling bulk mode)
- ❓ Do we need explicit conflict detection? (Concurrent updates on same issue)

If **all answers are NO**: Stay with Phase 1 (labels only)
If **any answer is YES**: Consider Phase 2 selectively

## Comparison Matrix

| Aspect | Pure Labels (A1) | Pure Projects (A2) | Hybrid (A3) |
|--------|------------------|-----------------------|-------------|
| **Initial Complexity** | Low (~100 LoC) | High (~1,200 LoC) | Low (~100 LoC) |
| **Final Complexity** | Low (stays simple) | High (all features) | Medium (~300 LoC) |
| **Rich Metadata** | ❌ Limited | ✅ Comprehensive | ⚠️ Selective |
| **Visibility** | ✅ High (labels) | ⚠️ Medium (project) | ✅ High (labels) |
| **Query Flexibility** | ❌ Basic | ✅ Advanced | ⚠️ Mixed |
| **Concurrency Control** | ⚠️ Atomic (LWW) | ✅ Optimistic lock | ⚠️ Atomic (Phase 1) |
| **Audit Trail** | ❌ Manual | ✅ Structured | ⚠️ Mixed |
| **Maintenance** | ✅ Low | ❌ High | ⚠️ Medium |
| **Migration** | ✅ Easy | ❌ Complex | ✅ Incremental |
| **Risk** | ✅ Low | ⚠️ Medium-High | ✅ Low (MVP) |
| **Scalability** | ❌ Limited | ✅ Scales | ✅ Scales |
| **Upgrade Path** | ❌ Requires rewrite | ➖ N/A (complete) | ✅ Progressive |

## Recommended Adoption Path

### Step 1: MVP (Phase 1 - Labels)
**Time:** 1-2 hours
**Deliverables:**
- Auto-label GitHub Action
- Label schema documentation
- Query pattern examples
- Handover pattern documentation

**Validate:**
- All workflows can query by labels
- Handover mechanism works
- Concurrent PR safety (different issues)

### Step 2: Monitor and Assess (1-2 weeks)
**Questions:**
- Are label-based queries sufficient?
- Do workflows need priority sorting?
- Is batch tracking needed?
- Are same-issue conflicts occurring?

### Step 3: Selective Enhancement (Phase 2 - If Needed)
**Time:** 3-5 hours
**Deliverables:**
- GitHub Project v2 with selective fields
- Slash command parser (optional)
- GraphQL queries for complex filtering
- Updated workflow documentation

**Adopt selectively:**
- Product Prioritization: Add priority field
- Process Modeling: Add batch field (optional)
- Other workflows: Continue with labels only

## Summary

The hybrid approach provides:
- **Low-risk MVP** with labels (proven, simple)
- **Upgrade path** to rich metadata (if needed)
- **Flexibility** for workflows to choose complexity
- **Incremental adoption** reduces upfront investment
- **Best of both worlds** (simplicity + scalability)

**Recommendation:** Start with Phase 1 (labels), defer Phase 2 unless specific needs emerge.
