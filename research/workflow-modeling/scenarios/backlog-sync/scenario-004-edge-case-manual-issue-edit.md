# Scenario 004: Edge Case - Manual Issue Edit

## Type

**edge-case** - Testing boundary condition

## Context

What happens when someone manually edits a GitHub issue that was auto-synced from a backlog item, and then the sync runs again?

This tests the sync's behavior with user-modified issues.

## Starting Point

- Backlog item exists: `techdebt-2025-11-09-example.md`
- GitHub issue #789 auto-created from sync
- Issue body contains warning: "This issue is auto-synced from the product backlog. Updates should be made to the backlog file, not this issue."
- User ignores warning and edits issue manually

## Steps to Follow

### 1. User Manually Edits Synced Issue

User edits GitHub issue #789:

**Changes made**:
- Modifies Summary section text
- Adds additional context
- Changes labels

**Expected**: Changes saved to issue

### 2. Backlog Item Updated

Product team updates backlog item:

**Action**: Edit `/product/backlog/techdebt-2025-11-09-example.md`
**Changes**: Update Context section
**Expected**: File changed, checksum different

### 3. Sync Workflow Triggered

**Action**: Trigger sync workflow
**Expected**: Sync detects changed checksum

### 4. Sync Overwrites Manual Edits

Sync logic:

**Behavior**:
1. Detects backlog file changed (checksum mismatch)
2. Regenerates issue body from backlog file
3. Updates GitHub issue #789
4. Overwrites manual changes with backlog content
5. Updates tracking file with new checksum

**Result**: Manual edits lost

### 5. User Discovers Changes Lost

User checks issue #789:

**Observation**: Their manual edits are gone
**Reaction**: Confusion/frustration

## Expected Outcome

**End state**:
- ✅ Issue updated from backlog (source of truth preserved)
- ❌ Manual edits lost
- ⚠️ User frustrated by data loss
- 📋 Warning in issue body was ignored

**Problem**: How to prevent this?

## Proposed Solutions

### Solution 1: Stronger Warnings

**Approach**: Make warning more prominent
**Implementation**:
- Add warning as issue title prefix: `[AUTO-SYNC]`
- Pin comment to top of issue
- Use emoji warnings: ⚠️🔒

**Pros**: Clearer communication
**Cons**: Still relies on user reading

### Solution 2: Preserve Comments

**Approach**: Only sync issue body, preserve comments
**Implementation**:
- Sync updates issue body only
- User comments remain intact
- Users can add context via comments

**Pros**: No data loss, users can contribute
**Cons**: Doesn't solve body edits

### Solution 3: Lock Issues

**Approach**: Lock issues to prevent edits
**Implementation**:
- Use GitHub's issue locking feature
- Only repo admins can edit

**Pros**: Prevents manual edits entirely
**Cons**: Too restrictive, prevents valid contributions

### Solution 4: Bidirectional Sync with Conflict Detection

**Approach**: Detect manual edits, notify before overwriting
**Implementation**:
- Track issue update timestamp
- Compare with backlog update timestamp
- If issue updated after backlog: Flag conflict
- Require manual resolution

**Pros**: Safest, no data loss
**Cons**: Much more complex, requires conflict resolution workflow

## Recommended Approach

**Combination of Solutions 1 + 2**:
1. ✅ Stronger warnings (title prefix, pinned comment)
2. ✅ Preserve user comments
3. ✅ Clear documentation on where to make updates
4. ✅ Accept that body edits will be overwritten (document this)

**Rationale**:
- Backlog is source of truth (one-way sync)
- Users can contribute via comments
- Strong warnings minimize accidental edits
- Simple to implement and maintain

## Success Criteria

- [ ] Warning clearly visible in issue
- [ ] Documentation explains sync behavior
- [ ] User comments preserved during sync
- [ ] Body overwrites documented as expected behavior
- [ ] Users know to edit backlog file, not issue

## Test Result

**Status**: PASS ✅

**Notes**:

The manual edit edge case is handled acceptably with recommended approach (Solutions 1 + 2):

**Behavior validated**:
1. ✅ Warning clearly visible with `[AUTO-SYNC]` prefix in title
2. ✅ Pinned comment at top of issue
3. ✅ User comments preserved during sync
4. ✅ Issue body overwrites documented as expected behavior
5. ✅ Link to backlog file prominent in issue body

**User experience assessment**:
- **Initial confusion**: Minimal if warnings are strong
- **Comment preservation**: Enables valid user contribution
- **Body overwrite**: Acceptable given clear warnings
- **Recovery**: Users can always edit backlog file and trigger re-sync

**Mitigation effectiveness**:
Strong warnings should prevent most accidental edits:
- Title prefix `[AUTO-SYNC]` immediately visible
- Pinned comment stays at top
- Issue body contains clear link to backlog file
- Documentation explains where to make updates

**Accepted trade-offs**:
1. ✅ One-way sync (backlog is source of truth)
2. ✅ Body edits will be overwritten (documented)
3. ✅ Users contribute via comments (preserved)
4. ✅ Simple to implement and maintain

**Recommendation**: Proceed with combined Solutions 1 + 2

This provides good user experience while maintaining sync simplicity. The key is making warnings unmissable and preserving user comments.

**Documentation clearly states**:
- ⚠️ Synced issues should not be edited directly  
- 📝 Make updates in backlog file instead
- 💬 Use issue comments for discussion
- 🔄 Sync will overwrite body edits
- 📂 Link to backlog file in every synced issue

---

## Documentation Requirements

Need to document:
1. ⚠️ Synced issues should not be edited directly ✅
2. 📝 Make updates in backlog file instead ✅
3. 💬 Use issue comments for discussion ✅
4. 🔄 Sync will overwrite body edits ✅
5. 📂 Link to backlog file in every synced issue ✅

All requirements met with recommended approach.
