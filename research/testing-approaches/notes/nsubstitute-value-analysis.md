# NSubstitute Value Analysis for DataFlow Tests

## Current State

**Production Tests**: NSubstitute package is referenced but not actively used
**POC Tests**: No mocking library present

**Current Mocking Approach**: Manual mock implementations (e.g., `InMemoryCashFlowDatabase`)

## Where NSubstitute Adds Value

### 1. Testing Actors with Service Dependencies ✅ HIGH VALUE

**Problem**: Actors that depend on external services require manual mock implementations.

**Example from RealWorldScenarioTests**:
```csharp
// Current: Manual mock implementation (~40 lines)
public class InMemoryCashFlowDatabase : ICashFlowDatabase
{
    private readonly HashSet<(string, DateTime)> _existingAggregates = new();
    private readonly List<CashFlowAggregate> _createdAggregates = new();
    
    public Task<bool> AggregateExistsAsync(string companyCode, DateTime valueDate, CancellationToken ct)
    {
        return Task.FromResult(_existingAggregates.Contains((companyCode, valueDate)));
    }
    
    public Task CreateAggregateAsync(CashFlowAggregate aggregate, CancellationToken ct)
    {
        _createdAggregates.Add(aggregate);
        return Task.CompletedTask;
    }
    
    public List<CashFlowAggregate> GetCreatedAggregates() => _createdAggregates;
}
```

**With NSubstitute** (~10 lines):
```csharp
var mockDatabase = Substitute.For<ICashFlowDatabase>();

// Setup behavior
mockDatabase.AggregateExistsAsync("COMP-B", new DateTime(2024, 1, 1), Arg.Any<CancellationToken>())
    .Returns(true);
mockDatabase.AggregateExistsAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
    .Returns(false);

// Verify calls
await mockDatabase.Received(2).CreateAggregateAsync(Arg.Any<CashFlowAggregate>(), Arg.Any<CancellationToken>());
```

**Benefits**:
- 75% less code
- Verification of method calls (Received)
- Easy to set up different return values
- No need to maintain mock state manually

### 2. Verifying Database Interactions ✅ HIGH VALUE

**Problem**: Testing that actors correctly call database methods with expected parameters.

**Example - DatabaseCheckActor Testing**:
```csharp
[Fact]
public async Task DatabaseCheckActor_Should_Call_Database_For_Each_Aggregate()
{
    // Arrange
    var mockDatabase = Substitute.For<ICashFlowDatabase>();
    mockDatabase.AggregateExistsAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
        .Returns(false);
    
    var actor = new DatabaseCheckActor(mockDatabase);
    var input = TestStreams.FromArray(
        new CashFlowAggregate("COMP-A", date1, 100m, "USD", 1),
        new CashFlowAggregate("COMP-B", date2, 200m, "USD", 1)
    );
    
    // Act
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, TestContext.CreateActor()));
    
    // Assert
    await mockDatabase.Received(2).AggregateExistsAsync(
        Arg.Any<string>(), 
        Arg.Any<DateTime>(), 
        Arg.Any<CancellationToken>());
    
    // Verify specific calls
    await mockDatabase.Received().AggregateExistsAsync("COMP-A", date1, Arg.Any<CancellationToken>());
    await mockDatabase.Received().AggregateExistsAsync("COMP-B", date2, Arg.Any<CancellationToken>());
}
```

**Value**: Can verify exact method calls with specific parameters, not just final state.

### 3. Testing Error Handling ✅ MEDIUM VALUE

**Problem**: Testing how actors handle service failures.

**Example**:
```csharp
[Fact]
public async Task ConditionalCreateActor_Should_Handle_Database_Errors_Gracefully()
{
    // Arrange
    var mockDatabase = Substitute.For<ICashFlowDatabase>();
    mockDatabase.CreateAggregateAsync(Arg.Any<CashFlowAggregate>(), Arg.Any<CancellationToken>())
        .Returns(x => throw new InvalidOperationException("Database connection failed"));
    
    var actor = new ConditionalCreateActor(mockDatabase);
    var input = TestStreams.FromArray(
        new AggregateCheckResult(aggregate, false)
    );
    
    // Act & Assert
    await Should.ThrowAsync<InvalidOperationException>(async () =>
    {
        await TestStreams.CollectAsync(actor.RunAsync(input, TestContext.CreateActor()));
    });
}
```

**Value**: Easy to simulate errors without complex setup.

### 4. Testing Business Logic Decoupling Pattern ✅ HIGH VALUE

**Problem**: When following the recommended pattern of extracting business logic into services, need to verify actor correctly calls service.

**Example**:
```csharp
// Following our recommended pattern
public class AggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    private readonly IAggregationService _service;
    
    public AggregatorActor(IAggregationService service)
    {
        _service = service;
    }
    
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        var groups = await CollectGroupsAsync(input);
        foreach (var (key, flows) in groups)
        {
            yield return _service.CreateAggregate(key.Company, key.Date, flows);
        }
    }
}

[Fact]
public async Task AggregatorActor_Should_Call_Service_For_Each_Group()
{
    // Arrange
    var mockService = Substitute.For<IAggregationService>();
    mockService.CreateAggregate(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<List<CashFlow>>())
        .Returns(x => new CashFlowAggregate(
            (string)x[0], 
            (DateTime)x[1], 
            ((List<CashFlow>)x[2]).Sum(f => f.Amount),
            "USD", 
            ((List<CashFlow>)x[2]).Count));
    
    var actor = new AggregatorActor(mockService);
    var input = TestStreams.FromArray(
        new CashFlow("COMP-A", date1, 100m, "USD"),
        new CashFlow("COMP-A", date1, 50m, "USD")
    );
    
    // Act
    await TestStreams.CollectAsync(actor.RunAsync(input, TestContext.CreateActor()));
    
    // Assert - Verify service was called correctly
    mockService.Received(1).CreateAggregate("COMP-A", date1, Arg.Is<List<CashFlow>>(l => l.Count == 2));
}
```

**Value**: Perfect fit for testing the recommended decoupling pattern - verify orchestration without implementing business logic.

### 5. Testing Concurrent Access Scenarios ✅ MEDIUM VALUE

**Problem**: Verifying thread-safe behavior with scoped services.

**Example**:
```csharp
[Fact]
public async Task Actor_Should_Use_Separate_Service_Instance_Per_Concurrent_Execution()
{
    var callCount = 0;
    var mockService = Substitute.For<IMyService>();
    mockService.ProcessAsync(Arg.Any<int>())
        .Returns(x => 
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(x.Arg<int>() * 2);
        });
    
    // Test with concurrent execution...
    
    // Verify total calls match expected concurrency
    mockService.ReceivedWithAnyArgs(100).ProcessAsync(default);
}
```

## Where NSubstitute Has Limited Value

### 1. Pure Integration Tests ❌ LOW VALUE

**Scenario**: Testing full pipelines end-to-end.

**Reason**: Integration tests should use real or in-memory implementations, not mocks. Mocks would defeat the purpose.

**Example**: Complete cashflow pipeline test should use InMemoryCashFlowDatabase, not mock.

### 2. Simple Transform Actors ❌ NO VALUE

**Scenario**: Actors with no dependencies doing pure transformations.

**Reason**: No services to mock.

**Example**: 
```csharp
public class IntToStringActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(...)
    {
        await foreach (var item in input)
            yield return $"Item-{item}";
    }
}
```

No dependencies = no need for mocking.

### 3. Test Helpers Themselves ❌ NO VALUE

**Scenario**: Testing `CollectorActor<T>`, `TestStreams`, etc.

**Reason**: These are simple utilities with no external dependencies.

## Concrete Recommendations

### 1. Add NSubstitute to POC Tests

**Action**: Add package reference to `DataFlow.POC.Tests.csproj`

```xml
<PackageReference Include="NSubstitute" Version="5.1.0" />
```

### 2. Create Examples Showing NSubstitute Value

**Location**: Add to research notes or create separate examples file

**Examples to Add**:
1. Testing DatabaseCheckActor with mocked database
2. Testing ConditionalCreateActor with mock verification
3. Testing business logic decoupling pattern with service mocks
4. Error handling scenarios

### 3. Update Testing Guide

**Add section**: "When to Use Mocking Libraries"

**Guidance**:
- ✅ Use NSubstitute for: Service dependencies, behavior verification, error scenarios
- ❌ Don't use for: Integration tests, simple actors, test utilities
- Pattern: Combine with test helpers (TestServiceBuilder can register mocks)

### 4. Update TestServiceBuilder

**Enhancement**: Make it easy to register mocks

```csharp
public TestServiceBuilder WithMock<TService>(TService mock) where TService : class
{
    _services.AddScoped<TService>(_ => mock);
    return this;
}

// Usage
var mockDb = Substitute.For<ICashFlowDatabase>();
var scopeFactory = TestServiceBuilder.Create()
    .WithActor<DatabaseCheckActor>()
    .WithMock(mockDb)
    .BuildScopeFactory();
```

## Impact Assessment

**Code Reduction**: 
- Manual mocks: ~30-40 lines → 5-10 lines with NSubstitute
- Additional reduction: 70-75%

**Test Quality**:
- ✅ Better verification (Received() assertions)
- ✅ Clearer test intent
- ✅ Easier error simulation
- ✅ Better for decoupling pattern

**When to Use**:
- **Always**: Testing actors with service dependencies
- **Often**: Behavior verification, error handling
- **Never**: Integration tests, simple actors

## Conclusion

**NSubstitute adds significant value** to DataFlow tests, especially:
1. Testing actors with service dependencies (75% less code)
2. Verifying database/service interactions
3. Supporting the business logic decoupling pattern
4. Testing error scenarios

**Recommendation**: 
- Add NSubstitute to POC tests
- Create concrete examples in research
- Update testing guide with mocking guidance
- Enhance TestServiceBuilder for mock registration

**Priority**: MEDIUM-HIGH - Not critical but provides clear value, especially for the patterns we're recommending.
