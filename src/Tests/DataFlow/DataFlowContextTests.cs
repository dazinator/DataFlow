namespace Tests.DataFlow
{
    [UnitTest]
    public class DataFlowContextTests
    {
        [Fact]
        public void Current_Is_Null_By_Default()
        {
            Assert.Null(DataFlowContext.Current);
        }

        [Fact]
        public void Current_Can_Be_Set_And_Retrieved()
        {
            var ctx = new DataFlowContext();
            DataFlowContext.Current = ctx;
            Assert.Same(ctx, DataFlowContext.Current);
        }

        [Fact]
        public void Generic_Current_Returns_Null_If_Type_Mismatch()
        {
            var ctx = new DataFlowContext();
            DataFlowContext.Current = ctx;
            Assert.Null(DataFlowContext<string>.Current);
        }

        [Fact]
        public void Generic_Current_Returns_Instance_If_Type_Matches()
        {
            var ctx = new DataFlowContext<string>("test");
            DataFlowContext.Current = ctx;
            Assert.Same(ctx, DataFlowContext<string>.Current);
        }

        [Fact]
        public async Task AsyncLocal_Propagates_Across_Async_Flows()
        {
            var ctx = new DataFlowContext();
            DataFlowContext.Current = ctx;

            await Task.Run(() =>
            {
                Assert.Same(ctx, DataFlowContext.Current);
            });
        }

        [Fact]
        public async Task AsyncLocal_Isolated_Between_Async_Flows()
        {
            var ctx1 = new DataFlowContext();
            var ctx2 = new DataFlowContext();

            DataFlowContext.Current = ctx1;

            await Task.Run(() =>
            {
                DataFlowContext.Current = ctx2;
                Assert.Same(ctx2, DataFlowContext.Current);
            });

            // Original context remains in parent flow
            Assert.Same(ctx1, DataFlowContext.Current);
        }
    }
}
