# Modernize File-Scoped Namespaces

**Backlog ID**: techdebt-2025-11-07-modernize-file-scoped-namespaces
**Source**: Tech Debt
**Category**: Code Modernization
**Status**: Active
**Created**: 2025-11-07
**Updated**: 2025-11-08

## Summary

Modernize namespace declarations to use C# 10+ file-scoped namespaces across the codebase, reducing boilerplate and improving code consistency.

## Context

The codebase uses traditional block-scoped namespaces:
```csharp
namespace Uniun.DataFlow
{
    // Entire file content indented
}
```

Modern C# (10+) supports file-scoped namespaces that reduce nesting:
```csharp
namespace Uniun.DataFlow;

// File content at root level
```

This change:
- Reduces indentation by one level throughout the codebase
- Aligns with modern C# practices
- Improves code readability
- Matches the project's `.editorconfig` guidelines

## Implementation Guidance

### Automated Approach (Recommended)

Use the `dotnet format` tool with analyzers:

```bash
# Enable the analyzer
dotnet format analyzers --severity info --no-restore

# Or use IDE refactoring
# Visual Studio: Right-click namespace → Convert to file-scoped namespace
```

### Manual Approach (if needed)

For each file:
1. Change `namespace Foo.Bar {` to `namespace Foo.Bar;`
2. Remove closing brace `}`
3. Reduce indentation of all content by one level

### Before
```csharp
namespace Uniun.DataFlow
{
    public class MyClass
    {
        public void MyMethod()
        {
            // code
        }
    }
}
```

### After
```csharp
namespace Uniun.DataFlow;

public class MyClass
{
    public void MyMethod()
    {
        // code
    }
}
```

## Success Criteria

- [ ] All namespace declarations updated to file-scoped style
- [ ] Code builds successfully
- [ ] All tests passing
- [ ] Code formatter (dotnet format) passes
- [ ] No merge conflicts with ongoing work

## Handover Assets

- No additional assets

## References

- Source analysis: `/research/tech-debt-2025-11-07/findings-report.md`
- C# file-scoped namespaces: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/file-scoped-namespaces
- IDE support: https://learn.microsoft.com/en-us/visualstudio/ide/reference/convert-namespace-to-file-scoped

## Notes

**Effort**: Medium (affects 100+ files but can be largely automated)
**Priority**: Medium
**Value**:
- Reduces boilerplate (removes 2 lines per file + reduces indentation)
- Aligns with modern C# practices
- Improves readability
- Matches project `.editorconfig` standards

**Impact**:
- Low risk - purely stylistic change
- Large diff but mechanical change
- May conflict with ongoing PRs
- Consider coordinating timing with team

**Implementation Tips**:
- Consider doing in phases (by project or directory)
- Run tests after each batch
- Use automated tooling where possible
- May want to coordinate with team to avoid merge conflicts
- Could be split into multiple PRs by project/area

**Multi-Phase Assessment**: 
Yes, recommended to implement in phases:
1. Phase 1: Test projects
2. Phase 2: Core library projects
3. Phase 3: Sample projects

This reduces risk and makes review easier.

---

## Status History

- **2025-11-07**: Created from tech debt analysis
- **2025-11-08**: Migrated to product backlog system
