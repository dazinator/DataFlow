# ADR-001: Workflow State Storage Using GitHub Labels

**Date**: 2025-11-09

**Status**: Proposed

**Decision Makers**: Research Team

## Context

The repository needs a centralized workflow topology system to coordinate issue progression across multiple workflows:
- Triage Workflow (new)
- Research Workflow
- Implementation Workflow  
- Tech Debt Workflow
- Product Prioritization Workflow
- Process Modeling Workflow

### Current State Problems

1. **No Central State Tracking**: Issues tracked only by GitHub status (open/closed), no workflow designation
2. **Ad-hoc Handovers**: Manual backlog file creation, no formal handover protocol
3. **No Workflow Queues**: Different entry points per workflow (issue templates, backlog files, improvement files)
4. **Limited Coordination**: File-based state can lead to merge conflicts

### Requirements

- Central state tracking for workflow designation
- Safe concurrent PR coordination
- Formal handover mechanisms
- Unified query interfaces
- Audit trail of transitions
- Low implementation and maintenance burden
- Easy migration from current system

## Decision

**We will use GitHub Labels as the primary state storage mechanism** for workflow designation in Phase 1 (MVP).

### Label Schema

**Workflow Designation**:
- `workflow:triage` - Default for new issues
- `workflow:research` - Research workflow
- `workflow:implementation` - Implementation workflow
- `workflow:tech-debt` - Tech debt workflow
- `workflow:product-backlog` - Product prioritization
- `workflow:process-modeling` - Process modeling

**Optional Enhancement** (Phase 1.5):
- `priority:high/medium/low` - Priority labels
- `status:blocked` - Blocked state

### Phase 2 Option: Projects v2 Rich Metadata

If workflows require rich metadata (priority fields, owner assignment, batch tracking):
- Add GitHub Projects v2 with selective fields
- Keep labels as primary state (visible in UI)
- Use Projects for complex queries only
- Progressive adoption (opt-in per workflow)

## Alternatives Considered

### Alternative 1: Pure GitHub Projects v2 API-Driven

**Approach**: Use GitHub Projects v2 fields as canonical state, with labels as mirrors.

**Components**:
- Project fields: `designation`, `status`, `priority`, `owner`, `batch`, `version`
- Slash command protocol for transitions (`/handover to: research`)
- Python agent for state management
- GraphQL queries for complex filtering
- Version field for optimistic locking

**Pros**:
- ✅ Rich structured metadata
- ✅ Complex multi-field queries
- ✅ Explicit conflict detection via version field
- ✅ Structured audit trail

**Cons**:
- ❌ High implementation complexity (~1,200 LoC vs ~100 LoC for labels)
- ❌ Requires Python agent, GraphQL knowledge
- ❌ Less visible in GitHub UI (need project view)
- ❌ Higher maintenance burden
- ❌ Complex migration

**Why Not Chosen**: Complexity doesn't justify benefits for typical usage. Rich metadata not immediately needed.

### Alternative 2: File-Based State (Current Implicit)

**Approach**: Store workflow state in repository files (e.g., `/workflow-state/issues/[number]/state.md`)

**Pros**:
- ✅ Rich content possible (markdown docs)
- ✅ Version controlled

**Cons**:
- ❌ Merge conflicts on concurrent PRs
- ❌ Branching issues (state diverges per branch)
- ❌ Complex queries (need to parse files)
- ❌ Not centralized (GitHub is not source of truth)

**Why Not Chosen**: Merge conflict risk, not truly centralized, complex querying.

### Alternative 3: Hybrid (Label + Optional Projects) - SELECTED

**Approach**: Start with labels (Phase 1), optionally add Projects v2 for specific workflows needing rich metadata (Phase 2).

**Phase 1 (MVP)**:
- Labels for workflow designation
- Simple GitHub CLI queries
- Low complexity (~100 LoC)

**Phase 2 (If Needed)**:
- Add Projects v2 with selective fields
- Keep labels as primary state
- Opt-in for workflows needing rich metadata

**Pros**:
- ✅ Start simple, scale as needed
- ✅ Low risk MVP
- ✅ Upgrade path for future needs
- ✅ Progressive adoption
- ✅ Best of both worlds

**Cons**:
- ⚠️ If Phase 2 adopted, need to maintain two systems (labels + projects)

**Why Chosen**: Delivers immediate value with minimal risk, provides flexibility for future growth.

## Comparison Matrix

| Aspect | Labels (Phase 1) | Projects v2 API | Hybrid (Chosen) |
|--------|------------------|-----------------|-----------------|
| **Complexity** | Low (~100 LoC) | High (~1,200 LoC) | Low → Medium |
| **Rich Metadata** | ❌ Limited | ✅ Comprehensive | ⚠️ Optional |
| **Visibility** | ✅ High (labels in UI) | ⚠️ Medium (project view) | ✅ High |
| **Queries** | ✅ Simple (CLI) | ✅ Complex (GraphQL) | ✅ Both |
| **Concurrency** | ⚠️ Atomic (LWW) | ✅ Version-based | ⚠️ → ✅ |
| **Audit Trail** | ⚠️ Manual | ✅ Structured | ⚠️ → ✅ |
| **Maintenance** | ✅ Low | ❌ High | ✅ Low → Medium |
| **Migration** | ✅ Easy | ❌ Complex | ✅ Incremental |
| **Risk** | ✅ Low | ⚠️ Medium-High | ✅ Low |

## Consequences

### Positive

1. **Fast Time to Value**: Phase 1 deployable in < 2 hours
2. **Low Risk**: Proven technology (GitHub labels), minimal complexity
3. **High Visibility**: Workflow state visible in issue list, no special tools needed
4. **Easy Migration**: Add labels to existing issues via script
5. **Simple Queries**: GitHub CLI label queries (< 1 second)
6. **Safe Concurrency**: Different issues = no conflicts (typical case)
7. **Future-Proof**: Can add Projects v2 later if needs emerge
8. **Low Maintenance**: No complex agent code, minimal moving parts

### Negative

1. **Limited Rich Metadata**: Can't store structured data like priority numbers, owner assignment (Phase 1)
   - *Mitigation*: Add optional labels (e.g., `priority:high`) or move to Phase 2 if needed
2. **Same-Issue Race Condition**: Last-write-wins for concurrent updates to same issue
   - *Mitigation*: Rare in practice (agents work on different issues), comment audit trail, manual correction
3. **No Built-In History**: Label changes are events, not stored history
   - *Mitigation*: GitHub Actions can log transitions in comments
4. **Complex Queries Limited**: Can't query "priority > 50 AND workflow=research"
   - *Mitigation*: Use Phase 2 Projects if complex queries needed

### Risks

1. **Risk**: Same-issue concurrent updates lead to incorrect state
   - **Likelihood**: Low (agents coordinate, work on different issues)
   - **Impact**: Low (comment history shows transitions, manual correction)
   - **Mitigation**: Monitor for conflicts, move to Phase 2 if frequent

2. **Risk**: Label-based system doesn't scale to rich metadata needs
   - **Likelihood**: Medium (may need priority sorting, batch tracking)
   - **Impact**: Low (Phase 2 option available)
   - **Mitigation**: Progressive adoption of Projects v2

3. **Risk**: Manual label manipulation breaks workflow state
   - **Likelihood**: Low (humans use UI carefully)
   - **Impact**: Low (easy to correct)
   - **Mitigation**: Documentation, review process

## Implementation

### Phase 1 (MVP - Immediate)

**Duration**: 1-2 hours

1. Create label schema (6 workflow labels)
2. Deploy auto-label GitHub Action (new issues → `workflow:triage`)
3. Migration script (add labels to existing issues)
4. Document query patterns for each workflow
5. Document handover patterns (label change + comment)
6. Update workflow documentation

**Deliverables**:
- `.github/workflows/auto-label-triage.yml`
- Label creation via GitHub UI or API
- Migration script (`/tmp/migrate-labels.sh`)
- Integration patterns guide
- Updated workflow docs

### Phase 2 (Optional - If Needed)

**Trigger Criteria**: Any of these needs identified:
- Priority-based sorting required (Product Prioritization)
- Batch tracking needed (Process Modeling bulk mode)
- Owner assignment tracking required
- Same-issue conflicts occurring frequently (> 5%)

**Duration**: 3-5 hours

1. Create GitHub Project v2 "Workflow Control"
2. Add selective fields (only those needed: priority, batch, etc.)
3. Populate fields for existing issues
4. Add GraphQL queries for complex filtering
5. Optional: Slash command parser for transitions
6. Update documentation (when to use Projects vs labels)

## Validation

### Success Metrics (Phase 1)

**Quantitative**:
- ✅ 100% new issues auto-labeled within 10 seconds
- ✅ < 2 seconds query time for workflow queues
- ✅ 0 merge conflicts on state (different issues)
- ✅ < 5% same-issue concurrent update conflicts

**Qualitative**:
- ✅ All workflows can query their queues easily
- ✅ Handover process is clear and intuitive
- ✅ State visible in GitHub UI without special tools
- ✅ No major pain points reported by users

### Reassessment Criteria (Phase 2)

Monitor Phase 1 for 1-2 weeks. Reassess if:
- Complex queries needed (multi-field filters)
- Same-issue conflicts frequent
- Priority-based sorting required
- Batch tracking needed
- Structured audit trail essential

## Related

- **Comparison Document**: `/research/workflow-topology-design/design/comparison-matrix.md`
- **Integration Patterns**: `/research/workflow-topology-design/design/integration-patterns.md`
- **Prototypes**: `/research/workflow-topology-design/benchmarks/`
- **Handover Document**: `/research/workflow-modeling/handover-workflow-topology.md`

## Notes

This decision supports the repository's incremental, iterative approach:
- Start simple (labels)
- Validate with real usage
- Scale complexity only when justified by actual needs
- Maintain flexibility for future enhancements

The label-based approach aligns with the repository's preference for:
- Minimal necessary changes
- Proven technology over novel approaches
- Low maintenance burden
- Easy debugging and understanding
