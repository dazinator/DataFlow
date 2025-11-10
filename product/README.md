# Product Backlog System

The `/product` folder contains the centralized backlog system for managing work items across all team workflows. This system separates **backlog registration** (what work exists) from **prioritization** (what work to do next).

## Overview

The product backlog serves as:
- **Single source of truth** for all work items ready for implementation
- **Prioritization hub** managed by the product team
- **Integration point** for all team workflows (Research, Tech Debt, etc.)
- **Handover mechanism** from discovery/research teams to implementation team

## Folder Structure

```
/product/
├── README.md                    # This file - system overview and usage guide
├── backlog/                     # Active backlog items
│   ├── [backlog-item-id].md    # Individual backlog item files
│   ├── [backlog-item-id]/      # Optional handover folder (same name as item)
│   │   ├── prototype/          # Prototype code
│   │   ├── design/             # Design documents
│   │   └── benchmarks/         # Performance data
│   └── ...
├── prioritization.md           # Current prioritization (managed by product team)
└── resolved/                   # Completed backlog items (archived)
    ├── YYYY-MM/                # Monthly archives
    │   ├── [backlog-item-id].md
    │   └── [backlog-item-id]/  # Associated handover folders
    └── ...
```

## Backlog Item Format

Each backlog item is an **independent markdown file** with a unique ID-based filename.

### File Naming Convention

Format: `[source]-[date]-[short-kebab-case-name].md`

Examples:
- `research-2025-11-08-flow-composability.md`
- `techdebt-2025-11-07-modernize-namespaces.md`
- `adhoc-2025-11-10-add-logging-helpers.md`

**Source Prefixes:**
- `research-` - From Research Workflow handovers
- `techdebt-` - From Tech Debt Workflow discoveries
- `adhoc-` - Direct product team additions or other sources

The filename serves as the **backlog item ID** for referencing in workflows and issues.

### Backlog Item Structure

Each backlog item file contains:

```markdown
# [Title]

**Backlog ID**: [filename without .md]
**Source**: [Research/Tech Debt/Ad-hoc]
**Category**: [Feature/Bug Fix/Tech Debt/Performance/etc.]
**Status**: Active / In Progress / Completed
**Created**: YYYY-MM-DD
**Updated**: YYYY-MM-DD

## Summary

[Brief description of what needs to be implemented]

## Context

[Background and rationale - why this work is needed]

## Implementation Guidance

[High-level guidance for implementation team]
- Key requirements
- Constraints or considerations
- Suggested approach (if any)

## Success Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

## Handover Assets

[If handover folder exists]
- **Location**: `/product/backlog/[backlog-item-id]/`
- **Contents**: [List what's in the handover folder]
  - Prototype code
  - Design documents
  - Benchmarks
  - etc.

[If no handover folder]
- No additional assets

## References

- Source handover/analysis: [path to research folder or tech debt analysis]
- Related issues: #[issue numbers]
- Related PRs: #[PR numbers]

## Notes

[Any additional context or considerations]
```

### Handover Folders

If a backlog item needs supporting assets (prototype code, design docs, benchmarks), create a folder with the same name as the backlog item file (minus `.md` extension):

```
/product/backlog/
├── research-2025-11-08-flow-composability.md
└── research-2025-11-08-flow-composability/
    ├── prototype/
    │   └── UnifiedFlowBuilder.cs
    ├── design/
    │   └── architecture-diagram.md
    └── benchmarks/
        └── composability-perf.md
```

**When to use handover folders:**
- Research handovers with prototype code
- Design documents or ADRs specific to the work item
- Performance benchmarks or data
- Test scenarios or examples

**When NOT to use handover folders:**
- Simple tech debt items that are self-explanatory
- Bug fixes with clear reproduction steps
- Items that only need description in the markdown file

## Prioritization System

The product team maintains prioritization separately in `/product/prioritization.md`.

### Prioritization File Format

```markdown
# Product Backlog Prioritization

**Last Updated**: YYYY-MM-DD
**Updated By**: [Name/Team]

## Active Priorities (Max 5)

These items are approved for immediate implementation. Implementation team should select from this list.

| Priority | Backlog Item ID | Title | Rationale |
|----------|----------------|-------|-----------|
| 1 (Highest) | research-2025-11-08-flow-composability | Flow Composability Unification | Critical for v2.0 API |
| 2 | techdebt-2025-11-07-modernize-namespaces | Modernize Namespace Declarations | Blocking other cleanups |
| 3 (Normal) | research-2025-11-05-performance-optimization | Channel Performance Optimization | User-reported perf issue |

## Notes

[Any context about prioritization decisions]
```

**Priority Levels:**
- **1** = Highest priority (critical, blocking, time-sensitive)
- **2** = High priority (important, significant value)
- **3** = Normal priority (default for most work)
- **4** = Lower priority (nice-to-have)
- **5** = Lowest priority (defer unless capacity allows)

**Max 5 items**: Keeping the active priority list short ensures focus and clarity.

## Using the Backlog

### For Product Team

**Adding Items to Backlog:**
1. Create backlog item file following naming convention
2. Fill in all sections of the template
3. Create handover folder if needed
4. Item is now in backlog but not prioritized

**Prioritizing Items:**
1. Review backlog items in `/product/backlog/`
2. Update `/product/prioritization.md`
3. Select up to 5 items for active priorities
4. Assign priority levels (1-5)
5. Add rationale for prioritization decisions

**Archiving Completed Items:**
1. When implementation team completes work, they update item status
2. Move completed item to `/product/resolved/YYYY-MM/`
3. Move associated handover folder (if exists)
4. Remove from prioritization list

### For Research Team

When completing research work:

1. **Create backlog item** in `/product/backlog/`
   - Use `research-YYYY-MM-DD-[name].md` naming
   - Fill in template with research findings
2. **Create handover folder** if you have assets
   - Copy prototype code to `/product/backlog/[item-id]/prototype/`
   - Copy design docs to `/product/backlog/[item-id]/design/`
   - Copy benchmarks to `/product/backlog/[item-id]/benchmarks/`
3. **Reference in research folder**
   - Link backlog item from your research handover
4. **Notify product team** (via GitHub issue or comment)

See `.team/prompts/RESEARCH_WORKFLOW.md` for integration details.

### For Tech Debt Team

When conducting tech debt analysis:

1. **Check existing backlog first**
   ```bash
   grep -r "keyword" product/backlog/*.md
   ```
2. **For NEW findings:**
   - Create backlog item using `techdebt-YYYY-MM-DD-[name].md`
   - Fill in template with finding details
   - Create handover folder if needed (e.g., for prototype fixes)
3. **For EXISTING items:**
   - Update the existing backlog item if needed
   - Add note about re-validation
   - Update priority recommendation if changed

See `.team/prompts/TECH_DEBT_WORKFLOW.md` for integration details.

### For Implementation Team

When starting implementation work:

1. **Check prioritization file**: `/product/prioritization.md`
2. **Select highest priority item** (or specific item if assigned)
3. **Read backlog item file** completely
4. **Review handover assets** (if folder exists)
5. **Update item status** to "In Progress"
6. **Reference backlog item** in implementation PR

**When work is complete:**
1. Update backlog item status to "Completed"
2. Add PR link to backlog item
3. Archive to `/product/resolved/YYYY-MM/`
4. Implementation team or product team removes from prioritization

See `.team/prompts/IMPLEMENTATION_WORKFLOW.md` for integration details.

## Searching the Backlog

### Finding Items

```bash
# List all active backlog items
ls product/backlog/*.md

# List by source
ls product/backlog/research-*.md
ls product/backlog/techdebt-*.md

# Search by keyword
grep -l "performance" product/backlog/*.md

# Search by category
grep "Category: Tech Debt" product/backlog/*.md

# Find items with handover folders
for f in product/backlog/*.md; do
  basename="${f%.md}"
  if [ -d "$basename" ]; then
    echo "$f has handover folder"
  fi
done

# Count active items
ls product/backlog/*.md | wc -l

# List recent items (last 7 days)
find product/backlog -name "*.md" -mtime -7
```

### Finding Prioritized Items

```bash
# View current priorities
cat product/prioritization.md

# Find highest priority item
grep "1 (Highest)" product/prioritization.md
```

### Finding Completed Items

```bash
# List all resolved items
ls product/resolved/*/*.md

# Find specific resolved item
find product/resolved -name "research-2025-11-*"

# Count resolved items this month
ls product/resolved/2025-11/*.md | wc -l
```

## Backlog Maintenance

### Periodic Review (Recommended: Monthly)

1. **Review active items** - Are descriptions still accurate?
2. **Update priorities** - Have circumstances changed?
3. **Archive completed items** - Move to resolved folder
4. **Remove obsolete items** - Archive or delete if no longer relevant
5. **Combine related items** - Consolidate if appropriate

### Archive Structure

Resolved items are archived monthly:

```
/product/resolved/
├── 2025-11/
│   ├── research-2025-11-08-flow-composability.md
│   ├── research-2025-11-08-flow-composability/
│   └── techdebt-2025-11-07-add-cancellation-attrs.md
├── 2025-12/
└── 2026-01/
```

## Migration from Old System

### Tech Debt Backlog Migration

Existing tech debt backlog items in `/research/backlog/` should be migrated:

1. For each item in `/research/backlog/*.md`:
   - Create new file in `/product/backlog/` with `techdebt-` prefix
   - Copy content and update format to match new template
   - Update references if needed
2. After migration, add note to `/research/backlog/README.md` pointing to new location
3. Optionally archive old backlog files

## Integration with GitHub Issues

### Implementation Issues

When creating implementation GitHub issues:

**Option 1: Specific backlog item**
```markdown
**Backlog Item**: `research-2025-11-08-flow-composability`
**Backlog Path**: `/product/backlog/research-2025-11-08-flow-composability.md`
```

**Option 2: Next highest priority**
```markdown
**Backlog Item**: Next from prioritization list
**Implementation team**: Please check `/product/prioritization.md` and comment with selected item
```

See `.github/ISSUE_TEMPLATE/implementation.md` for updated template.

## Best Practices

### For All Teams

1. **Keep items atomic** - One clear work item per file
2. **Be specific** - Clear descriptions, measurable success criteria
3. **Link to sources** - Always reference original research/analysis
4. **Update promptly** - Keep status current as work progresses
5. **Clean up regularly** - Archive completed items, remove obsolete ones

### For Product Team

1. **Review backlog weekly** - Stay aware of new items
2. **Prioritize thoughtfully** - Consider value, urgency, dependencies
3. **Limit priorities** - Max 5 items keeps focus clear
4. **Document decisions** - Add rationale for prioritization
5. **Communicate changes** - Notify teams when priorities shift

### For Implementation Team

1. **Check prioritization first** - Don't guess what to work on
2. **Read completely** - Review backlog item AND handover assets
3. **Update status** - Mark "In Progress" when starting
4. **Complete the cycle** - Update status and archive when done
5. **Ask questions early** - If backlog item is unclear, ask before starting

## FAQ

**Q: What if a backlog item doesn't need a handover folder?**
A: That's fine! Many items (especially simple tech debt) only need the markdown description. Only create handover folders when you have assets to share.

**Q: Can I update an existing backlog item?**
A: Yes! If you discover new information or the requirements change, update the markdown file and note the change in the "Updated" field and "Notes" section.

**Q: What if I'm implementing something that's not in the backlog?**
A: For urgent bugs or very small changes, you might skip the backlog. But for any significant work, create a backlog item first (use `adhoc-` prefix). This ensures visibility and proper handover.

**Q: How do I know if my research should create a backlog item?**
A: If your research is handing over work for implementation (not merging code directly), create a backlog item. If you're merging code directly as part of research, you don't need a backlog item.

**Q: What happens to backlog items that are never prioritized?**
A: That's OK! The backlog can contain items that might be done eventually. Product team periodically reviews and may archive items that are no longer relevant. Not everything in the backlog needs to be implemented.

---

**Note**: This backlog system replaces the previous `/research/backlog/` tech debt-specific backlog. All workflows now integrate with this unified product backlog.
