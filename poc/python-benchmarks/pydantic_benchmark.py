"""
Pydantic-based typed dataflow implementation.
Uses Pydantic models for type safety and async function composition.
Inspired by the pydantic-dataflow concept: typed, async-first DAG.
"""
import asyncio
import uuid
from datetime import datetime
from typing import AsyncIterator, List, Callable, TypeVar, Generic
from pydantic import BaseModel, Field

from common_models import (
    BenchmarkConfig, BenchmarkResult
)
from benchmark_framework import BenchmarkRunner


# Pydantic models for type-safe data flow
class RawRecordModel(BaseModel):
    """Pydantic model for raw records"""
    id: int = Field(ge=0)
    data: str
    timestamp: datetime


class ValidatedRecordModel(BaseModel):
    """Pydantic model for validated records"""
    id: int = Field(ge=0)
    data: str
    timestamp: datetime
    is_valid: bool


class EnrichedRecordModel(BaseModel):
    """Pydantic model for enriched records"""
    id: int = Field(ge=0)
    data: str
    timestamp: datetime
    is_valid: bool
    category: str
    value: float = Field(ge=0.0)


# Type variables for generic flow
TIn = TypeVar('TIn', bound=BaseModel)
TOut = TypeVar('TOut', bound=BaseModel)


class AsyncFlowNode(Generic[TIn, TOut]):
    """
    Generic async flow node that transforms input to output.
    Uses Pydantic models for type safety.
    """
    
    def __init__(
        self,
        name: str,
        transform_fn: Callable[[TIn], AsyncIterator[TOut]],
        concurrency: int = 1
    ):
        self.name = name
        self.transform_fn = transform_fn
        self.concurrency = concurrency
        self.processed_count = 0
    
    async def process(self, input_items: AsyncIterator[TIn]) -> AsyncIterator[TOut]:
        """Process input items with specified concurrency"""
        if self.concurrency == 1:
            # Single worker - preserve order
            async for item in input_items:
                async for output in self.transform_fn(item):
                    self.processed_count += 1
                    yield output
        else:
            # Multiple workers - use queue for concurrency
            queue = asyncio.Queue(maxsize=100)
            output_queue = asyncio.Queue(maxsize=100)
            finished = asyncio.Event()
            
            # Producer task: feed input items to queue
            async def feed_queue():
                async for item in input_items:
                    await queue.put(item)
                await queue.put(None)  # Sentinel
            
            # Worker tasks: process items from queue
            async def worker():
                while True:
                    item = await queue.get()
                    if item is None:
                        await queue.put(None)  # Pass sentinel to next worker
                        break
                    
                    async for output in self.transform_fn(item):
                        await output_queue.put(output)
                        self.processed_count += 1
            
            # Start all tasks
            feed_task = asyncio.create_task(feed_queue())
            worker_tasks = [asyncio.create_task(worker()) for _ in range(self.concurrency)]
            
            # Output consumer
            async def finalize():
                await asyncio.gather(feed_task, *worker_tasks)
                await output_queue.put(None)  # Sentinel for output
            
            finalize_task = asyncio.create_task(finalize())
            
            # Yield outputs
            while True:
                output = await output_queue.get()
                if output is None:
                    break
                yield output
            
            await finalize_task


class PydanticDataFlow:
    """
    Pydantic-based dataflow that chains typed async flow nodes.
    """
    
    def __init__(self, name: str):
        self.name = name
        self.nodes: List[AsyncFlowNode] = []
    
    def add_node(self, node: AsyncFlowNode) -> 'PydanticDataFlow':
        """Add a node to the flow (builder pattern)"""
        self.nodes.append(node)
        return self
    
    async def execute(self, source: AsyncIterator[BaseModel]) -> AsyncIterator[BaseModel]:
        """Execute the dataflow by chaining all nodes"""
        current_stream = source
        
        for node in self.nodes:
            current_stream = node.process(current_stream)
        
        async for item in current_stream:
            yield item


# Source generator
async def generate_raw_records(count: int) -> AsyncIterator[RawRecordModel]:
    """Generate raw records"""
    for i in range(count):
        yield RawRecordModel(
            id=i,
            data=f"Data_{i}_{uuid.uuid4().hex[:8]}",
            timestamp=datetime.now()
        )
        
        # Simulate some source latency
        if i % 100 == 0:
            await asyncio.sleep(0.001)


# Transform functions
async def validate_record(record: RawRecordModel) -> AsyncIterator[ValidatedRecordModel]:
    """Validate a raw record"""
    is_valid = bool(record.data) and record.id >= 0
    
    yield ValidatedRecordModel(
        id=record.id,
        data=record.data,
        timestamp=record.timestamp,
        is_valid=is_valid
    )


async def enrich_record(record: ValidatedRecordModel) -> AsyncIterator[EnrichedRecordModel]:
    """Enrich a validated record"""
    # Simulate external data lookup
    await asyncio.sleep(0.001)
    
    categories = ["TypeA", "TypeB", "TypeC"]
    category = categories[record.id % 3]
    value = (record.id % 1000) / 10.0
    
    yield EnrichedRecordModel(
        id=record.id,
        data=record.data,
        timestamp=record.timestamp,
        is_valid=record.is_valid,
        category=category,
        value=value
    )


class PydanticBenchmark(BenchmarkRunner):
    """Benchmark implementation using Pydantic-based typed dataflow"""
    
    def __init__(self):
        super().__init__("Pydantic")
    
    async def run_benchmark(self, config: BenchmarkConfig) -> BenchmarkResult:
        """Run the Pydantic benchmark with the given configuration"""
        
        with self.measure_performance(config) as result_container:
            # Build the dataflow
            flow = PydanticDataFlow("pydantic_benchmark")
            
            # Add validation node with concurrency
            validator_node = AsyncFlowNode(
                "validator",
                validate_record,
                concurrency=config.max_concurrency
            )
            flow.add_node(validator_node)
            
            # Add enrichment node with concurrency
            enricher_node = AsyncFlowNode(
                "enricher",
                enrich_record,
                concurrency=config.max_concurrency
            )
            flow.add_node(enricher_node)
            
            # Generate source records
            source = generate_raw_records(config.record_count)
            
            # Execute flow and count results
            record_count = 0
            category_counts = {"TypeA": 0, "TypeB": 0, "TypeC": 0}
            
            async for record in flow.execute(source):
                record_count += 1
                if isinstance(record, EnrichedRecordModel):
                    category_counts[record.category] = category_counts.get(record.category, 0) + 1
            
            # Verify counts
            assert record_count == config.record_count, \
                f"Expected {config.record_count} records, got {record_count}"
        
        return result_container['result']


# Test function
async def test_pydantic_benchmark():
    """Test the Pydantic benchmark implementation"""
    config = BenchmarkConfig(
        record_count=1000,
        max_concurrency=4,
        name="pydantic_test"
    )
    
    benchmark = PydanticBenchmark()
    result = await benchmark.run_benchmark(config)
    print(f"Pydantic benchmark completed: {result}")


if __name__ == "__main__":
    asyncio.run(test_pydantic_benchmark())
