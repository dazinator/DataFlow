# API Reference

## Overview

This folder will contain detailed API reference documentation for the epoch-based coordination system.

## Planned Structure

```
reference/
├── interfaces/
│   ├── IEpochLifecycleParticipant.md
│   ├── IBlockContext.md
│   ├── IEpochStream.md
│   └── ISourceActor.md
├── classes/
│   ├── EpochVector.md
│   ├── EpochLifecycleCoordinator.md
│   ├── GlobalEpochAlignment.md
│   └── BlockContext.md
└── patterns/
    ├── EntityTrackingBlock.md
    └── SourceActorBase.md
```

## Generation Strategy

API reference documentation should be:
- Generated from XML comments in code where possible
- Kept synchronized with actual implementations
- Updated with each API change
- Supplemented with usage examples

## Current Status

🚧 **Under Construction** - API reference documentation is planned but not yet generated.

For now, refer to:
- [Concepts](../concepts/) - Conceptual understanding
- [Guides](../guides/) - Implementation patterns
- Source code XML comments - Inline documentation

## Contributing

When adding new APIs:
1. Add XML comments to the code
2. Generate or write reference documentation
3. Link from relevant concept or guide documents
4. Update this README with new entries
