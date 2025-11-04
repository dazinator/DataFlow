# Phase 7 Proposed Documentation

This folder contains documentation that is proposed for integration into the main POC documentation structure **after PR approval**.

## Documents for Promotion

### ✅ research-findings.md
**Destination**: `/poc/docs/research/event-channel-node-poc-findings.md`  
**Status**: Already promoted (valuable regardless of pivot)  
**Content**: Comprehensive analysis of both channel-based and hybrid coordinator-wrapped approaches

### ✅ glossary-additions.md
**Destination**: Merge into `/poc/docs/POC_GLOSSARY.md` and `/poc/docs/RESEARCH_GLOSSARY.md`  
**Status**: Ready for promotion after PR approval  
**Content**:
- Terms to add to main glossary (EpochLifecycleNode - recommended for adoption)
- Terms to add to research glossary (EventChannelNode and related dismissed concepts)

## Documents Moved to Archived

The following documents were moved to `../archived/` after the pivot decision:

### ❌ adr-event-type-handling.md
**Moved to**: `../archived/adr-event-type-handling-dismissed.md`  
**Reason**: Separate channels per event type approach dismissed due to ordering concerns

### ❌ design-event-channel.md
**Moved to**: `../archived/design-event-channel-dismissed.md`  
**Reason**: Channel-based EventChannelNode approach dismissed due to complexity and over-engineering

## Promotion Workflow

After PR approval:

1. **Merge glossary-additions.md content**:
   ```bash
   # Add EpochLifecycleNode term to main glossary
   # Add dismissed terms to research glossary
   ```

2. **research-findings.md already in correct location** (no action needed)

3. **Remove this folder** once promotion is complete

See [POC_RESEARCH_WORKFLOW.md](/poc/docs/POC_RESEARCH_WORKFLOW.md) for complete workflow details.
