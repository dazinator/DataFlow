# Scenario 001: Code Quality Review

**Duty**: Tech Debt  
**Type**: End-to-End Scenario  
**Complexity**: Medium  
**Created**: 2025-11-12

---

## Purpose

Test tech debt duty's ability to conduct systematic code quality review, document findings, and create product backlog items.

---

## Starting State

**Work Item #1250**:
- **Title**: "Tech Debt: Code quality review for DataFlow.Core"
- **Description**: 
  ```
  Conduct systematic code quality review of DataFlow.Core namespace.
  
  Focus Areas:
  - Code duplication
  - Complex methods
  - Code smells
  
  Deliverables:
  - Findings report
  - Product backlog items for all findings
  ```
- **Duty**: `tech-debt`
- **Status**: `open`

---

## Expected Steps

### Step 1: Create Research Folder

```bash
/research/tech-debt-2025-11-12/
├── findings-report.md
├── notes/
└── handover/
```

### Step 2: Review Existing Backlog

```python
backlog = query_work_items_by_duty("product-backlog")
# Review to avoid creating duplicates
```

### Step 3: Conduct Exploration

Review code and discover issues:
- 3 instances of duplicated validation logic
- 2 methods with high complexity (>15)
- 1 god class with too many responsibilities

### Step 4: Document Findings

Create findings report with verification checks for each finding.

### Step 5: Create Product Backlog Items

```python
# Finding 1: Duplicated validation
backlog_id_1 = create_work_item(
    type="tech-debt",
    title="Tech Debt: Duplicated validation logic in DataFlow.Core",
    description="""...""",
    duty="product-backlog"
)

# Finding 2: Complex methods
backlog_id_2 = create_work_item(
    type="tech-debt",
    title="Tech Debt: Simplify complex methods in TransformBlock",
    description="""...""",
    duty="product-backlog"
)

# Finding 3: God class
backlog_id_3 = create_work_item(
    type="tech-debt",
    title="Tech Debt: Refactor DataFlowBuilder into smaller classes",
    description="""...""",
    duty="product-backlog"
)
```

### Step 6: Complete Analysis

```python
add_work_item_comment(
    work_item_id="1250",
    "[Copilot-Duty: Tech Debt] ✅ Analysis Complete\n\n"
    "**Findings**: Discovered 3 technical debt items\n"
    f"**Product Backlog Items Created**: #{backlog_id_1}, #{backlog_id_2}, #{backlog_id_3}\n\n"
    "**Documentation**: `/research/tech-debt-2025-11-12/findings-report.md`\n\n"
    "**Next Steps**: Product team will prioritize findings"
)

update_work_item(work_item_id="1250", status="completed")
```

---

## Expected Outcome

- **3 product backlog items created** for prioritization
- **Findings report** with verification checks
- **Tech debt work item closed**
- **Backlog items remain open** for product prioritization

---

## Success Criteria

- [ ] Research folder created
- [ ] Existing backlog reviewed
- [ ] Findings documented with verification
- [ ] Product backlog items created for ALL findings
- [ ] Handover to product prioritization complete
- [ ] No platform-specific code (zero kernel leaks)

---

## Semantic Operations Used

- `query_work_items_by_duty(duty)`
- `create_work_item(...)`
- `add_work_item_comment(work_item_id, text)`
- `update_work_item(work_item_id, fields)`
