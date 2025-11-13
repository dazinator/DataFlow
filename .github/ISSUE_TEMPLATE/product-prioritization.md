---
name: Product Backlog Prioritization
about: Request automated prioritization and selection from the product backlog
title: 'Product Backlog Prioritization - [Monthly/Security Focus/etc.]'
labels: ['workflow:product-backlog']
assignees: ''
---

## ⚠️ IMPORTANT: Product Prioritization Workflow

**This is a PRODUCT PRIORITIZATION issue for managing the product backlog.**

@copilot **MUST** follow the Product Prioritization duty in `.team/duties/PRODUCT_PRIORITIZATION_DUTY.md`.

---

## Prioritization Request

Request automated prioritization and selection of items from the product backlog.

**⚠️ IMPORTANT**: The **primary backlog source** is **GitHub issues** with the `workflow:product-backlog` label.

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

**Duty**: Follow `.team/duties/PRODUCT_PRIORITIZATION_DUTY.md` for complete process.

**Quick Reference:**
- Query backlog items with `workflow:product-backlog` label
- Apply prioritization policy (security, tech debt, priority overrides, standard criteria)
- Present prioritization for human review
- After approval: Select top items and move to implementation queue
- Complete self-improvement evaluation before PR review
