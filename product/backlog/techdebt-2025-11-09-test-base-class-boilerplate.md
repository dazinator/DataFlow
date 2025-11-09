# Reduce Test Boilerplate with Base Test Class

**Backlog ID**: techdebt-2025-11-09-test-base-class-boilerplate
**Source**: Tech Debt
**Category**: Developer Experience
**Status**: Active
**Created**: 2025-11-09
**Updated**: 2025-11-09

## Summary

Create a base test class to eliminate 8-12 lines of repeated boilerplate in ~50-60 test classes, removing ~500 lines of duplicated code and improving test readability.

## Context

**Source**: Tech debt analysis - `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-002)
**Priority**: High
**Effort**: Medium

Every test class currently repeats the same dependency injection setup pattern:

```csharp
public ITestOutputHelper Output { get; }
public ServiceCollection Services { get; }

public FooTests(ITestOutputHelper output)
{
    Output = output;
    Services = new ServiceCollection();
    AddDefaultServices();
}

private void AddDefaultServices()
{
    Services.AddLogging(builder => builder.AddXUnit(Output));
    Services.AddDataFlowMetrics();
    Services.AddDataFlows();
}
```

This pattern is repeated across 50+ test classes, making tests harder to read, harder to maintain, and error-prone when the standard setup needs to change.

**Who is affected**: All developers writing or maintaining tests

## Implementation Guidance

Create a base test class that encapsulates the standard test setup, then migrate test classes incrementally to inherit from it.

### Approach

**Phase 1: Create base class + pilot migration**
1. Create `DataFlowTestBase` class in Tests.Shared:
   ```csharp
   public abstract class DataFlowTestBase
   {
       protected ITestOutputHelper Output { get; }
       protected ServiceCollection Services { get; }
       
       protected DataFlowTestBase(ITestOutputHelper output)
       {
           Output = output;
           Services = new ServiceCollection();
           AddDefaultServices();
       }
       
       protected virtual void AddDefaultServices()
       {
           Services.AddLogging(builder => builder.AddXUnit(Output));
           Services.AddDataFlowMetrics();
           Services.AddDataFlows();
       }
   }
   ```

2. Migrate 5-10 test classes to validate approach
3. Run tests to ensure no regressions

**Phase 2: Migrate remaining test classes**
4. Batch-convert remaining 40-50 test classes
5. Run full test suite to verify

### Files Affected

- `src/Tests.Shared/` - New base class
- `src/Tests/DataFlow/*.cs` - ~50-60 test files to update

### Multi-Phase Implementation

**Recommended**: Multi-phase approach
**Rationale**: 50+ test files affected. Incremental migration reduces risk and allows validation of approach before full rollout.

**Suggested Phases**:
- Phase 1: Create base class + migrate 5-10 test files to validate (2-3 hours)
- Phase 2: Migrate remaining test files in batches (3-4 hours)

### External Dependencies

No external dependencies required.

## Success Criteria

- [ ] DataFlowTestBase class created in Tests.Shared
- [ ] Base class provides standard Output and Services setup
- [ ] All test classes inherit from DataFlowTestBase (or have specific reason not to)
- [ ] ~500 lines of boilerplate removed from test files
- [ ] All 180 tests still pass
- [ ] Test classes are more readable
- [ ] Single place to update common test dependencies
- [ ] Existing documentation covers this functionality (no new docs needed)

## Handover Assets

No additional assets.

## References

- Tech debt analysis: `/research/tech-debt-2025-11-09/findings-report.md` (Finding TD-002)
- Example test files showing current pattern:
  - `src/Tests/DataFlow/MonitoredChannelTests.cs`
  - `src/Tests/DataFlow/BatchBlockTests.cs`
  - `src/Tests/DataFlow/DataFlowContextTests.cs`

## Notes

**Developer Experience**: This significantly improves test readability and maintainability. New contributors will have less boilerplate to understand and write.

**Flexibility**: The `virtual AddDefaultServices()` method allows test classes to override or extend the default setup if needed.

**Incremental Adoption**: Can be done gradually. Test classes can be migrated one at a time without affecting others.

---

## Status History

- **2025-11-09**: Created from tech debt analysis
