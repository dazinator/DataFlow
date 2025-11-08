# Scenario: Improved - Research Team Creating Product Backlog Item with Guidance

## Context

A research team has completed a prototype of a new testing approach. They need to create a product backlog item with the IMPROVED template that includes documentation deliverables checklist and example tests guidance.

The research includes:
- Test helper utilities (prototype code)
- A comprehensive testing guide
- Example tests demonstrating the approach
- Performance benchmarks

## Starting Point

Research team has:
- Completed prototype code
- Completed research documentation
- Ready to create backlog item using IMPROVED template
- Template now has explicit documentation and example tests guidance

## Steps to Follow

1. Read IMPROVED `/product/backlog-item-template.md`
2. See "Documentation Deliverables (if applicable)" section
3. See "Example Tests Guidance (if applicable)" section
4. Create backlog item with specific deliverables
5. Fill in success criteria with concrete expectations

## Expected Outcome

Research team creates backlog item with clear specifications:
- Documents WHAT documentation is needed (usage guide, patterns guide, etc.)
- Specifies HOW MANY example tests (3-5 showing key patterns)
- Indicates SCOPE (comprehensive >10KB guide)
- Specifies TARGET AUDIENCE (contributors)

## Success Criteria

- [x] Template provides documentation deliverables checklist
- [x] Template provides example tests quantity guidance
- [x] Template includes scope guidance
- [x] Template includes target audience specification
- [x] Research team has no uncertainty about "how much is enough"

## Test Result

**Status**: PASS (with improved template) ✅

**Notes**:
IMPROVED template includes new sections:

**Documentation Deliverables (if applicable)**
- [ ] Usage guide (comprehensive >10KB or quick start <3KB)
  - Target audience: [End users / Contributors / Both]
- [ ] Pattern/best practices guide
- [ ] README for new directories/modules
- [ ] Update relevant index/navigation files (e.g., `/poc/docs/INDEX.md`)

**Example Tests Guidance (if applicable)**
- Minimum: 3-5 example/demo tests showing key usage patterns
- Focus on common patterns rather than exhaustive coverage
- Include "before/after comparison" tests if demonstrating improvements
- Example: `TestHelpersDemoTests.cs` showing OLD vs NEW patterns

**Benefits of Improved Template**:
1. ✅ Research team knows exactly what to include in handover
2. ✅ Clear scope guidance (comprehensive vs quick start)
3. ✅ Specific example test quantity (3-5 tests)
4. ✅ Target audience specified upfront
5. ✅ Navigation file updates explicitly called out

**Comparison to Baseline**:
- Baseline: Generic "Test scenarios" in Handover Assets
- Improved: "3-5 example tests showing key usage patterns, include before/after comparison"

**Result**: Research team creates backlog item with:
```markdown
## Success Criteria
- [ ] Test helper utilities implemented
- [ ] Usage guide created (comprehensive >10KB, target: contributors)
- [ ] Pattern guide created (best practices, target: contributors)
- [ ] 3-5 example tests demonstrating key patterns (before/after comparison)
- [ ] `/poc/docs/INDEX.md` updated with new guide
- [ ] Tests passing
```

No ambiguity, no follow-up questions needed from implementation team.
