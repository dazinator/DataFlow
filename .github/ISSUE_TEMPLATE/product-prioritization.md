---
name: Product Backlog Prioritization
about: Request automated prioritization of the product backlog
title: 'Product Backlog Prioritization - [Monthly/Security Focus/etc.]'
labels: ['prioritization', 'product']
assignees: ''
---

## ⚠️ IMPORTANT: Product Prioritization Workflow

**This is a PRODUCT PRIORITIZATION request.**

@copilot **MUST** follow the Product Prioritization workflow in `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`.

---

## Prioritization Request

Request automated prioritization of items in `/product/backlog/` according to established policy.

**@copilot**: Execute the Product Prioritization Workflow to update `/product/prioritization.md`.

---

## Prioritization Focus (Optional)

Leave blank for standard prioritization, or specify focus area:

- [ ] Security-focused (prioritize security vulnerabilities)
- [ ] Tech debt focus (emphasize technical debt items)
- [ ] User requests (prioritize user-requested features)
- [ ] Standard (no specific focus, apply policy as normal)

## Special Guidance (Optional)

Any special considerations or constraints for this prioritization cycle:

[e.g., "Q1 release deadlines approaching", "New security advisories published", "Customer escalations", etc.]

---

## For @copilot

**Execution Steps:**
1. Read `.team/workflows/PRODUCT_PRIORITIZATION_WORKFLOW.md`
2. Follow Step 1: Understand the Request (note any focus area or guidance above)
3. Follow Step 2: Collect All Backlog Items from `/product/backlog/`
4. Follow Step 3: Apply Selection Criteria (security, tech debt, overrides, standard)
5. Follow Step 4: Generate Prioritization Tables
6. Follow Step 5: Update `/product/prioritization.md`
7. Follow Step 6: Report Completion (comment on this issue with summary)

**Policy Compliance:**
- Maximum 5 selected items
- Security vulnerabilities take precedence
- At least 1 tech debt item (if any exist)
- Honor priority overrides
- Maintain "Assessed But Not Selected" table

**Success Criteria:**
- [ ] All active backlog items reviewed
- [ ] Security risk assessments performed
- [ ] Tech debt policy satisfied
- [ ] Priority overrides processed
- [ ] Prioritization file updated
- [ ] Summary comment posted
