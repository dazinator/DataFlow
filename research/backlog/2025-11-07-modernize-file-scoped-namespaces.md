# Modernize to File-Scoped Namespaces

**Category**: Modern C# Practices  
**Identified**: 2025-11-07  
**Source**: Tech Debt Analysis - [PR Link TBD]  
**Priority**: Medium  
**Effort**: Large (but automatable)

## Problem

91% of C# files (283 out of 310) use traditional block-scoped namespaces instead of modern file-scoped namespaces introduced in C# 10.

Current pattern:
```csharp
namespace Uniun.DataFlow.Blocks
{
    public class MyBlock 
    {
        // Class content
    }
}
```

Modern pattern:
```csharp
namespace Uniun.DataFlow.Blocks;

public class MyBlock 
{
    // Class content
}
```

## Proposed Solution

Convert all namespaces to file-scoped declarations using automated refactoring tools.

### Approach
1. Configure `.editorconfig` to prefer file-scoped namespaces
2. Use Visual Studio or ReSharper bulk refactoring across entire solution
3. Review sample of changes manually
4. Run full test suite

### Automation Options
- Visual Studio: "Convert to file-scoped namespace" refactoring
- ReSharper: Bulk cleanup with namespace style rule
- dotnet format: With appropriate configuration

## Value

### Benefits
- Reduces indentation by 1 level throughout codebase
- More modern, idiomatic C# code
- Follows .NET coding conventions
- Reduces LOC by ~600 lines (2 per file)
- Improved readability with less nesting

### Impact Areas
- All 283 files using block-scoped namespaces
- Primarily POC and src code
- No behavioral changes - purely syntactic

## References

- Original analysis: /research/tech-debt-2025-11-07/findings-report.md (Finding TD-004)
- Affected: poc/**/*.cs and src/**/*.cs (283 files)
- C# Feature: File-scoped namespaces (C# 10)

## Implementation Guidance

When implementing:
1. Ensure .NET 8 SDK is used (already is)
2. Configure .editorconfig first to prevent regression
3. Use automated tool to minimize errors
4. Run all tests to verify no issues
5. Consider doing in phases (e.g., POC first, then src)

## Status
- [ ] Not started
- [ ] In progress
- [ ] Completed (PR: #[N])

## Notes

This is a large-scope change but very low risk due to being purely syntactic. Can be fully automated. Consider as a good opportunity to familiarize new contributors with the codebase structure.
