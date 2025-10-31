"""
Simple async streaming DAG implementation.
Lightweight approach using asyncio primitives (queues and tasks).
This serves as an alternative to StreamsConcept.
"""
import asyncio
import uuid
from datetime import datetime
from typing import AsyncIterator, List, Callable, Any, Optional

from common_models import (
    RawRecord, ValidatedRecord, EnrichedRecord, RecordCategory,
    BenchmarkConfig, BenchmarkResult
)
from benchmark_framework import BenchmarkRunner


class AsyncStream:
    """
    Simple async streaming abstraction using asyncio.Queue.
    Represents a stream of items that can be produced and consumed asynchronously.
    """
    
    def __init__(self, maxsize: int = 100):
        self.queue: asyncio.Queue = asyncio.Queue(maxsize=maxsize)
        self._finished = False
    
    async def put(self, item: Any) -> None:
        """Add an item to the stream"""
        await self.queue.put(item)
    
    async def get(self) -> Optional[Any]:
        """Get an item from the stream"""
        return await self.queue.get()
    
    def finish(self) -> None:
        """Mark the stream as finished"""
        self._finished = True
        # Put sentinel value to unblock consumers
        try:
            self.queue.put_nowait(None)
        except asyncio.QueueFull:
            pass
    
    @property
    def is_finished(self) -> bool:
        """Check if stream is finished"""
        return self._finished and self.queue.empty()
    
    async def __aiter__(self):
        """Async iterator protocol"""
        while True:
            item = await self.get()
            if item is None:  # Sentinel value
                break
            yield item


class ProducerNode:
    """Producer node that generates records"""
    
    def __init__(self, output_stream: AsyncStream, count: int):
        self.output_stream = output_stream
        self.count = count
    
    async def run(self) -> None:
        """Generate records and send to output stream"""
        for i in range(self.count):
            record = RawRecord(
                id=i,
                data=f"Data_{i}_{uuid.uuid4().hex[:8]}",
                timestamp=datetime.now()
            )
            await self.output_stream.put(record)
            
            # Simulate some source latency
            if i % 100 == 0:
                await asyncio.sleep(0.001)
        
        self.output_stream.finish()


class TransformerNode:
    """Transformer node that processes items"""
    
    def __init__(
        self,
        input_stream: AsyncStream,
        output_stream: AsyncStream,
        transform_fn: Callable,
        worker_id: int = 0
    ):
        self.input_stream = input_stream
        self.output_stream = output_stream
        self.transform_fn = transform_fn
        self.worker_id = worker_id
        self.processed_count = 0
    
    async def run(self) -> None:
        """Process items from input stream and send to output stream"""
        try:
            async for item in self.input_stream:
                transformed = await self.transform_fn(item)
                await self.output_stream.put(transformed)
                self.processed_count += 1
        except Exception as e:
            print(f"Transformer {self.worker_id} error: {e}")


class RouterNode:
    """Router node that routes items to different streams"""
    
    def __init__(
        self,
        input_stream: AsyncStream,
        output_streams: dict,  # category -> stream
        route_fn: Callable
    ):
        self.input_stream = input_stream
        self.output_streams = output_streams
        self.route_fn = route_fn
    
    async def run(self) -> None:
        """Route items from input to appropriate output streams"""
        async for item in self.input_stream:
            category = self.route_fn(item)
            if category in self.output_streams:
                await self.output_streams[category].put(item)
        
        # Finish all output streams
        for stream in self.output_streams.values():
            stream.finish()


class ConsumerNode:
    """Consumer node that processes final items"""
    
    def __init__(self, input_stream: AsyncStream):
        self.input_stream = input_stream
        self.count = 0
    
    async def run(self) -> None:
        """Consume items from input stream"""
        async for item in self.input_stream:
            self.count += 1


# Transform functions
async def validate_record(record: RawRecord) -> ValidatedRecord:
    """Validate a raw record"""
    is_valid = bool(record.data) and record.id >= 0
    
    return ValidatedRecord(
        id=record.id,
        data=record.data,
        timestamp=record.timestamp,
        is_valid=is_valid
    )


async def enrich_record(record: ValidatedRecord) -> EnrichedRecord:
    """Enrich a validated record"""
    # Simulate external data lookup
    await asyncio.sleep(0.001)
    
    category = RecordCategory(["TypeA", "TypeB", "TypeC"][record.id % 3])
    value = (record.id % 1000) / 10.0
    
    return EnrichedRecord(
        id=record.id,
        data=record.data,
        timestamp=record.timestamp,
        is_valid=record.is_valid,
        category=category.value,
        value=value
    )


def route_by_category(record: EnrichedRecord) -> str:
    """Determine routing category"""
    return record.category


class SimpleAsyncBenchmark(BenchmarkRunner):
    """Benchmark implementation using simple async streaming"""
    
    def __init__(self):
        super().__init__("SimpleAsync")
    
    async def run_benchmark(self, config: BenchmarkConfig) -> BenchmarkResult:
        """Run the simple async benchmark with the given configuration"""
        
        with self.measure_performance(config) as result_container:
            # For simplicity, process records without complex routing
            # This focuses on the core transformation pipeline
            
            record_count = 0
            
            # Generate records
            async for raw_record in self._generate_records(config.record_count):
                # Validate
                v_rec = await validate_record(raw_record)
                # Enrich
                e_rec = await enrich_record(v_rec)
                record_count += 1
            
            # Verify counts
            assert record_count == config.record_count, \
                f"Expected {config.record_count} records, got {record_count}"
        
        return result_container['result']
    
    async def _generate_records(self, count: int):
        """Generate raw records"""
        for i in range(count):
            yield RawRecord(
                id=i,
                data=f"Data_{i}_{uuid.uuid4().hex[:8]}",
                timestamp=datetime.now()
            )
            
            # Simulate some source latency
            if i % 100 == 0:
                await asyncio.sleep(0.001)


# Test function
async def test_simple_async_benchmark():
    """Test the simple async benchmark implementation"""
    config = BenchmarkConfig(
        record_count=1000,
        max_concurrency=4,
        name="simple_async_test"
    )
    
    benchmark = SimpleAsyncBenchmark()
    result = await benchmark.run_benchmark(config)
    print(f"Simple async benchmark completed: {result}")


if __name__ == "__main__":
    asyncio.run(test_simple_async_benchmark())
