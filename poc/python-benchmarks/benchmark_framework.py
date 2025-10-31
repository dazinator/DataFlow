"""
Base benchmark framework providing common utilities for measuring performance.
"""
import gc
import time
import tracemalloc
import psutil
from typing import Callable, Any, Optional
from contextlib import contextmanager
from common_models import BenchmarkResult, BenchmarkConfig


class BenchmarkRunner:
    """Base class for running benchmarks with consistent metrics collection"""
    
    def __init__(self, library_name: str):
        self.library_name = library_name
        self.process = psutil.Process()
    
    @contextmanager
    def measure_performance(self, config: BenchmarkConfig):
        """
        Context manager that measures execution time, memory, and GC stats.
        
        Usage:
            with runner.measure_performance(config) as result:
                # Run benchmark code
                pass
            # result now contains the metrics
        """
        # Clear garbage and start fresh
        gc.collect()
        gc.collect()
        gc.collect()
        
        # Start memory tracking
        tracemalloc.start()
        
        # Record initial GC stats
        gc_stats_before = gc.get_count()
        
        # Record start time
        start_time = time.perf_counter()
        
        # Record initial CPU
        self.process.cpu_percent()  # First call returns 0, so we call it to initialize
        
        # Container for result
        result_container = {'result': None}
        
        try:
            yield result_container
        finally:
            # Record end time
            end_time = time.perf_counter()
            execution_time = end_time - start_time
            
            # Get peak memory
            current, peak = tracemalloc.get_traced_memory()
            tracemalloc.stop()
            peak_memory_mb = peak / 1024 / 1024
            
            # Record final GC stats
            gc_stats_after = gc.get_count()
            gc_collections = {
                "gen0": gc_stats_after[0] - gc_stats_before[0],
                "gen1": gc_stats_after[1] - gc_stats_before[1],
                "gen2": gc_stats_after[2] - gc_stats_before[2],
            }
            
            # Get CPU usage (averaged over the run)
            cpu_percent = self.process.cpu_percent()
            
            # Calculate throughput
            throughput = config.record_count / execution_time if execution_time > 0 else 0
            
            # Create result
            result = BenchmarkResult(
                library=self.library_name,
                config=config,
                execution_time_sec=execution_time,
                throughput_per_sec=throughput,
                peak_memory_mb=peak_memory_mb,
                gc_collections=gc_collections,
                cpu_percent=cpu_percent if cpu_percent > 0 else None
            )
            
            result_container['result'] = result
    
    async def run_benchmark(self, config: BenchmarkConfig) -> BenchmarkResult:
        """
        Run a single benchmark and return results.
        Must be implemented by subclasses.
        """
        raise NotImplementedError("Subclasses must implement run_benchmark")


def print_result(result: BenchmarkResult, verbose: bool = True):
    """Pretty print a benchmark result"""
    print(f"\n{'=' * 80}")
    print(f"Library: {result.library}")
    print(f"Config: {result.config}")
    print(f"{'=' * 80}")
    print(f"Execution Time: {result.execution_time_sec:.3f} seconds")
    print(f"Throughput: {result.throughput_per_sec:.0f} records/second")
    print(f"Peak Memory: {result.peak_memory_mb:.2f} MB")
    
    if verbose:
        print(f"\nGC Collections:")
        print(f"  Generation 0: {result.gc_collections.get('gen0', 0)}")
        print(f"  Generation 1: {result.gc_collections.get('gen1', 0)}")
        print(f"  Generation 2: {result.gc_collections.get('gen2', 0)}")
        
        if result.cpu_percent is not None:
            print(f"\nCPU Usage: {result.cpu_percent:.1f}%")
    
    print(f"{'=' * 80}\n")
