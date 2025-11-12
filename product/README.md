# Product Backlog System

The `/product` folder contains the centralized backlog system for managing work items across all team workflows. This system separates **backlog registration** (what work exists) from **prioritization** (what work to do next).

## ⚠️ IMPORTANT: Backlog Source Priority

**Primary and Authoritative Source**: **GitHub issues with the `workflow:product-backlog` label**

The product backlog is managed through GitHub issues. 

**GitHub Issues** are the **single source of truth** for backlog items:
- All backlog items exist as GitHub issues with `workflow:product-backlog` label
- Allows querying, filtering, and automated workflows
- Enables workflow transitions and tracking

**Backlog Items as GitHub Issues:**

All backlog items are tracked as GitHub issues with the `workflow:product-backlog` label. Each issue contains:
- Title and description
- Metadata (source, category, effort estimate)
- Labels and workflow state
- Links to any supporting research or handover assets (if applicable)

## Overview

The product backlog serves as:
- **Single source of truth** for all work items ready for implementation (via GitHub issues)
- **Prioritization hub** managed by the product team
- **Integration point** for all team workflows (Research, Tech Debt, etc.)

## Folder Structure

```
/product/
├── README.md                    # This file - system overview and usage guide
└── prioritization.md            # Current prioritization decisions (updated by Product Prioritization workflow)
```
├── prioritization.md           # Current prioritization (managed by product team)
└── resolved/                   # Completed backlog items (archived)
    ├── YYYY-MM/                # Monthly archives
    │   ├── [backlog-item-id].md
    │   └── [backlog-item-id]/  # Associated handover folders
    └── ...
```

## Backlog Item Format

## Prioritization System

The product team maintains prioritization in `/product/prioritization.md`, which references **GitHub issue numbers**.

**Prioritization Process:**
1. Query all GitHub issues with `workflow:product-backlog` label
2. Apply prioritization policy (security → tech debt → overrides → standard criteria)
3. Check implementation queue capacity
4. Select top items and move to `workflow:implementation` queue
5. Update `/product/prioritization.md` with results

See `.team/duties/PRODUCT_PRIORITIZATION_DUTY.md` for complete duty documentation.

### Prioritization File Format

```markdown
# Product Backlog Prioritization

**Last Updated**: YYYY-MM-DD
**Updated By**: [Name/Team]

## Active Priorities (Max 5)

These items are approved for immediate implementation. Implementation team should select from this list.

| Priority | Backlog Item (Issue #) | Title | Category | Rationale |
|----------|----------------------|-------|----------|-----------|
| 1 (Highest) | #251 | Implement caching layer | Feature | Critical for v2.0 API |
| 2 | #234 | Fix security vulnerability CVE-2025-1234 | Security | High CVE in core code |
| 3 (Normal) | #245 | Modernize namespace declarations | Tech Debt | Quick win cleanup |

## Assessed But Not Selected

Items reviewed but not currently selected for active work.

| Backlog Item (Issue #) | Title | Category | Assessment Priority | Notes |
|----------------------|-------|----------|-------------------|-------|
| #250 | Add XML comments | Documentation | 3 | Good candidate but current priorities take precedence |

## Backlog Housekeeping Summary

**Duplicates Detected**: 2 potential duplicates flagged (#228, #229 duplicates of #221)
**Completed Items**: 1 item suggested for archival (#230)
**Stale Items**: 0

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
### For Product Team

**Adding Items to Backlog:**

1. Create GitHub issue using "Product Backlog Item" template (`.github/ISSUE_TEMPLATE/backlog-item.md`)
2. Add `workflow:product-backlog` label
3. Fill in all metadata fields (source, category, effort, etc.)
4. If handover assets exist, reference their location in the issue body
5. Item is now in backlog, ready for prioritization

**Prioritizing Items:**
1. Create "Product Backlog Prioritization" GitHub issue (or trigger via comment)
2. @copilot executes prioritization workflow (queries GitHub issues with `workflow:product-backlog`)
3. Review `/product/prioritization.md` after completion
4. Top items are automatically moved to `workflow:implementation` queue based on capacity

**Archiving Completed Items:**
1. When implementation completes, close the GitHub issue with appropriate reason
2. Item automatically removed from backlog queries (issue is closed)

### For Research Team

When completing research work:

1. **Create GitHub issue** using "Product Backlog Item" template
   - Add `workflow:product-backlog` label
   - Fill in metadata (source: Research, category, etc.)
   - Include implementation guidance and success criteria
2. **Reference handover assets** if they exist
   - Link to research folder with prototype/design/benchmarks
   - Use issue body to document asset locations
3. **Link from research handover**
   - Reference the backlog GitHub issue from your research folder
4. **Notify product team** (via comment on backlog issue)

See `.team/duties/RESEARCH_DUTY.md` for integration details.

### For Tech Debt Team

When conducting tech debt analysis:

1. **Check existing backlog first** - Query GitHub issues:
   ```bash
   gh issue list --label "workflow:product-backlog" --search "keyword in:title,body"
   ```
2. **For NEW findings:**
   - Create GitHub issue using "Product Backlog Item" template
   - Add `workflow:product-backlog` and `tech-debt` labels
   - Fill in metadata (source: Tech Debt, category, effort, CVE if security)
   - Create markdown file + handover folder if needed (for prototype fixes, analysis docs)

3. **For EXISTING items:**
   - Update the existing GitHub issue with new findings
   - Add comment about re-validation
   - Update priority recommendation if changed

See `.team/duties/TECH_DEBT_DUTY.md` for integration details.

### For Implementation Team

When starting implementation work:

1. **Check prioritization file**: `/product/prioritization.md`
2. **Find backlog GitHub issue** by issue number from prioritization file
3. **Read issue completely** - all metadata, description, acceptance criteria
4. **Review handover assets** if referenced in the issue body
5. **Update issue** - add comment "Starting implementation"
6. **Reference issue** in implementation PR (e.g., "Implements #251")

**When work is complete:**
1. Close the backlog GitHub issue (or it will auto-close when PR with "Fixes #XXX" merges)
2. Item is automatically removed from backlog queries (closed issues are filtered out)

See `.team/duties/IMPLEMENTATION_DUTY.md` for integration details.

## Searching the Backlog

### Finding Items (GitHub Issues)

Use GitHub CLI or web interface to query backlog issues:

```bash
# List all active backlog items
gh issue list --label "workflow:product-backlog" --state open

# Filter by additional labels
gh issue list --label "workflow:product-backlog,tech-debt" --state open
gh issue list --label "workflow:product-backlog,security" --state open

# Search by keyword
gh issue list --label "workflow:product-backlog" --search "caching in:title,body"

# Find high-priority items
gh issue list --label "workflow:product-backlog" --search "Priority Override in:body"

# Count active backlog items
gh issue list --label "workflow:product-backlog" --state open --json number | jq 'length'
```

### Finding Prioritized Items

```bash
# View current priorities
cat product/prioritization.md

# Find items selected for implementation
grep "workflow:implementation" product/prioritization.md
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

## Integration with GitHub Issues

All backlog items are tracked as GitHub issues. The system is designed to work exclusively with GitHub's issue tracking capabilities.

See `.github/ISSUE_TEMPLATE/backlog-item.md` for the backlog item template and `.github/ISSUE_TEMPLATE/implementation.md` for implementation issue template.

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
