# Ongoing Initiatives

This directory tracks ongoing initiatives that should be considered when implementing new features or improvements.

## Purpose

Ongoing initiatives are patterns or improvements that should be incrementally applied across the codebase as new work is done. Rather than doing large-scale refactoring, these initiatives are opportunistically applied when working on related code.

## Examples

- **Test Helper Adoption**: Use new test helpers when writing or refactoring tests
- **Documentation Patterns**: Apply consistent documentation structure
- **Code Quality Improvements**: Apply coding standards incrementally
- **Performance Optimizations**: Apply proven optimization patterns

## How to Use

### For Implementation Team

When starting a new implementation:

1. **After creating your implementation plan**, check `.github/initiatives/active/` for active initiatives
2. **Review relevant initiatives** that relate to your work area
3. **Add initiative tasks** to the end of your implementation plan (after all primary phases)
4. **Implement initiative tasks** if time permits and they're low-risk additions
5. **Document what was done** - update the initiative file with progress

### When to Apply Initiatives

**✅ Good times to apply:**
- After completing primary implementation work
- When touching related code that fits the initiative
- When the initiative is low-risk and well-documented
- When it adds value without complicating the PR

**❌ Avoid applying when:**
- Primary implementation is complex or high-risk
- Initiative would significantly increase PR scope
- You're unfamiliar with the initiative pattern
- Deadline pressure doesn't allow for the extra work

## Initiative Lifecycle

### Active Initiatives

Location: `.github/initiatives/active/`

These are initiatives that should be considered for new work. Each has:
- Clear pattern to follow
- Examples of good application
- Success metrics
- Responsible party (if applicable)

### Archived Initiatives

Location: `.github/initiatives/archive/`

Completed or abandoned initiatives are moved here for historical reference.

## Initiative Template

When creating a new initiative, use this template:

```markdown
# Initiative: [Name]

**Status**: Active  
**Started**: YYYY-MM-DD  
**Owner**: [Person/Team if applicable]

## Objective

[Brief description of what this initiative aims to achieve]

## Pattern to Follow

[Clear guidance on how to apply this initiative]

### Example

[Concrete before/after example]

## When to Apply

- [Situation 1]
- [Situation 2]

## When NOT to Apply

- [Situation where it doesn't make sense]

## Success Metrics

- [How to measure progress]
- [Current status: X/Y files completed]

## References

- [Links to documentation, ADRs, examples]

## Progress Log

### YYYY-MM-DD - PR #[number]
- [What was done]
- [Files affected]
```

## Creating a New Initiative

1. Create file in `.github/initiatives/active/[name].md`
2. Use the template above
3. Provide clear examples and guidance
4. Link to relevant documentation
5. Add to the Implementation Workflow as a consideration point

## Archiving an Initiative

When an initiative is complete or no longer relevant:

1. Update status to "Complete" or "Abandoned"
2. Add completion summary
3. Move to `.github/initiatives/archive/YYYY-MM-DD-[name].md`
4. Update any workflow documentation that referenced it
