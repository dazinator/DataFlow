# Add EnumeratorCancellation Attributes

**Category**: Code Quality  
**Identified**: 2025-11-07  
**Source**: Tech Debt Analysis - [PR Link TBD]  
**Priority**: Medium  
**Effort**: Small

## Problem

Three instances of CS8425 warnings in async iterators where cancellation token parameters lack the `[EnumeratorCancellation]` attribute. This means the cancellation token from `GetAsyncEnumerator()` won't be properly propagated.

Warning:
```
CS8425: Async-iterator has one or more parameters of type 'CancellationToken' 
but none of them is decorated with the 'EnumeratorCancellation' attribute
```

Affected files:
- `src/Tests.Shared/Transformers/NumberTransformer.cs`
- `src/Tests.Shared/Transformers/TestProjector.cs`
- `sample/Otel.Example/Flows/ExampleFlow.cs`

## Proposed Solution

Add `[EnumeratorCancellation]` attribute to cancellation token parameters in async iterators.

### Before
```csharp
public async IAsyncEnumerable<T> ProduceAsync(CancellationToken cancellationToken)
{
    // CancellationToken from GetAsyncEnumerator() will be unconsumed
}
```

### After
```csharp
public async IAsyncEnumerable<T> ProduceAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // Now properly propagates cancellation from GetAsyncEnumerator()
}
```

## Value

### Benefits
- Proper cancellation token propagation semantics
- Follows C# async/await best practices
- Eliminates 3 compiler warnings
- Ensures cancellation works correctly in all scenarios

### Impact
- Low risk - additive change only
- Improves cancellation behavior
- No breaking changes

## References

- Original analysis: /research/tech-debt-2025-11-07/findings-report.md (Finding TD-003)
- CS8425 documentation: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs8425
- EnumeratorCancellation attribute: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.enumeratorcancellationattribute

## Implementation Guidance

1. Add `using System.Runtime.CompilerServices;` to affected files
2. Add `[EnumeratorCancellation]` attribute to each cancellation token parameter
3. Verify tests still pass (especially cancellation tests)
4. Confirm CS8425 warnings are eliminated

Quick win - takes ~15 minutes to implement and test.

## Status
- [ ] Not started
- [ ] In progress
- [ ] Completed (PR: #[N])

## Notes

This is a "quick win" - small effort with clear value. Good for new contributors or as part of a larger cleanup effort.
