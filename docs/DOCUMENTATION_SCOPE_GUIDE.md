# Documentation Scope Guide

This guide defines what content belongs in global instructions vs workflow-specific documentation, providing a formal framework for scoping documentation concepts.

---

## Purpose

As `copilot-instructions.md` grows, we need clear criteria for determining where documentation belongs. This guide tracks **shared** and **global** scope concerns to prevent duplication and ensure consistency.

**Scoping Precedent**:
1. **Default: Keep it workflow-specific** - If in doubt, document within the specific workflow file
2. **Promote to shared** - If multiple workflows find it valuable, create a shared `.team/` document
3. **Promote to global** - If it's a cross-cutting concern for all workflows (present and future), reference it in `copilot-instructions.md`

This guide maps shared and global concepts. Workflow-specific concepts are not tracked here as they remain isolated within their respective workflow files.

---

## Scoping Decision Framework

### Default: Workflow-Specific

**When in doubt, keep it workflow-specific.**

- Document concepts within the specific workflow file that uses them
- No need to track in this guide
- Simplifies maintenance and reduces coordination overhead

---

### Promote to Shared (.team/ docs)

**Criteria**:
- Used by MULTIPLE workflows (but not all)
- Substantial enough to warrant separate document
- Benefits from centralized updates (change once, affects multiple workflows)
- Reduces duplication across workflow files

**Examples**:
- Central Package Management → Used by Implementation + Research
- NuGet Dependency Updates → Specialized guidance for dependency work

**How to Reference**:
- Workflow files include "Required Reading" or "See Also" sections
- Link to shared doc with brief context
- Don't duplicate content - reference only

**Track in this guide**: Yes - see "Shared Concepts" table below

---

### Promote to Global (copilot-instructions.md)

**Criteria**:
- Cross-cutting concern for ALL workflows (present and future)
- Critical for navigation and workflow routing
- Fundamental to how Copilot agents operate in this repository
- Required reading regardless of which workflow you're in

**Examples**:
- Workflow routing and navigation
- Label schema and workflow topology
- Self-improvement loop (applies to all workflows)
- Repository structure overview
- Workflow label ownership rules

**Document Position**:
- **Top third**: Critical navigation, workflow routing, label rules
- **Middle third**: Repository overview, structure, cross-workflow patterns
- **Bottom third**: License info, getting help, references

**Location Strategy**:
- Keep in `.team/` directory unless very GitHub-vendor-specific
- Reference from `copilot-instructions.md` for discoverability

**Track in this guide**: Yes - see "Global Concepts" table below

---

## Concept Mapping Tables

### Global Concepts (copilot-instructions.md)

These concepts are needed by ALL workflows and belong in copilot-instructions.md:

| Concept | Scope | Current Location | Section | Rationale |
|---------|-------|-----------------|---------|-----------|
| Workflow Label Schema | Global | copilot-instructions.md | Top | All workflows use labels |
| Workflow Routing | Global | copilot-instructions.md | Top | All workflows need routing |
| Self-Improvement Loop | Global | copilot-instructions.md | Top | All workflows complete evaluation |
| Workflow Topology System | Global | copilot-instructions.md | Middle | All workflows query/handover |
| Repository Structure | Global | copilot-instructions.md | Middle | All workflows navigate repo |
| Workflow File Ownership | Global | copilot-instructions.md | Top | All workflows respect boundaries |
| Multi-Phase Issue Convention | Global | copilot-instructions.md | Top | All workflows may create sub-issues |
| Comment Prefix Convention | Global | copilot-instructions.md | Top | All workflows use prefixes |
| Document Hygiene | Global | `docs/DOCUMENT_HYGIENE.md` | Middle | All workflows create/update documentation |
| Building and Testing | Global | copilot-instructions.md | Bottom | All workflows build/test |
| License Info | Global | copilot-instructions.md | Bottom | Legal requirement, always visible |

---

### Shared Concepts (.team/ documents)

These concepts are used by multiple workflows and belong in shared .team/ documents:

| Concept | Scope | Current Location | Used By | Target Location |
|---------|-------|-----------------|---------|----------------|
| Getting Started Guide | Shared | `docs/guides/GETTING_STARTED.md` (NEW) | Implementation, Research | `docs/guides/GETTING_STARTED.md` ✅ |
| Central Package Management | Shared | `docs/guides/CENTRAL_PACKAGE_MANAGEMENT.md` | Implementation, Research | `docs/guides/CENTRAL_PACKAGE_MANAGEMENT.md` ✅ |
| NuGet Dependency Updates | Shared | `docs/guides/NUGET_DEPENDENCY_UPDATES.md` | Implementation, Research | `docs/guides/NUGET_DEPENDENCY_UPDATES.md` ✅ |

**Note on Getting Started Guide**: Contains C# coding standards, async/await patterns, testing standards, common patterns, and anti-patterns. Referenced from Implementation and Research workflows' Required Reading sections.

**Note on Document Hygiene**: Document Hygiene was promoted to Global because ALL workflows create or update documentation at some point (issue creation, PR descriptions, comments, etc.), making it a true cross-cutting concern.

**Note on Multi-Phase Issues**: Multi-Phase Issues is listed in Global Concepts table because ALL workflows may create sub-issues, making it a true cross-cutting concern.

---

## Decision Process for New Concepts

When adding new documentation or concepts, follow this process:

### Step 1: Start Workflow-Specific

**Default**: Document the concept within the workflow file that needs it.

- No coordination overhead
- Easy to maintain
- Clear ownership
- Not tracked in this guide

### Step 2: Assess for Promotion to Shared

Ask: **Do multiple workflows need this same content?**

- YES → Consider creating shared `.team/` document
- NO → Keep workflow-specific

**If creating shared document**:
- Add entry to "Shared Concepts" table in this guide
- Update affected workflows to reference it
- Remove duplicated content from workflow files

### Step 3: Assess for Promotion to Global

Ask: **Is this a cross-cutting concern for ALL workflows (present and future)?**

- YES → Consider promoting to global
- NO → Keep as shared or workflow-specific

**If promoting to global**:
- Keep document in `.team/` unless very GitHub-vendor-specific
- Add reference in `copilot-instructions.md` for discoverability
- Add entry to "Global Concepts" table in this guide
- Update affected workflows to reference it

---

## Document Size and Position Guidance

### Size Considerations

When creating shared or global documents:

- **Substantial content (>100 lines)** → Strong candidate for separate `.team/` document
- **Concise content (<50 lines)** → Can be inline in workflow or global instructions
- **Frequently changing** → Separate document easier to maintain
- **Stable** → Can be inline

### Position Within Files

Within workflow or global files, determine section:

- **Top third** - Must be read before starting work; critical warnings or rules
- **Middle third** - Main workflow content; used during work execution
- **Bottom third** - Reference material; examples and edge cases

### Cross-References

When creating shared documents:
- Add reference in relevant workflow files
- Use "Required Reading" or "See Also" sections
- Provide brief context about when to consult

---

## Common Patterns

### Pattern 1: Global vs Shared

**Global** (cross-cutting for all workflows):
- Workflow coordination (e.g., "Self-improvement loop", "Multi-phase issues")
- Universal process rules (e.g., "Label schema", "Comment prefix convention")
- Repository navigation (e.g., "Workflow topology system")

**Shared** (multiple workflows, but not all):
- Code-specific guidance (e.g., "Central package management", "Async/await patterns")
- Specialized processes (e.g., "Document hygiene", "NuGet dependency updates")

**Example**: 
- Global: "Self-improvement loop" - ALL workflows complete evaluation (Triage, Research, Implementation, Process Modeling, etc.)
- Shared: "Central package management" - Only Implementation and Research workflows deal with code dependencies

---

### Pattern 2: Cross-Workflow Concerns

When multiple workflows need the same concept:

**Option A: Keep Global** if:
- Very concise (<50 lines)
- Foundational to repository
- Examples: Coding standards, testing patterns

**Option B: Create Shared Doc** if:
- Substantial (>100 lines)
- Detailed procedures
- Examples: Dependency updates, package management

**Decision**: Favor shared docs to keep copilot-instructions scannable.

---

### Pattern 3: Progressive Disclosure

Structure documentation for progressive disclosure:

1. **Global**: High-level principle + link to details
2. **Workflow**: When this applies + link to shared doc
3. **Shared Doc**: Complete detailed guidance

**Example**:
- Global: "Use centralized package management (see .team/CENTRAL_PACKAGE_MANAGEMENT.md)"
- Implementation Workflow: "Step 6: For dependency updates, see CENTRAL_PACKAGE_MANAGEMENT.md"
- Shared Doc: Complete guide with all details, patterns, edge cases

---

## Refactoring Checklist

When moving content from global to shared docs:

- [ ] Create new .team/ document with complete content
- [ ] Update global docs to reference new location (don't duplicate)
- [ ] Update affected workflow files to reference new location
- [ ] Add to "Required Reading" sections where applicable
- [ ] Ensure navigation works (all links valid)
- [ ] Test with tabletop simulation scenarios

---

## Maintenance

### When to Revisit Scopes

Revisit scoping decisions when:
- Workflow files grow beyond 1000 lines
- Concepts are duplicated across multiple workflows
- New workflows are added
- Agents report confusion about where to find information

### Keeping This Guide Updated

When creating new .team/ documents or updating scopes:
- Update the concept mapping tables
- Document rationale for scope decision
- Add examples of how concept is referenced

---

## Examples

### Example 1: Central Package Management (Shared → .team/)

**Before**: 
- Content in copilot-instructions.md (~100 lines)
- Used by Implementation and Research workflows
- Duplicated some content with NUGET_DEPENDENCY_UPDATES.md

**After**:
- Complete guide in `docs/guides/CENTRAL_PACKAGE_MANAGEMENT.md`
- Cross-references with NUGET_DEPENDENCY_UPDATES.md
- copilot-instructions.md has brief reference
- Implementation/Research workflows reference in "Required Reading"

**Rationale**: Substantial content, used by multiple workflows, benefits from dedicated document.

---

### Example 2: Testing Standards (Shared, kept in copilot-instructions.md)

**Analysis**:
- Used by Implementation and Research workflows (not all workflows)
- Scope: Shared (not Global)
- Concise (~50 lines)
- Fundamental to code quality in this repository

**Decision**: Keep in copilot-instructions.md despite being Shared
- Concise content doesn't create clutter
- Quick reference for code review
- Maintains coherent "how to contribute code" guidance
- Exception to "shared → .team/" rule due to size

**Note**: This is Shared scope (not Global) because Process Modeling, Triage, and other non-coding workflows don't need it.

---

## Related Documentation

- [Document Hygiene Guide](/docs/DOCUMENT_HYGIENE.md) - Principles for maintainable docs
- [Process Modeling Duty](/.team/duties/PROCESS_MODELING_DUTY.md) - How to update workflows and processes
- [Workflow Topology Guide](/.github/docs/WORKFLOW_TOPOLOGY_GUIDE.md) - Workflow system overview
