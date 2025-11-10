# Research Plan: Workflow Topology Design

## Research Objective

Design and validate a centralized workflow topology system for coordinating GitHub issue progression across multiple workflows (Triage, Research, Implementation, Tech Debt, Product Prioritization, Process Modeling).

The system must provide:
- Central state tracking for issue workflow designation
- Safe concurrent PR coordination
- Formal handover mechanisms between workflows
- Unified query interfaces for workflow queues
- Audit trail of workflow transitions

## Research Questions

1. **State Storage**: What is the optimal approach for storing workflow state?
   - Pure GitHub Labels (suggested in handover)
   - GitHub Projects v2 fields (alternative in issue)
   - Hybrid approach combining both
   - File-based state (current implicit approach)

2. **Concurrency Safety**: How do we ensure safe concurrent updates?
   - GitHub API conflict detection mechanisms
   - ETag-based optimistic locking
   - Label vs Project field update semantics
   - Race condition handling

3. **Query Patterns**: What is the most effective way to query workflow queues?
   - GitHub CLI label filtering
   - GitHub API label queries
   - Projects v2 GraphQL queries
   - Performance and rate limit considerations

4. **Transition Mechanisms**: How should workflow handovers be performed?
   - Slash commands in comments
   - Label changes with audit comments
   - Project field updates via API
   - GitHub Actions automation

5. **Developer Experience**: What provides the best UX for agents and humans?
   - Visibility of workflow state
   - Ease of querying and handover
   - Debugging and audit trail
   - Integration with existing tools

6. **Integration Complexity**: What is the implementation burden?
   - GitHub Actions workflow development
   - Python/Bash scripting requirements
   - Maintenance and testing overhead
   - Migration path from current system

## Success Metrics

### Quantitative Metrics
- **Concurrency Safety**: 100% conflict detection in concurrent PR scenarios (measure via test)
- **Query Performance**: < 2 seconds to query workflow queues (measure via benchmarks)
- **Integration Effort**: Estimate LoC and number of files for each approach
- **API Rate Limits**: Query count per workflow operation (measure via API calls)

### Qualitative Metrics
- **Visibility**: Can humans easily see workflow state in GitHub UI?
- **Simplicity**: Is the query/handover pattern intuitive for agents?
- **Auditability**: Can transitions be tracked and reviewed?
- **Maintainability**: Is the system easy to understand and modify?

### Baseline (Current State)
- No central workflow state tracking
- Manual handover via backlog file creation
- Ad-hoc entry points per workflow
- No formal audit trail
- File-based state leads to potential merge conflicts

### Validation Approach
- **Concurrent PR Test**: Simulate 2+ PRs modifying different issues simultaneously
- **Query Benchmark**: Measure time to query 10, 50, 100 issues with different approaches
- **Integration Prototype**: Build minimal working examples for each approach
- **Usability Assessment**: Document developer experience for common operations

## Validation Approach

### Phase 1: Approach Analysis (2-3 hours)
Compare three approaches systematically:

1. **Label-Based Approach** (from handover document)
   - Document architecture and components
   - Identify strengths and limitations
   - Estimate implementation complexity

2. **API/Projects-Based Approach** (from issue alternative)
   - Document architecture and components
   - Identify strengths and limitations
   - Estimate implementation complexity

3. **Hybrid Approach** (potential combination)
   - Explore if combination provides best of both
   - Document architecture and components
   - Identify strengths and limitations

**Deliverable**: Comparison matrix across criteria (state storage, concurrency, queries, transitions, DX, complexity)

### Phase 2: Prototype Critical Paths (3-4 hours)
Build minimal prototypes to validate key assumptions:

1. **GitHub Actions Auto-Labeling**
   - Create workflow to auto-label new issues
   - Test label change event handling
   - Measure GitHub Actions execution time

2. **Projects v2 Field Updates**
   - Test GraphQL mutations for field updates
   - Test concurrent update conflict detection
   - Measure API response times

3. **Query Patterns**
   - Benchmark GitHub CLI label queries
   - Benchmark Projects v2 GraphQL queries
   - Test filtering and sorting capabilities

4. **Concurrent PR Simulation**
   - Create test scenario with 2 PRs, different issues
   - Validate conflict detection works
   - Document race condition handling

**Deliverable**: Working prototypes with benchmark data

### Phase 3: Design Validation (2-3 hours)
Validate selected approach against requirements:

1. **Integration Scenarios**
   - Triage workflow integration
   - Research workflow integration
   - Implementation workflow integration
   - Tech Debt workflow integration
   - Product Prioritization integration
   - Process Modeling bulk mode integration

2. **Edge Cases**
   - Re-triage (workflow reassignment)
   - Blocked states
   - Multi-workflow handoffs
   - Error handling and recovery

3. **Migration Path**
   - Current system → new system
   - Backward compatibility considerations
   - Rollout strategy

**Deliverable**: Integration patterns document, edge case analysis

## Expected Outcomes

### Research Artifacts
- `/research/workflow-topology-design/README.md` - Final research documentation
- `/research/workflow-topology-design/design/` - Design documents
  - `comparison-matrix.md` - Approach comparison
  - `integration-patterns.md` - How workflows integrate
  - `migration-guide.md` - Rollout plan
- `/research/workflow-topology-design/benchmarks/` - Performance data
  - `query-benchmarks.md` - Query performance measurements
  - `concurrency-tests.md` - Concurrent PR test results
- `/research/workflow-topology-design/notes/` - Working notes during exploration

### ADRs (in codebase, not research folder)
- `.team/adr/YYYY-MM-DD-workflow-state-storage.md` - State storage decision
- `.team/adr/YYYY-MM-DD-handover-mechanism.md` - Transition protocol decision

### Handover Materials
- `/research/workflow-topology-design/handover/github-issue-implement-workflow-topology.md` - Implementation issue
- `/research/workflow-topology-design/handover/prototype/` - Reference prototype code (if applicable)
  - GitHub Actions workflows
  - Query scripts
  - Test scenarios

### Product Backlog Items (if needed)
If research recommends multi-phase implementation, create backlog items for:
- Phase 1: Core state management
- Phase 2: Triage workflow
- Phase 3: Workflow integrations
- Phase 4: Documentation updates

## Timeline

### Week 1 (Current)
- **Day 1-2**: Approach analysis and comparison (Phase 1)
- **Day 3-4**: Critical path prototyping (Phase 2)
- **Day 5**: Design validation and documentation (Phase 3)

### Week 2 (If needed)
- **Day 1-2**: Handover materials creation
- **Day 3**: Self-improvement evaluation and PR finalization

**Total Estimated Effort**: 7-10 hours of active research

## Key Constraints

1. **Must maintain backward compatibility** during migration
2. **Must not break existing workflow patterns** without migration path
3. **Must support concurrent PR operations** safely
4. **Must be maintainable** by future agents and developers
5. **Must respect GitHub API rate limits**
6. **Must provide clear audit trail** for workflow transitions

## References

- **Handover Document**: `/research/workflow-modeling/handover-workflow-topology.md`
- **Current Workflows**: `.team/prompts/*.md`
- **Research Workflow Process**: `.team/prompts/RESEARCH_WORKFLOW.md`
- **Folder Structure Guide**: `/research/FOLDER_STRUCTURE.md`
