# Self-Improvement Feedback Procedure

**Purpose**: Submit workflow improvement feedback after completing work

**Layer**: 1 (Global Procedure)

**Used By**: All duties (Layer 2), invoked before completing work

---

## Overview

The self-improvement loop ensures workflows continuously evolve based on real experiences. Every agent submits feedback after completing work, documenting what worked well and what needs improvement.

---

## Required Context

**⚠️ IMPORTANT**: Before using this procedure, understand:

- **[Semantic Language](../../docs/design/prompt-engineering/semantic-language.md)** - Definitions of semantic operations used below
- **[Kernel Layer](../kernel/README.md)** - Platform abstraction that implements semantic operations

**Semantic Operations Used**:
- `submit_feedback(duty, work_item_id, worked_well, didnt_work_well, improvements)` - Submit feedback
- `get_work_item_details(work_item_id)` - Get work item context

**Feedback Fields**:
- `duty` - Which duty was executed (for categorization)
- `work_item_id` - Reference to work completed
- `worked_well` - List of positives (what helped success)
- `didnt_work_well` - List of issues (what caused delays/confusion)
- `improvements` - List of specific, actionable improvements

---

## When to Submit Feedback

**⚠️ CRITICAL**: Submit feedback BEFORE marking work complete or requesting review.

**Required**: For ALL work items in ALL duties
- Research work
- Implementation work
- Process modeling changes
- Tech debt discovery
- Product prioritization
- Triage assessment

**Timing**: After work is complete but before final review requested

---

## Procedure

### Step 1: Reflect on Workflow Effectiveness

Consider the duty you just executed:

**Questions to ask**:
1. **Which duty did I use?** (Research, Implementation, Process Modeling, etc.)
2. **Did the duty guidance help or hinder progress?**
3. **Were there missing instructions that would have been helpful?**
4. **What workflow steps were unclear or confusing?**
5. **What guidance helped me avoid mistakes?**

### Step 2: Document What Worked Well

Identify positive aspects that contributed to success:

**Examples of what worked well**:
- ✅ "Clear step-by-step instructions in Phase X"
- ✅ "Handover template made requirements gathering easy"
- ✅ "Multi-phase guidance helped structure the work"
- ✅ "Code review checklist caught important issues"
- ✅ "Example code snippets clarified expected patterns"

**Be specific**: Point to exact sections, steps, or guidance that helped

### Step 3: Document What Didn't Work Well

Identify pain points, confusion, or delays:

**Examples of what didn't work**:
- ❌ "Step 5 in research workflow was ambiguous about when to create handover"
- ❌ "No guidance on handling edge case X"
- ❌ "Conflicting instructions between workflow and procedure Y"
- ❌ "Missing information about how to test Z"
- ❌ "Had to iterate multiple times because X wasn't clear"

**Be specific**: Point to exact problems, not vague complaints

### Step 4: Propose Specific Improvements

Make actionable, concrete suggestions:

**Good improvement proposals**:
- ✅ "Add step 5.1: 'Check if parent issue exists before creating handover'"
- ✅ "Update research workflow to include edge case handling for X"
- ✅ "Clarify difference between Y and Z in concepts document"
- ✅ "Add example code for pattern X in implementation workflow"

**Poor improvement proposals**:
- ❌ "Make it better" (not specific)
- ❌ "Fix the workflow" (not actionable)
- ❌ "This is confusing" (no proposed fix)

### Step 5: Submit Feedback

```python
submit_feedback(
    duty="implementation",  # The duty you just executed
    work_item_id=current_work_item_id,  # Current work item
    worked_well=[
        "Clear step-by-step instructions in Phase 3",
        "Handover template made requirements gathering easy",
        "Example code snippets clarified expected patterns"
    ],
    didnt_work_well=[
        "Step 5 was ambiguous about when to create handover",
        "No guidance on handling edge case with null values",
        "Had to iterate on test structure due to missing examples"
    ],
    improvements=[
        "Add step 5.1: 'Check if parent issue exists before creating handover'",
        "Add section on null value handling in implementation workflow",
        "Include test structure example in implementation workflow testing section"
    ]
)
```

**Platform Implementation**:
- GitHub: Creates comment on Workflow Feedback Tracker issue
- Azure DevOps: Creates comment on Workflow Feedback work item

---

## Examples

### Example 1: Implementation Work Feedback

```python
# After completing implementation work
submit_feedback(
    duty="implementation",
    work_item_id="456",
    worked_well=[
        "Multi-phase guidance helped structure the 4-phase plan",
        "Code review tool caught potential null reference issues",
        "Getting Started guide had clear examples of async patterns"
    ],
    didnt_work_well=[
        "Unclear when to run tests - before or after code review?",
        "No guidance on documenting breaking changes in PR",
        "Had to search for dependency update procedure"
    ],
    improvements=[
        "Add testing step before code review in workflow",
        "Include breaking changes checklist in PR template",
        "Link to dependency update guide from implementation workflow"
    ]
)
```

### Example 2: Research Work Feedback

```python
# After completing research work
submit_feedback(
    duty="research",
    work_item_id="789",
    worked_well=[
        "Research folder structure conventions made organization clear",
        "Handover template ensured all required information included",
        "ADR template helped document decision rationale"
    ],
    didnt_work_well=[
        "No guidance on how much benchmarking is 'enough'",
        "Unclear whether to create implementation issue or wait",
        "Missing example of multi-phase research handover"
    ],
    improvements=[
        "Add benchmarking guidelines to research workflow",
        "Clarify handover work item creation in Step 6",
        "Add multi-phase research handover example to documentation"
    ]
)
```

### Example 3: Documentation-Only Changes Feedback

```python
# After completing documentation updates
submit_feedback(
    duty="process-modeling",
    work_item_id="321",
    worked_well=[
        "Change procedure clearly defined required testing steps",
        "Leak detection tools made validation straightforward",
        "Tabletop testing methodology was effective"
    ],
    didnt_work_well=[
        "Testing framework document was long - hard to find specific guidance",
        "Scenario creation examples were abstract"
    ],
    improvements=[
        "Add table of contents to testing framework document",
        "Include concrete scenario examples for each node type"
    ]
)
```

---

## Edge Cases

### Case 1: No Issues Encountered (Everything Worked)

**Scenario**: Workflow was perfect, no improvements needed

**Resolution**:
1. Still submit feedback documenting what worked
2. Leave `didnt_work_well` and `improvements` as empty lists
3. Example:
   ```python
   submit_feedback(
       duty="implementation",
       work_item_id="123",
       worked_well=[
           "All guidance was clear and complete",
           "No ambiguities or missing steps encountered"
       ],
       didnt_work_well=[],
       improvements=[]
   )
   ```

### Case 2: Major Workflow Problems

**Scenario**: Workflow guidance was severely lacking or wrong

**Resolution**:
1. Submit detailed feedback documenting all issues
2. Consider also creating a process-modeling work item for urgent fixes
3. Example:
   ```python
   submit_feedback(
       duty="implementation",
       work_item_id="456",
       worked_well=[],
       didnt_work_well=[
           "Step 3 instructions contradicted Step 5",
           "Critical testing step missing from workflow",
           "No guidance on required code review criteria"
       ],
       improvements=[
           "Reconcile Step 3 and Step 5 guidance",
           "Add testing step before code review",
           "Add code review criteria checklist"
       ]
   )
   
   # Also create urgent process-modeling issue
   create_work_item(
       type="process-modeling",
       title="URGENT: Implementation workflow missing critical testing step",
       description="[Details]",
       duty="process-modeling"
   )
   ```

### Case 3: Feedback About Another Duty

**Scenario**: Discovered issues with research workflow while doing implementation

**Resolution**:
1. Submit feedback for the duty you executed (implementation)
2. In `improvements`, note the cross-duty issue
3. Example:
   ```python
   submit_feedback(
       duty="implementation",
       work_item_id="789",
       worked_well=["Implementation workflow was clear"],
       didnt_work_well=[
           "Received incomplete handover from research - missing edge cases"
       ],
       improvements=[
           "Research workflow should include edge case documentation checklist in handover template"
       ]
   )
   ```

---

## Anti-Patterns

### ❌ Don't: Skip Feedback Submission

```python
# Wrong - completing work without feedback
# No call to submit_feedback()
# Missing opportunity to improve workflows
```

✅ **Correct**: Always submit feedback before completing work

### ❌ Don't: Submit Vague Feedback

```python
# Wrong - not actionable
submit_feedback(
    duty="implementation",
    work_item_id="123",
    worked_well=["Some things were good"],
    didnt_work_well=["It was confusing"],
    improvements=["Make it better"]
)
```

✅ **Correct**: Be specific and actionable

### ❌ Don't: Only Provide Complaints Without Solutions

```python
# Wrong - problems without improvements
submit_feedback(
    duty="implementation",
    work_item_id="456",
    worked_well=[],
    didnt_work_well=["Step 5 unclear", "Missing guidance", "Confusing"],
    improvements=[]  # No solutions proposed
)
```

✅ **Correct**: For each issue, propose a specific improvement

### ❌ Don't: Use Platform-Specific Code

```python
# Wrong - using GitHub-specific API
add_issue_comment(
    issue_number=TRACKER_NUMBER,
    body="Feedback: [...]"
)
```

✅ **Correct**: Use semantic operation `submit_feedback()`

---

## Success Criteria

- [ ] Reflection completed on workflow effectiveness
- [ ] What worked well documented (specific examples)
- [ ] What didn't work well documented (specific issues)
- [ ] Improvements proposed (actionable suggestions)
- [ ] Feedback submitted using semantic operation
- [ ] Feedback includes all required fields

---

## Feedback Processing

**Who processes feedback**: Process Modeling duty

**When**: Periodically (when assigned by human reviewer)

**How**: 
1. Human assigns Process Modeling duty to Feedback Tracker
2. Process Modeling reviews feedback comments
3. Groups related feedback
4. Addresses improvements using standard workflow
5. Marks feedback as addressed

**You don't need to wait**: Submit feedback and continue - processing happens asynchronously

---

## Related Procedures

- [Comment Patterns](comment-patterns.md) - Standard comment formats
- [Duty Assignment](duty-assignment.md) - Which duty to reference in feedback

---

## Testing

**Test Scenarios**: `/research/workflow-modeling/scenarios/procedures/self-improvement/`

**Key Scenarios**:
1. Successful work with feedback (happy path)
2. No issues encountered (everything worked)
3. Major workflow problems (extensive feedback)
4. Cross-duty feedback (issue with another duty)
5. Documentation-only changes feedback
