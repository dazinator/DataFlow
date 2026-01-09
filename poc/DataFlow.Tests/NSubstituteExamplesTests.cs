namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Demonstrates the value of NSubstitute for testing DataFlow actors with dependencies.
/// Compares manual mocking vs NSubstitute approach.
/// </summary>
public class NSubstituteExamplesTests
{
    #region Domain Models and Interfaces

    public record Transaction(string AccountId, decimal Amount, DateTime Date);
    public record ProcessedTransaction(string AccountId, decimal Amount, DateTime Date, bool IsValid);

    public interface ITransactionValidator
    {
        Task<bool> ValidateAsync(Transaction transaction, CancellationToken ct);
    }

    public interface ITransactionRepository
    {
        Task SaveAsync(ProcessedTransaction transaction, CancellationToken ct);
        Task<int> GetTransactionCountAsync(string accountId, CancellationToken ct);
    }

    #endregion

    #region Example Actors with Dependencies

    /// <summary>
    /// Actor that validates transactions using an external service.
    /// </summary>
    public class ValidationActor : IStreamActor<Transaction, ProcessedTransaction>
    {
        private readonly ITransactionValidator _validator;

        public ValidationActor(ITransactionValidator validator)
        {
            _validator = validator;
        }

        public async IAsyncEnumerable<ProcessedTransaction> RunAsync(
            IAsyncEnumerable<Transaction> input,
            IActorExecutionContext context)
        {
            await foreach (var transaction in input.WithCancellation(context.CancellationToken))
            {
                var isValid = await _validator.ValidateAsync(transaction, context.CancellationToken);
                yield return new ProcessedTransaction(
                    transaction.AccountId,
                    transaction.Amount,
                    transaction.Date,
                    isValid);
            }
        }
    }

    /// <summary>
    /// Actor that saves transactions and has side effects.
    /// </summary>
    public class SaveActor : IStreamActor<ProcessedTransaction, string>
    {
        private readonly ITransactionRepository _repository;

        public SaveActor(ITransactionRepository repository)
        {
            _repository = repository;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<ProcessedTransaction> input,
            IActorExecutionContext context)
        {
            await foreach (var transaction in input.WithCancellation(context.CancellationToken))
            {
                if (transaction.IsValid)
                {
                    await _repository.SaveAsync(transaction, context.CancellationToken);
                    yield return $"Saved: {transaction.AccountId}";
                }
                else
                {
                    yield return $"Skipped: {transaction.AccountId}";
                }
            }
        }
    }

    #endregion

    #region Example 1: BEFORE - Manual Mock (Verbose)

    /// <summary>
    /// Manual mock implementation - requires 20-30 lines of boilerplate.
    /// </summary>
    public class ManualMockValidator : ITransactionValidator
    {
        private readonly Dictionary<string, bool> _validationResults = new();
        public List<Transaction> ValidatedTransactions { get; } = new();

        public void SetupValidation(string accountId, bool isValid)
        {
            _validationResults[accountId] = isValid;
        }

        public Task<bool> ValidateAsync(Transaction transaction, CancellationToken ct)
        {
            ValidatedTransactions.Add(transaction);
            return Task.FromResult(_validationResults.GetValueOrDefault(transaction.AccountId, true));
        }
    }

    [Fact]
    public async Task BEFORE_ValidationActor_With_Manual_Mock_Verbose()
    {
        // Arrange - Manual mock setup (verbose!)
        var manualMock = new ManualMockValidator();
        manualMock.SetupValidation("ACC-001", true);
        manualMock.SetupValidation("ACC-002", false);

        var actor = new ValidationActor(manualMock);
        var input = TestStreams.FromArray(
            new Transaction("ACC-001", 100m, DateTime.Now),
            new Transaction("ACC-002", 200m, DateTime.Now)
        );

        // Act
        var results = await TestStreams.CollectAsync(
            actor.RunAsync(input, TestContext.CreateActor()));

        // Assert
        results.Count.ShouldBe(2);
        results[0].IsValid.ShouldBeTrue();
        results[1].IsValid.ShouldBeFalse();

        // Verify calls manually
        manualMock.ValidatedTransactions.Count.ShouldBe(2);
        manualMock.ValidatedTransactions[0].AccountId.ShouldBe("ACC-001");
    }

    #endregion

    #region Example 2: AFTER - NSubstitute (Concise)

    [Fact]
    public async Task AFTER_ValidationActor_With_NSubstitute_Concise()
    {
        // Arrange - NSubstitute setup (much cleaner!)
        var mockValidator = Substitute.For<ITransactionValidator>();
        mockValidator.ValidateAsync(
            Arg.Is<Transaction>(t => t.AccountId == "ACC-001"),
            Arg.Any<CancellationToken>())
            .Returns(true);
        mockValidator.ValidateAsync(
            Arg.Is<Transaction>(t => t.AccountId == "ACC-002"),
            Arg.Any<CancellationToken>())
            .Returns(false);

        var actor = new ValidationActor(mockValidator);
        var input = TestStreams.FromArray(
            new Transaction("ACC-001", 100m, DateTime.Now),
            new Transaction("ACC-002", 200m, DateTime.Now)
        );

        // Act
        var results = await TestStreams.CollectAsync(
            actor.RunAsync(input, TestContext.CreateActor()));

        // Assert
        results.Count.ShouldBe(2);
        results[0].IsValid.ShouldBeTrue();
        results[1].IsValid.ShouldBeFalse();

        // Verify calls with NSubstitute - more powerful!
        await mockValidator.Received(2).ValidateAsync(
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
        await mockValidator.Received(1).ValidateAsync(
            Arg.Is<Transaction>(t => t.AccountId == "ACC-001"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Example 3: Verifying Side Effects

    [Fact]
    public async Task SaveActor_Should_Only_Save_Valid_Transactions()
    {
        // Arrange
        var mockRepository = Substitute.For<ITransactionRepository>();

        var actor = new SaveActor(mockRepository);
        var input = TestStreams.FromArray(
            new ProcessedTransaction("ACC-001", 100m, DateTime.Now, true),
            new ProcessedTransaction("ACC-002", 200m, DateTime.Now, false),
            new ProcessedTransaction("ACC-003", 300m, DateTime.Now, true)
        );

        // Act
        var results = await TestStreams.CollectAsync(
            actor.RunAsync(input, TestContext.CreateActor()));

        // Assert - Verify only valid transactions were saved
        await mockRepository.Received(2).SaveAsync(
            Arg.Is<ProcessedTransaction>(t => t.IsValid),
            Arg.Any<CancellationToken>());

        // Verify specific transactions
        await mockRepository.Received().SaveAsync(
            Arg.Is<ProcessedTransaction>(t => t.AccountId == "ACC-001"),
            Arg.Any<CancellationToken>());
        await mockRepository.Received().SaveAsync(
            Arg.Is<ProcessedTransaction>(t => t.AccountId == "ACC-003"),
            Arg.Any<CancellationToken>());

        // Verify invalid transaction was NOT saved
        await mockRepository.DidNotReceive().SaveAsync(
            Arg.Is<ProcessedTransaction>(t => t.AccountId == "ACC-002"),
            Arg.Any<CancellationToken>());

        results.ShouldContain("Saved: ACC-001");
        results.ShouldContain("Skipped: ACC-002");
        results.ShouldContain("Saved: ACC-003");
    }

    #endregion

    #region Example 4: Testing Error Handling

    [Fact]
    public async Task ValidationActor_Should_Propagate_Service_Errors()
    {
        // Arrange - Simulate service failure
        var mockValidator = Substitute.For<ITransactionValidator>();
        mockValidator.ValidateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("Validation service unavailable"));

        var actor = new ValidationActor(mockValidator);
        var input = TestStreams.FromArray(
            new Transaction("ACC-001", 100m, DateTime.Now)
        );

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await TestStreams.CollectAsync(
                actor.RunAsync(input, TestContext.CreateActor()));
        });
    }

    #endregion

    #region Example 5: Combining with TestServiceBuilder

    [Fact]
    public async Task TestServiceBuilder_Works_Seamlessly_With_NSubstitute()
    {
        // Arrange - Use TestServiceBuilder with NSubstitute mock
        var mockValidator = Substitute.For<ITransactionValidator>();
        mockValidator.ValidateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // TestServiceBuilder makes DI setup clean even with mocks
        var scopeFactory = TestServiceBuilder.Create()
            .WithActor<ValidationActor>()
            .WithScoped(mockValidator)
            .BuildScopeFactory();

        // This demonstrates the pattern works well in real scenarios
        scopeFactory.ShouldNotBeNull();

        // Verify mock can be used to check actor behavior
        var scope = scopeFactory.CreateScope();
        var actor = scope.ServiceProvider.GetService(typeof(ValidationActor)) as ValidationActor;
        actor.ShouldNotBeNull();
    }

    #endregion

    #region Example 6: Business Logic Decoupling Pattern

    // Recommended pattern: Extract business logic to service
    public interface ITransactionProcessor
    {
        ProcessedTransaction Process(Transaction transaction, bool isValid);
    }

    public class BusinessLogicActor : IStreamActor<Transaction, ProcessedTransaction>
    {
        private readonly ITransactionValidator _validator;
        private readonly ITransactionProcessor _processor;

        public BusinessLogicActor(ITransactionValidator validator, ITransactionProcessor processor)
        {
            _validator = validator;
            _processor = processor;
        }

        public async IAsyncEnumerable<ProcessedTransaction> RunAsync(
            IAsyncEnumerable<Transaction> input,
            IActorExecutionContext context)
        {
            await foreach (var transaction in input.WithCancellation(context.CancellationToken))
            {
                var isValid = await _validator.ValidateAsync(transaction, context.CancellationToken);
                yield return _processor.Process(transaction, isValid);
            }
        }
    }

    [Fact]
    public async Task Decoupled_Pattern_Easy_To_Test_With_Mocks()
    {
        // Arrange - Mock both dependencies
        var mockValidator = Substitute.For<ITransactionValidator>();
        mockValidator.ValidateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var mockProcessor = Substitute.For<ITransactionProcessor>();
        mockProcessor.Process(Arg.Any<Transaction>(), Arg.Any<bool>())
            .Returns(x => new ProcessedTransaction(
                ((Transaction)x[0]).AccountId,
                ((Transaction)x[0]).Amount,
                ((Transaction)x[0]).Date,
                (bool)x[1]));

        var actor = new BusinessLogicActor(mockValidator, mockProcessor);
        var input = TestStreams.FromArray(
            new Transaction("ACC-001", 100m, DateTime.Now)
        );

        // Act
        var results = await TestStreams.CollectAsync(
            actor.RunAsync(input, TestContext.CreateActor()));

        // Assert - Verify orchestration
        results.Count.ShouldBe(1);

        // Verify actor called both services correctly
        await mockValidator.Received(1).ValidateAsync(
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
        mockProcessor.Received(1).Process(
            Arg.Any<Transaction>(),
            Arg.Is<bool>(v => v == true));
    }

    #endregion

    #region Value Summary

    /// <summary>
    /// CODE COMPARISON SUMMARY:
    /// 
    /// MANUAL MOCK:
    /// - Manual mock class: 15-20 lines
    /// - Setup: 3-5 lines
    /// - Verification: Manual inspection of captured calls
    /// - Total: ~25-30 lines per test
    /// 
    /// NSUBSTITUTE:
    /// - Setup: 3-5 lines
    /// - Verification: Powerful Received() API
    /// - Total: ~10-15 lines per test
    /// 
    /// IMPROVEMENT: 50-60% code reduction + better verification
    /// 
    /// KEY BENEFITS:
    /// 1. Less boilerplate (no manual mock classes)
    /// 2. Powerful verification (Received, DidNotReceive, Arg.Is)
    /// 3. Easy error simulation
    /// 4. Clear test intent
    /// 5. Perfect fit for business logic decoupling pattern
    /// </summary>

    #endregion
}
