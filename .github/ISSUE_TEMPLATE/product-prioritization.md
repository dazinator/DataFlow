---
name: Product Backlog Prioritization
about: Request automated prioritization and selection from the product backlog
title: 'Product Backlog Prioritization - [Monthly/Security Focus/etc.]'
labels: ['workflow:product-backlog']
assignees: ''
---

## Prioritization Request

Request automated prioritization and selection of items from the product backlog.

**⚠️ IMPORTANT**: The **primary backlog source** is **GitHub issues** with the `workflow:product-backlog` label.

**@copilot**: Execute the Product Prioritization Workflow (see `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW.md` for details).

**Configuration**: See `.team/prompts/PRODUCT_PRIORITIZATION_WORKFLOW_PARAMS.md` for limits and parameters.

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

## Expected Deliverables

When prioritization is complete, the following should be delivered:

- [ ] Prioritization analysis posted for review
- [ ] Human confirmation received
- [ ] Implementation queue capacity checked
- [ ] Top-priority items moved to `workflow:implementation` queue
- [ ] `/product/prioritization.md` updated with:
  - Selection summary
  - Selected items (moved to implementation)
  - Remaining backlog items by priority
  - Queue status
- [ ] Summary comment posted to this issue with results

**Note**: Housekeeping (duplicates, completed items, stale items) is handled by Bulk Triage workflow (`.team/prompts/TRIAGE_WORKFLOW.md` Step 6).
