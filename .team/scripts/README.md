# Graph and Leak Detection Scripts

**Version**: 1.0  
**Created**: 2025-11-12  
**Purpose**: Tools for managing and validating the prompt system dependency graph

---

## Overview

This directory contains scripts for:
- **Graph Management**: Infer, validate, and visualize dependency graphs
- **Leak Detection**: Detect kernel and dependency leaks
- **Dependency Analysis**: Show file dependencies and impact

---

## Scripts

### Graph Management

#### infer-graph.sh

**Purpose**: Extract dependency graph from markdown references

**Usage**:
```bash
./.team/scripts/infer-graph.sh [output-file]

# Default output: .team/model-graph-inferred.yaml
./.team/scripts/infer-graph.sh

# Custom output:
./.team/scripts/infer-graph.sh /tmp/graph-inferred.yaml
```

**What it does**:
- Scans markdown files in `.github`, `.team`, and `docs/design/prompt-engineering`
- Extracts markdown links: `[text](path)`
- Builds node and edge lists
- Generates YAML graph structure

**Output**: Inferred graph in YAML format

**Use Case**: Generate graph from actual file references

---

#### validate-graph.sh

**Purpose**: Validate dependency graph for cycles, orphans, and integrity

**Usage**:
```bash
./.team/scripts/validate-graph.sh [graph-file]

# Default: .team/model-graph.yaml
./.team/scripts/validate-graph.sh

# Custom file:
./.team/scripts/validate-graph.sh .team/model-graph-inferred.yaml
```

**Validations**:
1. ✅ All node files exist
2. ✅ No orphaned nodes (except orchestration)
3. ✅ All edge references valid
4. ✅ No circular dependencies
5. ✅ Graph structure is valid

**Exit Codes**:
- `0`: Graph is valid
- `1`: Validation errors found

**Use Case**: Ensure graph integrity before committing changes

---

#### visualize-graph.sh

**Purpose**: Generate Mermaid diagram from dependency graph

**Usage**:
```bash
./.team/scripts/visualize-graph.sh [graph-file] [output-file]

# Defaults: .team/model-graph.yaml → graph-visualization.md
./.team/scripts/visualize-graph.sh

# Custom files:
./.team/scripts/visualize-graph.sh .team/model-graph.yaml /tmp/viz.md
```

**What it generates**:
- Mermaid flowchart diagram
- Node styling by type (orchestration, kernel, workflow, etc.)
- Edge styling by type (required, optional, cross-reference)
- Legend explaining symbols

**Output**: Markdown file with Mermaid diagram

**Viewing**:
1. Open output file in any Markdown viewer
2. Copy Mermaid block to https://mermaid.live
3. View in VS Code with Mermaid extension

**Use Case**: Visualize system architecture and dependencies

---

#### get-dependencies.sh

**Purpose**: Show dependencies for a specific file

**Usage**:
```bash
./.team/scripts/get-dependencies.sh <file-path>

# Examples:
./.team/scripts/get-dependencies.sh .team/prompts/RESEARCH_WORKFLOW.md
./.team/scripts/get-dependencies.sh .team/kernel/README.md
```

**What it shows**:
- **Dependencies**: What this file depends on
- **Dependents**: What depends on this file
- **Edge types**: Required, optional, cross-reference
- **Impact analysis**: How changes affect other files

**Use Case**: Impact analysis before modifying files

---

### Leak Detection

#### check-kernel-leaks.sh

**Purpose**: Detect platform-specific operations outside kernel layer

**Usage**:
```bash
./.team/scripts/check-kernel-leaks.sh
```

**What it detects**:
- GitHub MCP tool calls (`issue_write`, `list_issues`, etc.) outside kernel
- GitHub-specific labels (`workflow:*`) in procedures/duties
- Platform-specific configuration (owner/repo) outside kernel
- Azure DevOps patterns (future)

**Pattern Source**:
- Patterns are defined in `.team/kernel/domains.yaml` under each domain's `leak_patterns` field
- This ensures patterns are co-located with their kernel configuration for better cohesion

**Output**:
- List of files with kernel leaks
- Line numbers and matched patterns
- Guidance on fixing leaks

**Exit Codes**:
- `0`: No leaks detected
- `1`: Leaks found

**Use Case**: Enforce kernel abstraction (procedures/duties should use semantic operations only)

---

#### check-dependency-leaks.sh

**Purpose**: Detect content duplication across graph

**Usage**:
```bash
./.team/scripts/check-dependency-leaks.sh
```

**What it detects**:
- Content duplicated between dependencies
- Violations of DRY (Don't Repeat Yourself)
- Missing references to canonical sources

**How it works**:
- Extracts significant content chunks
- Compares dependencies line-by-line
- Flags when duplicate content exceeds threshold

**Output**:
- Source and target file pairs
- Number of common lines
- Recommendations for fixing

**Exit Codes**:
- `0`: No significant duplication (or warnings only)

**Use Case**: Maintain single source of truth

---

#### check-graph-drift.sh

**Purpose**: Detect drift between graph and actual documents

**Usage**:
```bash
./.team/scripts/check-graph-drift.sh
```

**What it detects**:
- Graph nodes referencing missing files
- Duty files not documented in graph
- Procedure files not documented in graph
- Kernel files not documented in graph

**How it works**:
1. Parses `.team/model-graph.yaml` to extract all node paths
2. Verifies each referenced file exists
3. Scans `.team/duties/`, `.team/procedures/`, `.team/kernel/` for undocumented files
4. Reports errors (missing files) and warnings (undocumented files)

**Output**:
- Missing file errors (graph references non-existent files)
- Undocumented file warnings (files exist but aren't in graph)
- Summary with error and warning counts

**Exit Codes**:
- `0`: No drift detected or warnings only
- `1`: Errors detected (missing files)

**Use Case**: Ensure graph stays synchronized with actual file structure

---

## Workflows

### Before Committing Changes

**Always run validation**:
```bash
# Validate graph integrity
./.team/scripts/validate-graph.sh

# Check for graph drift
./.team/scripts/check-graph-drift.sh

# Check for kernel leaks (if modifying procedures/duties)
./.team/scripts/check-kernel-leaks.sh

# Check for dependency leaks
./.team/scripts/check-dependency-leaks.sh
```

### When Adding Kernel Nodes

1. **Add to graph manually**: Edit `.team/model-graph.yaml`
2. **Add node** with `type: kernel`
3. **Add edges** from/to kernel
4. **Validate**: `./validate-graph.sh`
5. **Visualize**: `./visualize-graph.sh` (optional - review diagram)

### When Modifying Procedures/Duties

1. **Before changes**: Run `check-kernel-leaks.sh` (baseline)
2. **Make changes**: Replace platform calls with semantic operations
3. **After changes**: Run `check-kernel-leaks.sh` (verify no new leaks)
4. **Validate graph**: If dependencies changed

### Periodic Maintenance

**Monthly or per-phase**:
```bash
# Re-infer graph from actual references
./.team/scripts/infer-graph.sh /tmp/inferred.yaml

# Compare to manual graph
diff .team/model-graph.yaml /tmp/inferred.yaml

# Update manual graph if discrepancies found
```

---

## Examples

### Example 1: Check Impact of Modifying Kernel

```bash
# Show what depends on kernel
./.team/scripts/get-dependencies.sh .team/kernel/README.md

# Output shows:
# Dependents:
#   ← .team/kernel/github/README.md [REQUIRED]
#   ← .team/kernel/tests/README.md [REQUIRED]
#
# Impact: 2 files will need review if kernel changes
```

### Example 2: Validate Before Commit

```bash
# Run all validations
./.team/scripts/validate-graph.sh && \
./.team/scripts/check-graph-drift.sh && \
./.team/scripts/check-kernel-leaks.sh && \
./.team/scripts/check-dependency-leaks.sh

# If all pass, safe to commit
```

### Example 3: Visualize After Adding Kernel

```bash
# Generate visualization
./.team/scripts/visualize-graph.sh .team/model-graph.yaml /tmp/viz.md

# Open in VS Code or browser to view Mermaid diagram
code /tmp/viz.md
```

### Example 4: Detect Kernel Leaks in New Procedure

```bash
# Create new procedure file
vim .team/procedures/new-procedure.md

# Check for leaks (should use semantic operations only)
./.team/scripts/check-kernel-leaks.sh

# If leaks found:
# ✗ Kernel leak detected in: .team/procedures/new-procedure.md
#   Line 42: GitHub pattern 'issue_write' found
#
# Fix by replacing with semantic operation:
#   Before: issue_write(...)
#   After:  create_work_item(...)
```

---

## Troubleshooting

### Graph Validation Fails

**Issue**: Circular dependency detected

**Solution**:
1. Review edges in graph
2. Identify cycle path
3. Remove circular reference
4. Use cross-reference edge type if needed

**Issue**: Missing file

**Solution**:
1. Check node path is correct
2. Verify file exists in repository
3. Update path or create missing file

### Leak Detection False Positives

**Issue**: Pattern matches in documentation/examples

**Solution**:
- Review context of match
- Update pattern to be more specific
- Exclude documentation sections (comments in pattern file)

**Issue**: Legitimate platform-specific code flagged

**Solution**:
- Confirm code is in kernel layer
- Check that patterns exclude kernel paths
- If in kernel, no action needed

---

## Related Documentation

- [Model Graph](../model-graph.yaml) - Current dependency graph
- [Kernel Overview](../kernel/README.md) - Kernel layer documentation
- [Testing Framework](../../docs/design/prompt-engineering/testing-framework.md) - Leak detection methodology
- [Design: Graph-Based Testing](../../docs/design/prompt-engineering/testing-framework.md#graph-based-testing-model)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1 | 2025-11-12 | Added check-graph-drift.sh for detecting drift between graph and files |
| 1.0 | 2025-11-12 | Initial script documentation - all 6 scripts |
