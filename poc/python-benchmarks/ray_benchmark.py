"""
Ray-based streaming DAG implementation.
Uses Ray's actor model with async streaming.
"""
import asyncio
import uuid
from datetime import datetime
from typing import AsyncIterator, List
import ray

from common_models import (
    RawRecord, ValidatedRecord, EnrichedRecord, RecordCategory,
    BenchmarkConfig, BenchmarkResult
)
from benchmark_framework import BenchmarkRunner


@ray.remote
class ProducerActor:
    """Ray actor that produces raw records"""
    
    async def produce(self, count: int) -> AsyncIterator[RawRecord]:
        """Generate raw records asynchronously"""
        for i in range(count):
            yield RawRecord(
                id=i,
                data=f"Data_{i}_{uuid.uuid4().hex[:8]}",
                timestamp=datetime.now()
            )
            
            # Simulate some source latency
            if i % 100 == 0:
                await asyncio.sleep(0.001)


@ray.remote
class ValidatorActor:
    """Ray actor that validates records"""
    
    async def validate(self, record: RawRecord) -> ValidatedRecord:
        """Validate a single record"""
        is_valid = bool(record.data) and record.id >= 0
        
        return ValidatedRecord(
            id=record.id,
            data=record.data,
            timestamp=record.timestamp,
            is_valid=is_valid
        )


@ray.remote
class EnricherActor:
    """Ray actor that enriches records"""
    
    async def enrich(self, record: ValidatedRecord) -> EnrichedRecord:
        """Enrich a single record with external data"""
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


@ray.remote
class RouterActor:
    """Ray actor that routes records by category"""
    
    def __init__(self, routes: dict):
        self.routes = routes  # category -> writer actor
    
    async def route(self, record: EnrichedRecord) -> None:
        """Route record to appropriate writer"""
        writer = self.routes.get(record.category)
        if writer:
            await writer.write.remote(record)


@ray.remote
class WriterActor:
    """Ray actor that writes/consumes records"""
    
    def __init__(self):
        self.count = 0
    
    async def write(self, record: EnrichedRecord) -> None:
        """Consume a record"""
        self.count += 1
    
    def get_count(self) -> int:
        """Get total records written"""
        return self.count


class RayBenchmark(BenchmarkRunner):
    """Benchmark implementation using Ray actors"""
    
    def __init__(self):
        super().__init__("Ray")
        
        # Initialize Ray if not already initialized
        if not ray.is_initialized():
            ray.init(ignore_reinit_error=True)
    
    async def run_benchmark(self, config: BenchmarkConfig) -> BenchmarkResult:
        """
        Run the Ray benchmark with the given configuration.
        
        NOTE: This implementation has known issues and requires refactoring:
        1. Ray remote methods don't support async iteration directly
        2. Need to use Ray's streaming API or batch processing
        3. Mixing ray.get() with asyncio.gather is incorrect
        
        For now, this is a placeholder that demonstrates the intended architecture.
        A working implementation would need to:
        - Use Ray's streaming generator pattern
        - Process records in batches
        - Use proper async/await with Ray's async API
        """
        
        print("⚠️  Ray benchmark implementation has known issues and needs refactoring")
        print("   See comments in ray_benchmark.py for details")
        print("   Returning placeholder result...")
        
        # Placeholder - simulate execution time based on config
        import time
        estimated_time = config.record_count * 0.001  # 1ms per record
        time.sleep(min(estimated_time, 5.0))  # Cap at 5 seconds
        
        with self.measure_performance(config) as result_container:
            # Placeholder result
            pass
        
        return result_container['result']
    
    def shutdown(self):
        """Shutdown Ray"""
        if ray.is_initialized():
            ray.shutdown()


# Test function
async def test_ray_benchmark():
    """Test the Ray benchmark implementation"""
    config = BenchmarkConfig(
        record_count=1000,
        max_concurrency=4,
        name="ray_test"
    )
    
    benchmark = RayBenchmark()
    try:
        result = await benchmark.run_benchmark(config)
        print(f"Ray benchmark completed: {result}")
    finally:
        benchmark.shutdown()


if __name__ == "__main__":
    asyncio.run(test_ray_benchmark())
