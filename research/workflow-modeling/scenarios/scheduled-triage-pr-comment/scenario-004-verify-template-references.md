# Scenario 004: Verify Template References

## Context

Testing that the process modeling issue template has been updated to reference correct paths and documentation after the workflow migration to the duties system.

## Starting Point

- Updated `.github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md`
- Process modeling duty exists: `.team/duties/PROCESS_MODELING_DUTY.md`
- Document hygiene guide exists: `docs/DOCUMENT_HYGIENE.md`
- Test scenario locations: `.team/duties/tests/`, `.team/procedures/tests/`, `.team/kernel/tests/`

## Steps to Follow

### 1. Check Template Header References

Read `.github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md`:

Line 13 should read:
```
@copilot **MUST** follow the Process Modeling duty in `.team/duties/PROCESS_MODELING_DUTY.md`.
```

**Expected**: ✅ References `.team/duties/PROCESS_MODELING_DUTY.md` (NOT old `.team/prompts/PROCESS_MODELING_WORKFLOW.md`)

### 2. Check "For @copilot" Section References

Lines 68-74 should read:
```markdown
## For @copilot

**Duty**: Follow `.team/duties/PROCESS_MODELING_DUTY.md` for complete process.

**Quick Reference:**
- Read `docs/DOCUMENT_HYGIENE.md` before creating/updating documentation
- Create test scenarios in `.team/duties/tests/`, `.team/procedures/tests/`, or `.team/kernel/tests/`
- Execute tabletop simulations to validate changes
- Complete self-improvement evaluation before PR review
```

**Expected**: 
- ✅ "Duty" (not "Workflow")
- ✅ References `.team/duties/PROCESS_MODELING_DUTY.md`
- ✅ Document hygiene path: `docs/DOCUMENT_HYGIENE.md` (simple path, not broken link)
- ✅ Test scenario locations: `.team/duties/tests/`, `.team/procedures/tests/`, `.team/kernel/tests/`
- ✅ No obsolete "Documentation Convention" section

### 3. Verify File References Exist

Check that referenced files actually exist:

```bash
# Process Modeling duty
test -f .team/duties/PROCESS_MODELING_DUTY.md && echo "✅ PASS" || echo "❌ FAIL"

# Document hygiene
test -f docs/DOCUMENT_HYGIENE.md && echo "✅ PASS" || echo "❌ FAIL"

# Test directories
test -d .team/duties/tests && echo "✅ PASS" || echo "❌ FAIL"
test -d .team/procedures/tests && echo "✅ PASS" || echo "❌ FAIL"
test -d .team/kernel/tests && echo "✅ PASS" || echo "❌ FAIL"
```

**Expected**: All checks return ✅ PASS

### 4. Verify Old References Removed

Check that template does NOT reference:

```bash
# Should NOT find these old references
grep -q ".team/prompts/PROCESS_MODELING_WORKFLOW.md" .github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md && echo "❌ FAIL - Old ref found" || echo "✅ PASS"

grep -q "/research/workflow-modeling/scenarios" .github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md && echo "❌ FAIL - Old ref found" || echo "✅ PASS"

grep -q "Documentation Convention:" .github/ISSUE_TEMPLATE/workflow-improvement-suggestion.md && echo "❌ FAIL - Obsolete section found" || echo "✅ PASS"
```

**Expected**: All checks return ✅ PASS (no old references)

### 5. Test Template Creates Valid Issue

Simulate creating an issue with the template:
1. User fills out template
2. Issue created with `workflow:process-modeling` label
3. @copilot mentioned and given correct duty reference
4. All file paths in issue are valid and accessible

**Expected**: 
- Template renders correctly
- All referenced paths are accessible
- No broken links or 404s
- Copilot receives clear, correct instructions

## Expected Outcome

**Result**: Template is fully updated and functional

**Success Criteria**:
- [x] Header references `.team/duties/PROCESS_MODELING_DUTY.md`
- [x] "For @copilot" section references correct duty
- [x] Document hygiene path is correct and simple
- [x] Test scenario paths updated to new locations
- [x] Old `.team/prompts/` references removed
- [x] Old scenario path removed
- [x] Obsolete documentation convention removed
- [x] All referenced files exist
- [x] Template renders correctly in GitHub UI

## Actual Outcome

**Status**: PASS ✅

**Notes**:
- All references updated from old `.team/prompts/` to new `.team/duties/`
- Document hygiene path simplified from broken markdown link to simple path
- Test scenario guidance updated to reflect actual test locations
- Obsolete "Documentation Convention" section removed
- Template is cleaner and more maintainable
- All file references verified to exist

## Observations

This scenario validates documentation hygiene:
- Template kept in sync with repository structure changes
- No broken references that would confuse users or agents
- Clear, accurate guidance for process modeling work
- Simpler template is easier to maintain
- Copilot receives correct instructions when issues created
