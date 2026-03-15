namespace DataFlow.POC.Tests;

using System.Runtime.CompilerServices;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Regression tests for terminal block execution in DataFlow graphs.
///
/// Issue: Terminal blocks (blocks with no outgoing connections) were never having
/// their RunAsync() method fully executed. The root cause was that
/// ReflectionHelper.EnumerateTypedStreamGenericAsync&lt;T&gt; only consumed the outer
/// IAsyncEnumerable&lt;IEpochStream&lt;TOut&gt;&gt; stream but never enumerated the inner
/// items within each epoch stream, so the actor's lazy RunAsync was never driven
/// to completion.
/// </summary>
public class TerminalBlockExecutionTests
{
    // ====================================================================
    // Actors – integer type
    // ====================================================================

    private class IntegerSource : SourceActorBase<int>
    {
        public IntegerSource() : base("int-source") { }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            for (int i = 1; i <= 3; i++)
            {
                var items = YieldItem(i);
                yield return await CreateEpochStreamAsync(context, i, items, context.CancellationToken);
            }
        }

        private async IAsyncEnumerable<int> YieldItem(int value)
        {
            yield return value;
        }
    }

    private class IntegerDoubler : IStreamActor<int, int>
    {
        private readonly List<int> _results;

        public IntegerDoubler(List<int> results) => _results = results;

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var doubled = item * 2;
                _results.Add(doubled);
                yield return doubled;
            }
        }
    }

    private class IntegerTerminal : IStreamActor<int, int>
    {
        private readonly List<int> _results;

        public IntegerTerminal(List<int> results) => _results = results;

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _results.Add(item);
                yield return item;
            }
        }
    }

    // ====================================================================
    // Actors – custom reference type
    // ====================================================================

    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class PersonSource : SourceActorBase<Person>
    {
        public PersonSource() : base("person-source") { }

        public override async IAsyncEnumerable<IEpochStream<Person>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            var people = new[]
            {
                new Person { Name = "Alice", Age = 30 },
                new Person { Name = "Bob",   Age = 25 },
                new Person { Name = "Charlie", Age = 35 }
            };

            for (int i = 0; i < people.Length; i++)
            {
                var person = people[i];
                var items = YieldItem(person);
                yield return await CreateEpochStreamAsync(context, i + 1, items, context.CancellationToken);
            }
        }

        private async IAsyncEnumerable<Person> YieldItem(Person person)
        {
            yield return person;
        }
    }

    private class PersonTerminal : IStreamActor<Person, Person>
    {
        private readonly List<Person> _results;

        public PersonTerminal(List<Person> results) => _results = results;

        public async IAsyncEnumerable<Person> RunAsync(
            IAsyncEnumerable<Person> input,
            IActorExecutionContext context)
        {
            await foreach (var person in input.WithCancellation(context.CancellationToken))
            {
                _results.Add(person);
                yield return person;
            }
        }
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static (DataFlowGraph graph, ExecutionContext context) BuildGraph(
        string name,
        IServiceProvider serviceProvider,
        Action<TestGraphBuilder> configure)
    {
        var builder = GraphHelpers.CreateGraphBuilder(name);
        var testBuilder = new TestGraphBuilder(builder);
        configure(testBuilder);
        var graph = builder.Build(serviceProvider, new BlockTypeRegistry());
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        return (graph, context);
    }

    private sealed class TestGraphBuilder
    {
        private readonly DataFlow.POC.Builder.DataFlowGraphBuilder _inner;
        public TestGraphBuilder(DataFlow.POC.Builder.DataFlowGraphBuilder inner) => _inner = inner;
        public TestGraphBuilder AddBlock(IBlock block) { _inner.AddBlock(block); return this; }
        public TestGraphBuilder Connect(IBlock source, IBlock target) { _inner.Connect(source, target); return this; }
    }

    // ====================================================================
    // TEST 1: Two-block graph (source → terminal) with int type
    // ====================================================================

    [Fact]
    public async Task TwoBlockGraph_IntegerType_TerminalExecutesAndCollectsAllItems()
    {
        // Arrange
        var terminalResults = new List<int>();

        var coordinator = new EpochCoordinator(
            TestServiceBuilder.Create().Build().GetRequiredService<IServiceScopeFactory>());

        var source = BlockHelpers.CreateEpochSource<int, IntegerSource>(
            "source",
            TestServiceBuilder.Create().WithScoped(new IntegerSource()).BuildScopeFactory(),
            coordinator);

        var terminal = BlockHelpers.CreateEpochActor<int, int, IntegerTerminal>(
            "terminal",
            new IntegerTerminal(terminalResults));

        var serviceProvider = TestServiceBuilder.Create().Build();
        var (graph, context) = BuildGraph("test-2block-int", serviceProvider, b =>
            b.AddBlock(source)
             .AddBlock(terminal)
             .Connect(source, terminal));

        // Act
        await graph.ExecuteAsync(context);

        // Assert – terminal must have processed all 3 items
        terminalResults.Count.ShouldBe(3);
        terminalResults.ShouldBe(new[] { 1, 2, 3 });

        await coordinator.DisposeAsync();
        await ((IAsyncDisposable)serviceProvider).DisposeAsync();
    }

    // ====================================================================
    // TEST 2: Three-block graph (source → middle → terminal) with int type
    // ====================================================================

    [Fact]
    public async Task ThreeBlockGraph_IntegerType_MiddleAndTerminalBothExecute()
    {
        // Arrange
        var middleResults = new List<int>();
        var terminalResults = new List<int>();

        var coordinator = new EpochCoordinator(
            TestServiceBuilder.Create().Build().GetRequiredService<IServiceScopeFactory>());

        var source = BlockHelpers.CreateEpochSource<int, IntegerSource>(
            "source",
            TestServiceBuilder.Create().WithScoped(new IntegerSource()).BuildScopeFactory(),
            coordinator);

        var doubler = BlockHelpers.CreateEpochActor<int, int, IntegerDoubler>(
            "doubler",
            new IntegerDoubler(middleResults));

        var terminal = BlockHelpers.CreateEpochActor<int, int, IntegerTerminal>(
            "terminal",
            new IntegerTerminal(terminalResults));

        var serviceProvider = TestServiceBuilder.Create().Build();
        var (graph, context) = BuildGraph("test-3block-int", serviceProvider, b =>
            b.AddBlock(source)
             .AddBlock(doubler)
             .AddBlock(terminal)
             .Connect(source, doubler)
             .Connect(doubler, terminal));

        // Act
        await graph.ExecuteAsync(context);

        // Assert – middle actor doubles values; terminal collects doubled values
        middleResults.Count.ShouldBe(3);
        middleResults.ShouldBe(new[] { 2, 4, 6 });

        terminalResults.Count.ShouldBe(3);
        terminalResults.ShouldBe(new[] { 2, 4, 6 });

        await coordinator.DisposeAsync();
        await ((IAsyncDisposable)serviceProvider).DisposeAsync();
    }

    // ====================================================================
    // TEST 3: Two-block graph with Person type (custom reference type)
    // ====================================================================

    [Fact]
    public async Task TwoBlockGraph_PersonType_TerminalExecutesAndCollectsAllItems()
    {
        // Arrange
        var terminalResults = new List<Person>();

        var coordinator = new EpochCoordinator(
            TestServiceBuilder.Create().Build().GetRequiredService<IServiceScopeFactory>());

        var source = BlockHelpers.CreateEpochSource<Person, PersonSource>(
            "source",
            TestServiceBuilder.Create().WithScoped(new PersonSource()).BuildScopeFactory(),
            coordinator);

        var terminal = BlockHelpers.CreateEpochActor<Person, Person, PersonTerminal>(
            "terminal",
            new PersonTerminal(terminalResults));

        var serviceProvider = TestServiceBuilder.Create().Build();
        var (graph, context) = BuildGraph("test-2block-person", serviceProvider, b =>
            b.AddBlock(source)
             .AddBlock(terminal)
             .Connect(source, terminal));

        // Act
        await graph.ExecuteAsync(context);

        // Assert – terminal must have processed all 3 Person objects
        terminalResults.Count.ShouldBe(3);
        terminalResults.Select(p => p.Name).ShouldBe(new[] { "Alice", "Bob", "Charlie" });

        await coordinator.DisposeAsync();
        await ((IAsyncDisposable)serviceProvider).DisposeAsync();
    }
}
