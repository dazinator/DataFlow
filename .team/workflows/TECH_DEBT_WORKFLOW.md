# Tech Debt Discovery and Analysis Workflow

---

## ⚠️ CRITICAL: This is a Specialized Research Workflow

**If you're a Copilot agent working on a tech debt discovery issue:**

This workflow is a specialized variant of the Research Workflow for systematically discovering and documenting technical debt. Like research issues, tech debt analysis produces **documentation and selective implementation handovers**, not direct code merges.

### DO (During Tech Debt Analysis):
- ✅ **Read [Document Hygiene Guide](/.github/DOCUMENT_HYGIENE.md)** before creating/updating documentation
- ✅ Create `/research/tech-debt-[date]/` with structured exploration
- ✅ **Review existing product backlog** (`/product/backlog/`) before new exploration
- ✅ Follow systematic exploration areas to discover tech debt
- ✅ Write exploratory code to validate issues and solutions
- ✅ Document all findings in findings report
- ✅ Create production-ready prototype fixes (if applicable)
- ✅ **Include verification checks** in all findings (how to confirm issue still exists)

### DO (After Discovery Complete):
- ✅ Create product backlog items for **all** findings in `/product/backlog/`
- ✅ Save important prototype code to handover folders
- ✅ **REVERT all exploratory code changes** from `/poc/` and `/src/`
- ✅ Keep all documentation and backlog items
- ✅ **Complete self-improvement evaluation** in `.github/workflow-improvements.md`
- ✅ Product team will prioritize items using Product Prioritization workflow

### DON'T:
- ❌ Merge exploratory code (it will be reverted)
- ❌ Create implementation issues before reviewer selection
- ❌ Skip documenting findings that seem minor
- ❌ Revert code before reviewer approval

### OUTCOME:
Tech debt analysis produces **findings report + product backlog items**, not merged code. All findings go to the product backlog for prioritization by the product team. The exploratory code validates findings and demonstrates solutions, then gets reverted.

---

## Workflow Queue

**Query issues designated to this workflow:**

```bash
gh issue list \
  --label "workflow:tech-debt" \
  --state open \
  --json number,title,url
```

**Or use the query script:**
```bash
./.team/scripts/workflow/query-workflow-queue.sh tech-debt
```

**Entry Points:**
- From Triage workflow (tech debt identified)
- From Implementation workflow (debt discovered during work)
- From periodic code quality reviews

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete documentation on querying and handover patterns.

---

## Overview

Tech debt discovery is the systematic process of identifying improvement opportunities in a codebase. This workflow enables Copilot agents to comprehensively analyze code quality, developer experience, and maintenance burden, then present findings for review and selective implementation.

## When to Use This Workflow

Use this workflow when:
- Conducting periodic codebase health reviews
- Onboarding to a new codebase and identifying friction points
- Before major architectural changes (understand current state)
- After completing a feature (reflect on what could be improved)
- When team requests a "code quality audit"

**This is NOT for**:
- Focused research on specific approaches (use Research Workflow)
- Immediate bug fixes (use standard issue/PR process)
- Feature development (use Implementation Workflow)

## How to Initiate Tech Debt Discovery

**Create a GitHub Issue** using the "Tech Debt Discovery" issue template:

1. Go to GitHub Issues → New Issue
2. Select **"Tech Debt Discovery"** template
3. Fill in:
   - Target codebase (POC / Production / Both)
   - Time box estimate
   - Exploration areas to focus on
4. Assign to @copilot or mention @copilot in comments

The issue template includes:
- Complete checklist for @copilot
- Pre-filled structure for analysis context
- Reminder to review existing backlog first
- Expected deliverables and success criteria

**Alternative**: For ad-hoc analysis, create a blank issue and reference this workflow document (`/.team/workflows/TECH_DEBT_WORKFLOW.md`) with clear instructions for @copilot.

## Differences from Standard Research Workflow

| Aspect | Research Workflow | Tech Debt Workflow |
|--------|------------------|-------------------|
| **Focus** | Deep investigation of one topic | Breadth across multiple issues |
| **Output** | Single comprehensive solution | Multiple categorized findings |
| **Depth** | Deep dive with prototypes | Survey with targeted validation |
| **Handover** | One implementation issue | All findings to product backlog |
| **Prioritization** | N/A | Handled by Product Prioritization workflow |
| **Duration** | Variable, can be extensive | Time-boxed (analysis scope dependent) |

## Analysis Scope Guidance

**Codebase Size Considerations**:
- **Small codebase** (<50 files): Focused analysis, can be comprehensive
- **Medium codebase** (50-200 files): Select priority exploration areas
- **Large codebase** (200+ files): Prioritize high-impact areas, may need multiple analyses

**Note**: First tech debt analysis takes longer. Subsequent analyses are faster as patterns become familiar.

## Prioritization Criteria

**Priority Levels**:
- **High**: Security issues, correctness bugs, blocks other work
- **Medium**: Quality improvements, developer experience enhancements, modernization
- **Low**: Nice-to-have, aesthetic improvements, non-critical tooling

**Complexity Levels** (for multi-phase assessment):
- **Small**: Localized changes, < 10 files, atomic scope
- **Medium**: Moderate scope, 10-30 files, may benefit from phases
- **Large**: Broad impact, > 30 files, likely needs phased approach

See [Implementation Handover Guidance](/implementation/HANDOVER_GUIDANCE.md) for creating multi-phase assessments.

## The Tech Debt Discovery Process

### Phase 1: Planning and Setup

**1. Create Research Folder**

```bash
mkdir -p research/tech-debt-$(date +%Y-%m-%d)/{notes,design,handover/{selected,deferred}}
```

Follow standard research folder structure (see `/research/FOLDER_STRUCTURE.md`).

**2. Create Research Plan**

Create `/research/tech-debt-[date]/research-plan.md`:

```markdown
# Tech Debt Analysis: [Codebase Name]

**Analysis Date**: YYYY-MM-DD
**Target Codebase**: [POC | Production | Both]
**Time Box**: [6-12 hours typical]

**Analysis Scope Decision**:
- **Analyze as single codebase** if POC and Production share patterns/issues (e.g., same compiler warnings, similar code practices)
- **Separate analysis** if codebases are distinct with different priorities and issues
- **Combined analysis** makes sense when issues are common across both (saves time, consolidates findings)

## Objectives
- Systematic discovery of tech debt and improvement opportunities
- Document findings for review and prioritization
- Create handover issues for selected improvements

## Exploration Areas
[Select from standard areas or customize:]
- [ ] New developer onboarding simulation
- [ ] Build and test health
- [ ] Code quality and modern practices
- [ ] Developer experience
- [ ] Documentation quality
- [ ] Performance and efficiency
- [ ] Tooling and automation
- [ ] [Custom area]

## Expected Outcomes
- Findings report for reviewer
- Handover issues for selected items
- Backlog items for deferred improvements
```

**3. Review Existing Product Backlog**

**Before starting new exploration**, review `/product/backlog/` for existing items:

**See `/product/README.md` for complete product backlog system documentation.**

```bash
# List all tech debt backlog items
ls -lt product/backlog/techdebt-*.md

# Search for high-priority items
grep -l "Priority: High" product/backlog/techdebt-*.md

# Search by keyword
grep -i "keyword" product/backlog/*.md

# Find small-effort items (quick wins)
grep -l "Effort: Small" product/backlog/techdebt-*.md
```

**For each backlog item found**:
1. **Validate it's still relevant** - Has the code changed? Is it still an issue?
2. **If valid** - Consider selecting for handover (skip new exploration for this area)
3. **If no longer valid** - Recommend archiving (see `/product/README.md`)
4. **Prioritize existing items** - Low-hanging fruit from backlog should be implemented before discovering new issues

**Grouping Small Items**:
- If multiple small backlog items affect similar files/areas, consider grouping them
- Create combined handover issue if they can reasonably be managed in one PR
- Criteria for grouping:
  - Similar files impacted (<5 files overlap)
  - Similar category or purpose
  - Combined effort still Small/Medium (<8 hours)
- If unsure, select one but note potential to include others in implementation issue

**4. Set Up Tracking**

Create `/research/tech-debt-[date]/notes/exploration-notes.md` for running notes during analysis.

Document any backlog items selected in the notes before starting exploration.

### Phase 2: Systematic Exploration

Execute each exploration area systematically. Document findings as you go.

**Note**: Skip exploration areas already covered by validated backlog items selected for handover.

#### Standard Exploration Areas

**1. New Developer Onboarding Simulation**

Simulate first-time developer experience:

```bash
# Pretend you've just cloned the repo
# 1. Can you find build instructions?
# 2. Does solution build without errors?
# 3. Can you run tests?
# 4. Is it clear how to add a new feature?
# 5. Where do you get stuck? What's confusing?
```

**Document**: Missing docs, unclear instructions, build issues, confusing structure.

**2. Build and Test Health**

**Clean Build First** (avoid cached results):
```bash
# Clean before analysis
dotnet clean

# Fresh build and capture output
dotnet build 2>&1 | tee build-output.txt

# Count warnings
grep "warning" build-output.txt | wc -l
# Categorize warnings
grep "warning" build-output.txt | cut -d: -f4 | sort | uniq -c

# Run tests
dotnet test

# Check for flaky tests, slow tests, unclear test names
```

**Document**: Compiler warnings by category, test failures, slow tests, test organization issues.

**3. Code Quality and Modern Practices**

Survey codebase for:
- Outdated C# patterns (vs current version capabilities)
- Code duplication
- Naming inconsistencies
- Public API surface issues
- Missing abstractions
- Over-engineering

**Tools**:
```bash
# Find non-file-scoped namespaces
grep -r "^namespace " --include="*.cs" | wc -l

# Find missing nullable annotations
# Find var vs explicit type inconsistencies
# Find primary constructor opportunities
```

**Document**: Pattern inconsistencies, duplication examples, modernization opportunities.

**4. Developer Experience**

Try realistic scenarios:
- Add a new block type - how many files to touch?
- Write a test - how much boilerplate?
- Find documentation for a feature - how easy?
- Debug a test failure - how clear are error messages?

**Document**: Friction points, missing helpers, unclear patterns, boilerplate burden.

**5. Documentation Quality**

Check:
- README completeness (is it up to date?)
- API documentation coverage (xmldoc on public APIs?)
- Example code (does it work? is it current?)
- Architecture documentation (is it accurate?)
- Migration guides (for breaking changes)

**Document**: Missing docs, outdated docs, confusing explanations.

**6. Performance and Efficiency**

Look for:
- Obvious inefficiencies (N+1 queries, repeated allocations)
- Async/await anti-patterns
- Missing benchmark coverage
- Memory allocation patterns
- Threading issues

**Note**: This is survey-level, not deep profiling.

**Document**: Clear inefficiencies, missing benchmarks, anti-patterns.

**7. Tooling and Automation**

Review:
- Build scripts (clear and maintained?)
- Linting/formatting configuration
- Code analyzers enabled
- CI/CD pipeline quality
- Developer tooling (scripts, helpers)

**Document**: Missing tooling, configuration issues, automation opportunities.

### Phase 3: Document Findings

**1. Cross-Reference Existing Research**

**Before finalizing findings**, check for duplicates in existing research:

```bash
# Search research folders for related topics
find research/ -name "*.md" -type f | xargs grep -l "nullable warnings"
find research/ -name "*.md" -type f | xargs grep -l "test helpers"

# Check product backlog for similar items
grep -r "compiler warnings" product/backlog/
```

**For each potential duplicate**:
- If duplicate found, **reference existing research** instead of creating new finding
- Example: "TD-007: Test Helper Opportunities - See existing research at `/research/testing-approaches/`"
- Helps avoid redundant work and consolidates related findings
- Update existing backlog item priority if needed rather than creating duplicate

**2. Organize Findings by Category**

Create `/research/tech-debt-[date]/notes/findings-by-category.md`:

```markdown
# Findings by Category

## Compiler Warnings (620 warnings)
- CS0436: Type conflicts (15+ instances)
- CS0169: Unused fields (10+ instances)
- CS8425: Missing EnumeratorCancellation (5 instances)
[etc.]

## Modern C# Practices
- Non-file-scoped namespaces (100+ files)
- Missing primary constructors (50+ opportunities)
- Var keyword inconsistency
[etc.]

## Developer Experience
- Service provider setup boilerplate (8-10 lines per test)
- Missing test helpers (15+ duplicate collectors)
- No scaffolding commands for new blocks
[etc.]

## Documentation
- README missing build prerequisites
- No architecture diagram
- Outdated examples in guides
[etc.]
```

**2. Create Findings Report**

Create `/research/tech-debt-[date]/findings-report.md`:

This report documents all discovered issues for the product team to review and prioritize.

```markdown
# Tech Debt Findings Report

**Analysis Date**: YYYY-MM-DD
**Target Codebase**: [POC | Production]
**Total Findings**: [N]

## Summary

[High-level overview of what was found]

### High Priority (H)
- [Count] findings requiring attention

### Medium Priority (M)  
- [Count] findings that would improve quality

### Low Priority (L)
- [Count] nice-to-have improvements

---

## Finding TD-001: [Short Title]

**Category**: Compiler Warnings  
**Priority**: High  
**Complexity**: Medium  
**Files Affected**: ~100

### Current State
[Describe the issue - be specific]

### Proposed Improvement
[Describe what should be done]

### Value Proposition
- **Impact**: [What gets better]
- **Risk**: [What could go wrong]
- **Benefits**: [Why this matters]

### Implementation Approach
[High-level steps]

### Multi-Phase Assessment
**Recommendation**: [ ] Single-Phase  [ ] Multi-Phase
**Rationale**: [Brief reasoning based on volume, complexity, risk factors]
[High-level steps]

### Validation
[How to verify it's fixed - tests, benchmarks, metrics]

### Verification Check
**REQUIRED**: Include verification check for implementation team

```bash
# Command to verify this tech debt still exists
# Example: Check for specific warnings
dotnet build 2>&1 | grep "CS0436" | wc -l
# Expected: ~15 (if 0, tech debt already fixed)
```

---

## Finding TD-002: [Next Finding]
[Same structure]

---

[Continue for all findings]
```

### Phase 4: Create Product Backlog Items

**For Each Finding** (all findings go to product backlog):

Create product backlog item in `/product/backlog/techdebt-YYYY-MM-DD-[short-name].md`

**See `/product/README.md` for complete product backlog system documentation.**

Use backlog item template from `/product/backlog-item-template.md`:

```markdown
# [Finding Title]

**Backlog ID**: techdebt-YYYY-MM-DD-[short-name]
**Source**: Tech Debt
**Category**: [Category]
**Status**: Active
**Created**: YYYY-MM-DD
**Updated**: YYYY-MM-DD

## Summary

[Brief 1-2 sentence description of the tech debt issue]

## Context

**Source**: Tech debt analysis - `/research/tech-debt-[date]/findings-report.md` (Finding TD-[NNN])
**Priority**: [High/Medium/Low]
**Effort**: [Small/Medium/Large]

[Describe the tech debt issue and why it matters]

## Implementation Guidance

### Approach
[How to implement the fix]

### Files to Change
[List affected files/areas]

### Testing Strategy
[How to validate fix]

## Success Criteria

- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Validation benchmark/test passes]
- [ ] Tests passing
- [ ] Documentation updated (if applicable)

## Verification Check

**CRITICAL**: Include verification check so implementation team can confirm this tech debt still exists before starting work.

```bash
# Command to verify tech debt still exists
# Example: Check for compiler warnings
dotnet build 2>&1 | grep "CS0436" | wc -l
# Expected: ~15 warnings
# If result is 0, tech debt already fixed - update backlog item status
```

**Purpose**: Prevents wasted effort if someone else already fixed the issue.

## Handover Assets

[If prototype fixes exist]
- **Location**: `/product/backlog/techdebt-YYYY-MM-DD-[name]/prototype/`
- **Contents**: Prototype fixes demonstrating solution

[If no prototypes]
- No additional assets

## References

- Tech debt analysis: `/research/tech-debt-[date]/findings-report.md` (Finding TD-[NNN])
- Related issues: #[N]
- Affected files: [List key files]

## Notes

**Multi-Phase Assessment**: [Single/Multi]
[If multi-phase, explain why and suggest phases]

[Any additional context or edge cases]
```

**Naming Convention**: `techdebt-YYYY-MM-DD-[short-kebab-case-name].md`
- `techdebt-` prefix identifies source
- Date enables chronological browsing
- Short name enables quick identification
- Examples:
  - `techdebt-2025-11-07-reduce-cs0436-warnings.md`
  - `techdebt-2025-11-07-modernize-namespace-declarations.md`
  - `techdebt-2025-11-07-add-test-helpers.md`

**If prototype fixes exist**:

Create handover folder and copy prototypes:

```bash
# Create handover folder
mkdir -p product/backlog/techdebt-YYYY-MM-DD-[name]/prototype

# Copy prototype fixes
cp poc/DataFlow.POC.Tests/ImprovedTestHelper.cs \
   product/backlog/techdebt-YYYY-MM-DD-[name]/prototype/

# Create README explaining prototypes
cat > product/backlog/techdebt-YYYY-MM-DD-[name]/prototype/README.md << 'EOF'
# Prototype Fixes

## [File].cs
[Description of what this prototype demonstrates]
- [Key insight 1]
- [Key insight 2]
EOF
```

### Phase 5: Code Reversion and Finalization

**After all backlog items created**:

**1. Preserve Prototype Code** (if applicable)

If you created prototype fixes during exploration, save valuable examples to product backlog handover folder (see Phase 4 above).

**2. Revert Exploratory Code**

Only revert actual code changes, keep documentation:

```bash
# Keep documentation
git add research/
git add product/

# Revert exploratory code in core projects
git checkout HEAD -- poc/DataFlow.POC/
git checkout HEAD -- poc/DataFlow.POC.Tests/
git checkout HEAD -- src/

# Verify only docs and backlog items remain
git status
```

**3. Final Commit**

```bash
git add research/tech-debt-[date]/
git add product/backlog/
git commit -m "Tech debt analysis findings and handovers"
```

**What Stays**:
- ✅ Research folder with all documentation
- ✅ Findings report
- ✅ Product backlog items for all findings
- ✅ Prototype code in handover folders

**What Gets Reverted**:
- ❌ Exploratory code changes
- ❌ Test validation code
- ❌ Prototype implementations in source tree (copied to handover first)

## Using Product Backlog Items

### Browsing the Backlog

```bash
# List all tech debt backlog items chronologically
ls -lt product/backlog/techdebt-*.md

# Search for specific categories
grep -l "Category: Compiler Warnings" product/backlog/*.md

# Find high-priority items
grep -l "Priority: High" product/backlog/*.md
```

### Prioritizing Backlog Items

**Product team uses Product Prioritization workflow** to select items for implementation:
- Review all backlog items
- Consider current priorities and capacity
- Create `/product/prioritization.md` with ordered list
- See `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`

### Implementing Backlog Items

When implementation team picks up a tech debt item:

1. **Read backlog item** completely at `/product/backlog/[item-id].md`
2. **Execute verification check** to ensure tech debt still exists
   - If tech debt already fixed: Update backlog item status and notify team
   - If tech debt exists: Continue with implementation
3. Follow implementation workflow
4. Mark backlog item as completed with PR link
5. Archive to `/product/resolved/`

### Backlog Maintenance

Periodically review backlog:
- Archive completed items
- Update priorities based on project evolution
- Combine related items
- Remove obsolete items (those verified as already fixed)

## Templates Summary

### 1. Research Plan Template
Use standard research plan format with tech debt specific exploration areas.

### 2. Findings Report Template
Key deliverable for reviewer selection decisions. Format shown in Phase 3.

### 3. Tech Debt Handover Issue Template
Adapted implementation issue template for selected findings. Format shown in Phase 5.

### 4. Backlog Item Template
Lightweight format for non-selected findings. Format shown in Phase 5.

## Integration with Other Workflows

### Relationship to Research Workflow
- Tech debt analysis is a specialized research variant
- Uses same infrastructure and reversion process
- Focuses on breadth of discovery vs depth

### Relationship to Implementation Workflow
- Selected handovers → standard implementation issues
- Follow existing implementation workflow
- No special handling needed

### Relationship to Standard PRs
- Tech debt fixes can be done in any PR
- Not all tech debt fixes need the full workflow
- Use workflow for systematic reviews, not individual fixes

## Best Practices

### During Discovery

**Be Comprehensive**: Cover all exploration areas systematically. Don't skip areas that seem "fine" - often that's where interesting findings hide.

**Be Specific**: Vague findings like "improve documentation" aren't actionable. Specific findings like "README missing build prerequisites for Windows" are.

**Validate Issues**: If you think something is tech debt, write a small test or prototype to confirm the issue and validate the fix.

**Document Context**: Capture why something is problematic, not just that it is. Future readers need to understand the "why".

### During Reporting

**Prioritize Ruthlessly**: Not everything needs to be fixed. Focus on high-value items.

**Assess Multi-Phase Needs**: For each finding, evaluate whether implementation would benefit from phased approach. Consider volume, complexity, and risk factors. See [Implementation Handover Guidance](/implementation/HANDOVER_GUIDANCE.md).

**Show Value**: Every finding should clearly articulate why fixing it matters. "It's cleaner" isn't enough; "reduces test boilerplate by 60%" is.

**Group Related Findings**: If 5 findings all relate to modernizing C#, consider grouping them into one implementation effort.

### During Handover

**Make Issues Actionable**: Each handover should be implementable by someone who wasn't involved in the analysis.

**Don't Over-Specify**: Provide guidance, not prescriptive implementation. Leave room for implementer judgment.

**Include Tool Guidance**: When recommending automation tools:
- **Copilot-Available Tools**: Prefer command-line tools available to copilot agents (dotnet CLI, sed, awk, grep, find)
- **IDE-Only Tools**: If recommending Visual Studio, ReSharper, or IDE-specific features:
  - Mark as **[Requires Reviewer]** in the implementation issue
  - Note that implementation agent should notify reviewer at this step
  - Provide alternative command-line approach if possible
- **Hybrid Approach**: Suggest copilot-friendly automation where possible, IDE tools as alternative

**Example - Tool Recommendations**:
```markdown
### Implementation Approach

**Option 1: Command-line (Copilot-friendly)**
```bash
# Automated with dotnet format
dotnet format --include src/
```

**Option 2: IDE Refactoring [Requires Reviewer]**
- Visual Studio: Right-click → "Convert to file-scoped namespace"  
- Note: Notify reviewer to perform bulk refactoring, then continue
```
```

**Include Validation**: Every handover should specify how to verify the fix works.

### Backlog Management

**Keep It Lightweight**: Backlog items don't need to be as comprehensive as handover issues. Brief but actionable.

**Date Everything**: Enables understanding when items were identified and prioritization changes over time.

**Review Regularly**: Backlog that's never reviewed becomes junk. Schedule periodic reviews.

## Example: POC Tech Debt Analysis

**Scenario**: Analyze POC codebase for tech debt

**Phase 1 - Planning**:
- Create `/research/tech-debt-2025-11-07/`
- Define exploration: onboarding, build health, modern practices, DX, docs

**Phase 2 - Exploration**:
- Build POC: 620 compiler warnings
- Run tests: all pass but some slow
- Analyze code: lots of CS0436 warnings, non-file-scoped namespaces
- Try adding a block: requires touching 5+ files
- Check docs: README good, API docs sparse

**Phase 3 - Findings Report**:
```
TD-001: Reduce CS0436 Type Conflict Warnings (H/M/~100 files) + verification check
TD-002: Modernize to File-Scoped Namespaces (M/L/~200 files) + verification check
TD-003: Add Test Helper Utilities (H/S/~15 test files) + verification check
TD-004: Add API Documentation to Public Types (M/M/~50 types) + verification check
TD-005: Create Block Scaffolding Command (M/M/new feature) + verification check
```

**Phase 4 - Create Backlog Items**:
All findings go to product backlog:
- Create `product/backlog/techdebt-2025-11-07-reduce-type-conflicts.md`
- Create `product/backlog/techdebt-2025-11-07-modernize-namespaces.md`
- Create `product/backlog/techdebt-2025-11-07-add-test-helpers.md`
- Create `product/backlog/techdebt-2025-11-07-add-api-documentation.md`
- Create `product/backlog/techdebt-2025-11-07-block-scaffolding-tool.md`

**Phase 5 - Finalization**:
- Revert exploratory test code
- Keep all documentation and backlog items
- PR ready for merge
- Product team will prioritize items using Product Prioritization workflow

---

## Handover to Next Workflow

When tech debt analysis is complete, hand over findings to the appropriate next workflow.

**See**: [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) for complete handover patterns and troubleshooting.

### Handover to Product Prioritization

**When**: Tech debt analysis complete, backlog items created and need prioritization

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE tech-debt product-backlog "Tech debt analysis complete. Created [N] backlog items for prioritization."
```

**Comment Should Include**:
- Number of backlog items created
- Location: `/product/backlog/`
- Findings report: `/research/tech-debt-[date]/findings.md`

### Handover to Research

**When**: Findings reveal unknowns requiring deeper validation

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE tech-debt research "Tech debt analysis uncovered unknowns requiring research. See findings report."
```

### Handover to Implementation

**When**: Critical tech debt item identified that needs immediate attention

```bash
./.team/scripts/workflow/handover-issue.sh \
  $ISSUE tech-debt implementation "Critical tech debt identified, needs immediate implementation"
```

**Note**: Most tech debt items should go through product prioritization first. Only use direct handover to implementation for critical/blocking issues.

### Close Issue

**When**: Tech debt analysis complete and all items in product backlog

```bash
gh issue close $ISSUE --comment "✅ **Tech Debt Analysis Complete**

Analysis complete with [N] findings.

**Findings Report**: \`/research/tech-debt-[date]/findings.md\`
**Backlog Items Created**: [N] items in \`/product/backlog/\`

All findings available for product team prioritization.

See: \`.team/workflows/TECH_DEBT_WORKFLOW.md\`"
```


## Self-Improvement Loop

**Before marking PR ready for review**, complete evaluation in `.github/workflow-improvements.md`:

1. Reflect on tech debt workflow effectiveness
2. Document what worked well
3. Document what didn't work well
4. Propose specific improvements
5. Add to workflow improvements file

This continuous feedback improves the workflow for future tech debt analyses.

---

## Summary

The Tech Debt Discovery Workflow enables systematic identification and documentation of codebase improvements. It produces:

1. **Findings Report** - Comprehensive survey of tech debt issues
2. **Product Backlog Items** - All findings added to `/product/backlog/` with verification checks
3. **Prioritization Handoff** - Product team uses Product Prioritization workflow to select items for implementation

This workflow complements existing research and implementation workflows by focusing on breadth of discovery and integration with the product backlog system.

**Result**: Clean, systematic tech debt discovery with all findings preserved in the product backlog for prioritization.
