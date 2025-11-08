# Scenario 002: Version Selection - Baseline (No Guidance)

## Context
Testing current workflow guidance when implementing a security vulnerability fix where handover specifies minimum patched version but doesn't mention latest version.

## Starting Point
- Copilot agent receives handover for CVE fix
- Handover says "Update package X to 1.10.1 (patches CVE-2024-XXXX)"
- Latest version is actually 1.12.0
- Agent follows Implementation Workflow

## Steps to Follow (Current Workflow)
1. Read handover - specifies version 1.10.1
2. Update package to 1.10.1 as specified
3. **QUESTION**: Should I check for latest version?
4. **NO GUIDANCE** in workflow on whether to:
   - Use minimum patched version (1.10.1)
   - Use latest stable version (1.12.0)
   - Check what's available at all

## Expected Outcome (Baseline)
**Inconsistent approach:**
- Some agents might use exact version specified (1.10.1)
- Others might independently check for latest (1.12.0)
- No standardized approach
- Potential to miss additional security patches in later versions
- Handover authors unsure whether to specify minimum or latest

## Success Criteria
- [ ] Workflow has no guidance on version selection ❌
- [ ] No mention of checking for latest versions ❌
- [ ] No guidance on minimum patched vs latest stable ❌
- [ ] Inconsistent outcomes likely ❌

## Test Result
**Status**: BASELINE (showing current gap)

**Notes**:
Current workflow doesn't address version selection strategy for dependency updates. This creates:
- **Risk**: Using minimum patched version might miss subsequent security fixes
- **Inconsistency**: Different agents make different choices
- **Handover confusion**: Research teams unsure what version to specify

For security fixes especially, using latest stable version (not just minimum patched) ensures:
1. All security patches included
2. Bug fixes included
3. Reduced need for near-term updates

This validates the need for "Version Selection Guidance" in both workflow and handover creation.
