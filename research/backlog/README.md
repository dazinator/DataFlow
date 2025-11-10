# Tech Debt Backlog (DEPRECATED)

⚠️ **This backlog system has been replaced by the unified Product Backlog.**

**New Location**: `/product/backlog/`

**Please use the new system**: All new backlog items should be added to `/product/backlog/` following the conventions in `/product/README.md`.

This folder contains legacy tech debt items identified during systematic tech debt analyses. Items have been migrated to the new product backlog system.

## Purpose

The backlog serves as:
- **Long-term memory** of identified improvements
- **Prioritization resource** for future planning
- **Context preservation** for why issues weren't addressed immediately
- **Discovery tool** for finding related improvements

## File Naming Convention

Format: `YYYY-MM-DD-[short-kebab-case-name].md`

Examples:
- `2025-11-07-reduce-cs0436-warnings.md`
- `2025-11-07-modernize-namespace-declarations.md`
- `2025-11-07-add-test-helpers.md`

The date prefix enables:
- Chronological browsing (`ls -lt`)
- Understanding when items were identified
- Tracking how priorities change over time

## Backlog Item Structure

Each file should contain:
- **Category** - Type of tech debt (e.g., Compiler Warnings, Modern Practices)
- **Priority** - Relative importance (Low/Medium/High)
- **Complexity** - Implementation scope (Small/Medium/Large)
- **Problem** - What the issue is
- **Solution** - How to address it
- **Value** - Why it matters
- **Multi-Phase Assessment** - Whether phased implementation recommended
- **References** - Links to original analysis
- **Status** - Current state (Not started/In progress/Completed)

See template in `.team/prompts/TECH_DEBT_WORKFLOW.md` for full format.

## Using the Backlog

### Browsing

```bash
# List all items chronologically (newest first)
ls -lt research/backlog/

# List all items alphabetically by name
ls research/backlog/

# Count total backlog items
ls research/backlog/*.md | wc -l
```

### Searching

```bash
# Find items by category
grep -l "Category: Compiler Warnings" research/backlog/*.md

# Find high-priority items
grep -l "Priority: High" research/backlog/*.md

# Find low-complexity items (quick wins)
grep -l "Complexity: Small" research/backlog/*.md

# Search by keyword
grep -i "test helper" research/backlog/*.md
```

### Filtering by Date

```bash
# Items from 2025-11
ls research/backlog/2025-11-*.md

# Items from specific day
ls research/backlog/2025-11-07-*.md
```

### Promoting to Implementation

When ready to implement a backlog item:

1. **Create implementation issue** in GitHub referencing the backlog file
2. **Follow standard implementation workflow**
3. **Update backlog item** status to "Completed" with PR link
4. **Consider moving to archive** (optional, see maintenance below)

Example:
```markdown
## Status
- [x] Completed
- **PR**: #123
- **Completed**: 2025-12-15
```

## Backlog Maintenance

### Periodic Review (Recommended: Quarterly)

1. **Review priorities** - Some items become more/less important over time
2. **Identify obsolete items** - Code changes may have already addressed issues
3. **Combine related items** - Group related improvements for efficiency
4. **Archive completed items** - Move to `research/backlog/archive/` (optional)

### Archive Structure (Optional)

```
research/backlog/
├── archive/
│   └── 2025-Q4/
│       └── [completed-items].md
└── [active items].md
```

Archiving is optional. Some teams prefer to keep all items in one flat structure for simpler searching.

### Preventing Backlog Rot

**Don't let backlog become a dumping ground:**
- Only add items that have clear value propositions
- Be ruthless about what qualifies as "tech debt"
- Regularly remove obsolete items
- Keep items actionable with sufficient context

**Good backlog item**: "Add file-scoped namespaces to reduce boilerplate (400+ files affected, 1-2 line change per file, automated with dotnet format)"

**Poor backlog item**: "Improve code quality"

## Relationship to Other Workflows

### Tech Debt Workflow
Backlog items come from tech debt analyses where reviewers chose to defer implementation.

### Implementation Workflow  

**⚠️ Important for Implementation Teams**:

When implementing a backlog item:

1. **Read this README first** - Understand backlog item format and update procedures
2. **Verify item is still valid** - Code may have changed since item was created
3. **Follow standard implementation workflow** - The backlog item provides context but doesn't replace a proper implementation issue
4. **Update item status when complete**:
   ```markdown
   ## Status
   - [x] Completed
   - **PR**: #123
   - **Completed**: YYYY-MM-DD
   ```
5. **Consider archiving** - Move to `archive/` folder if team prefers (optional)

The backlog item should be referenced in your implementation PR for traceability.

### Research Workflow
If a backlog item needs further investigation, it can become a research topic. Link the research back to the backlog item.

## Statistics and Insights

Track backlog health:

```bash
# Count by priority
grep "Priority: High" research/backlog/*.md | wc -l
grep "Priority: Medium" research/backlog/*.md | wc -l
grep "Priority: Low" research/backlog/*.md | wc -l

# Count by complexity
grep "Complexity: Small" research/backlog/*.md | wc -l
grep "Complexity: Medium" research/backlog/*.md | wc -l
grep "Complexity: Large" research/backlog/*.md | wc -l

# Count by category
grep "Category:" research/backlog/*.md | cut -d: -f3 | sort | uniq -c

# Find quick wins (High priority + Small complexity)
for f in research/backlog/*.md; do
  if grep -q "Priority: High" "$f" && grep -q "Complexity: Small" "$f"; then
    echo "$f"
  fi
done
```

## Tips for Backlog Success

1. **Keep items atomic** - One clear improvement per file
2. **Date everything** - Context about when items were identified helps prioritization
3. **Link to source** - Always reference the original tech debt analysis
4. **Be specific** - Vague items never get implemented
5. **Review regularly** - Stale backlog is useless backlog
6. **Celebrate completion** - Update status when items are implemented

## Example Backlog Item

See `2025-11-07-reduce-cs0436-warnings.md` (if it exists) or refer to the template in `.team/prompts/TECH_DEBT_WORKFLOW.md`.

---

**Note**: This backlog is specifically for tech debt findings. Feature requests, bug reports, and other issue types belong in GitHub Issues, not here.
