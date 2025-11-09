# Scenario: Concurrent PRs with Different Issues

## Context

Testing concurrency safety: Multiple PRs processing different issues simultaneously.

This is a critical requirement from the problem statement.

## Starting Point

- 5 issues in system:
  - Issue #101: labeled `workflow:research`
  - Issue #102: labeled `workflow:research`
  - Issue #103: labeled `workflow:implementation`
  - Issue #104: labeled `workflow:implementation`
  - Issue #105: labeled `workflow:triage`

- Two PRs started simultaneously:
  - PR-A: Research workflow processing issue #101
  - PR-B: Implementation workflow processing issue #103

## Steps to Follow

1. **PR-A (Research Workflow)**
   - Query issues with `workflow:research` label
   - Find issues #101, #102
   - Process issue #101
   - Complete research
   - Change label #101: `workflow:research` → `workflow:product-backlog`
   - Add comment to #101
   - Commit and push PR-A

2. **PR-B (Implementation Workflow) - Concurrent**
   - Query issues with `workflow:implementation` label
   - Find issues #103, #104
   - Process issue #103
   - Complete implementation
   - Close issue #103
   - Commit and push PR-B

3. **Merge Both PRs**
   - PR-A merges first
   - PR-B merges second
   - Check: Any conflicts?
   - Check: State consistency?

## Expected Outcome

✅ No merge conflicts (different issues, label changes via GitHub API)
✅ Issue #101 now labeled `workflow:product-backlog`
✅ Issue #103 closed
✅ Issues #102, #104, #105 unchanged
✅ Both PRs merge successfully

## Success Criteria

- [ ] No merge conflicts
- [ ] Each PR only affected its designated issues
- [ ] Final state is consistent
- [ ] No data loss or corruption
- [ ] Audit trail preserved (comments, label history)

## Test Result

**Status**: PASS (concurrent PRs are safe with label-based approach)

**Tabletop Simulation Notes**:

Simulated concurrent PR scenario:

**Setup:**
- Main branch: 5 issues with various labels
- PR-A: Will process issue #101 (research workflow)
- PR-B: Will process issue #103 (implementation workflow)

**PR-A Timeline:**
1. Branch created from main
2. Workflow script queries: `gh issue list --label "workflow:research"`
3. Finds issues #101, #102
4. Processes #101 (makes code/doc changes in PR-A branch)
5. Completes work
6. Changes label #101 via GitHub API: `workflow:research` → `workflow:product-backlog`
7. Adds comment to #101
8. Commits code changes, pushes PR-A

**PR-B Timeline (Concurrent):**
1. Branch created from main (around same time as PR-A)
2. Workflow script queries: `gh issue list --label "workflow:implementation"`
3. Finds issues #103, #104
4. Processes #103 (makes code/doc changes in PR-B branch)
5. Completes work
6. Closes issue #103 via GitHub API
7. Commits code changes, pushes PR-B

**Merge Behavior:**
1. PR-A merges: Code changes merge, label changes already applied to #101
2. PR-B merges: Code changes merge, issue #103 already closed
3. **No merge conflicts** because:
   - Label changes are via GitHub API (not in git)
   - Code changes are in different areas (research vs implementation)
   - Each PR touched different issues

**Concurrency Analysis:**
- ✅ Label changes happen via GitHub API (centralized, not in git repo)
- ✅ Each PR modified different issues (#101 vs #103)
- ✅ No race condition on labels (GitHub handles concurrent API calls)
- ✅ Code changes don't conflict (different workflow areas)
- ✅ Final state is consistent

**Edge Case: Same Issue Different PRs**
If PR-A and PR-B both tried to change label on same issue:
- GitHub API: Last write wins (atomic operation)
- Not ideal, but rare (workflows query, then process specific items)
- Can be mitigated by workflow design (process items from queue atomically)

**Conclusion**: Concurrent PRs processing different issues are completely safe. Label-based approach avoids file merge conflicts by using GitHub API as centralized state store.
