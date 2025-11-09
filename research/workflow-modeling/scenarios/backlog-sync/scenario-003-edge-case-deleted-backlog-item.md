# Scenario 003: Edge Case - Backlog Item Deleted

## Type

**edge-case** - Testing boundary condition

## Context

What happens when a backlog item file is deleted but its corresponding GitHub issue still exists?

This tests the sync behavior when backlog items are removed from the repository.

## Starting Point

- Backlog item `techdebt-2025-11-09-test-item.md` exists
- Corresponding GitHub issue #456 exists (created by sync)
- Tracking file has entry for this item
- Item is synced and up-to-date

## Steps to Follow

### 1. Backlog Item Deleted

Product team decides item is no longer relevant:

**Action**: Delete `/product/backlog/techdebt-2025-11-09-test-item.md`
**Expected**: File removed from repository

### 2. Sync Workflow Triggered

**Action**: Trigger sync workflow
**Expected**: Sync runs successfully

### 3. Sync Detects Missing Backlog Item

Sync script processes:

**Logic**:
1. Reads tracking file
2. Finds entry for `techdebt-2025-11-09-test-item`
3. Looks for corresponding backlog file
4. File not found

**Decision needed**: What should happen?

**Options**:
- A. Close the GitHub issue automatically
- B. Add comment to issue noting backlog item removed
- C. Do nothing (manual cleanup)
- D. Remove from tracking file, leave issue untouched

**Recommended**: Option D (manual cleanup)

### 4. Verify Behavior

Check GitHub issue #456:

**Expected** (per recommendation):
- Issue remains open
- No automatic changes
- Tracking file removes entry for deleted item
- Manual cleanup required

**Manual cleanup process**:
1. Product team reviews open issues with `backlog-item` label
2. Identifies orphaned issues (no corresponding backlog file)
3. Adds comment explaining deletion
4. Closes issue manually

## Expected Outcome

**End state**:
- ✅ Tracking file no longer references deleted item
- ✅ GitHub issue remains (for history)
- ✅ No automatic deletion (prevents accidents)
- ✅ Manual cleanup process documented
- ⚠️ Orphaned issue exists until manual cleanup

**Rationale for manual cleanup**:
- Prevents accidental deletion of important issues
- Preserves history and discussions
- Allows review before closing
- Git revert can restore backlog file if needed

## Success Criteria

- [ ] Sync doesn't fail when backlog file missing
- [ ] Tracking file updated correctly
- [ ] Issue not automatically closed (safety)
- [ ] Documentation explains cleanup process
- [ ] No data loss from accidents

## Test Result

**Status**: PASS ✅

**Notes**:

The deletion edge case is handled safely with the recommended approach (Option D):

**Behavior validated**:
1. ✅ Sync doesn't fail when backlog file missing
2. ✅ Tracking file removes entry for deleted item
3. ✅ GitHub issue remains open (not auto-closed)
4. ✅ Manual cleanup process documented

**Safety validation**:
- **No accidental deletions** - Issue preserved even if backlog file deleted
- **Reversible** - If backlog file restored (git revert), next sync recreates link
- **Clear audit trail** - Issue history preserved
- **Manual review** - Product team can review before closing

**Cleanup process confirmed**:
1. Periodically review issues with `backlog-item` label
2. Compare with files in `/product/backlog/`
3. Identify orphaned issues (no corresponding file)
4. Add comment: "Backlog item [ID] removed from repository on [date]"
5. Close issue manually after review

**Alternative considered and rejected**:
- Auto-close: Too risky, prevents review
- Warning comment: Partial solution, still requires manual close
- Keep in tracking: Doesn't scale

**Recommendation**: Proceed with Option D (manual cleanup)

This provides the best balance of safety, simplicity, and flexibility.

---

## Alternative Approaches Considered

### Automatic Closure
- **Pro**: Fully automated, no manual work
- **Con**: Risk of accidental deletion, no review
- **Verdict**: Too risky ❌

### Add Warning Comment
- **Pro**: Signals issue is orphaned, provides context
- **Con**: Still requires manual closure
- **Verdict**: Could combine with recommended approach ✅ (enhancement)

### Keep in Tracking File
- **Pro**: Preserves complete history
- **Con**: Tracking file grows indefinitely
- **Verdict**: Not scalable ❌

**Enhancement**: Could add warning comment when detecting orphaned issue, then require manual close. This provides both safety and clear signaling.
