"""
Main benchmark runner that executes all Python library benchmarks
and generates comparison reports.
"""
import asyncio
import argparse
import json
import csv
from datetime import datetime
from pathlib import Path
from typing import List

from common_models import BenchmarkConfig, BenchmarkResult
from benchmark_framework import print_result
from simple_async_benchmark import SimpleAsyncBenchmark
from pydantic_benchmark import PydanticBenchmark

# Ray import is conditional since it may not be available
try:
    from ray_benchmark import RayBenchmark
    RAY_AVAILABLE = True
except ImportError:
    RAY_AVAILABLE = False
    print("Warning: Ray not available, skipping Ray benchmarks")


async def run_all_benchmarks(
    record_counts: List[int],
    concurrency_levels: List[int],
    libraries: List[str]
) -> List[BenchmarkResult]:
    """Run benchmarks for all specified configurations"""
    
    results = []
    
    # Create benchmark instances
    benchmarks = {}
    
    if "simple" in libraries:
        benchmarks["simple"] = SimpleAsyncBenchmark()
    
    if "pydantic" in libraries:
        benchmarks["pydantic"] = PydanticBenchmark()
    
    if "ray" in libraries and RAY_AVAILABLE:
        benchmarks["ray"] = RayBenchmark()
    elif "ray" in libraries and not RAY_AVAILABLE:
        print("Skipping Ray benchmark - not installed")
    
    # Run benchmarks for each configuration
    for record_count in record_counts:
        for concurrency in concurrency_levels:
            config = BenchmarkConfig(
                record_count=record_count,
                max_concurrency=concurrency,
                name=f"{record_count}rec_{concurrency}workers"
            )
            
            print(f"\n{'='*80}")
            print(f"Running benchmarks: {record_count:,} records, {concurrency} workers")
            print(f"{'='*80}")
            
            for lib_name, benchmark in benchmarks.items():
                try:
                    print(f"\nRunning {lib_name}...")
                    result = await benchmark.run_benchmark(config)
                    print_result(result, verbose=True)
                    results.append(result)
                    
                    # Brief pause between benchmarks
                    await asyncio.sleep(1)
                    
                except Exception as e:
                    print(f"ERROR: {lib_name} benchmark failed: {e}")
                    import traceback
                    traceback.print_exc()
            
            # Longer pause between configurations
            await asyncio.sleep(2)
    
    # Cleanup Ray if used
    if "ray" in benchmarks:
        benchmarks["ray"].shutdown()
    
    return results


def save_results(results: List[BenchmarkResult], output_dir: Path) -> None:
    """Save benchmark results to files"""
    output_dir.mkdir(exist_ok=True)
    
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    
    # Save as CSV
    csv_path = output_dir / f"benchmark_results_{timestamp}.csv"
    with open(csv_path, 'w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow([
            'Library', 'RecordCount', 'Concurrency', 'ExecutionTime(s)',
            'Throughput(rec/s)', 'PeakMemory(MB)', 'GC_Gen0', 'GC_Gen1', 'GC_Gen2', 'CPU%'
        ])
        
        for result in results:
            writer.writerow([
                result.library,
                result.config.record_count,
                result.config.max_concurrency,
                f"{result.execution_time_sec:.3f}",
                f"{result.throughput_per_sec:.0f}",
                f"{result.peak_memory_mb:.2f}",
                result.gc_collections.get('gen0', 0),
                result.gc_collections.get('gen1', 0),
                result.gc_collections.get('gen2', 0),
                f"{result.cpu_percent:.1f}" if result.cpu_percent else "N/A"
            ])
    
    print(f"\nResults saved to: {csv_path}")
    
    # Save as JSON
    json_path = output_dir / f"benchmark_results_{timestamp}.json"
    with open(json_path, 'w') as f:
        json.dump([
            {
                'library': r.library,
                'record_count': r.config.record_count,
                'concurrency': r.config.max_concurrency,
                'execution_time_sec': r.execution_time_sec,
                'throughput_per_sec': r.throughput_per_sec,
                'peak_memory_mb': r.peak_memory_mb,
                'gc_collections': r.gc_collections,
                'cpu_percent': r.cpu_percent
            }
            for r in results
        ], f, indent=2)
    
    print(f"Results saved to: {json_path}")
    
    # Generate markdown summary
    md_path = output_dir / f"benchmark_summary_{timestamp}.md"
    generate_markdown_summary(results, md_path)
    print(f"Summary saved to: {md_path}")


def generate_markdown_summary(results: List[BenchmarkResult], output_path: Path) -> None:
    """Generate a markdown summary of benchmark results"""
    
    with open(output_path, 'w') as f:
        f.write("# Python Streaming DAG Library Benchmark Results\n\n")
        f.write(f"*Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*\n\n")
        
        # Group results by configuration
        configs = {}
        for result in results:
            key = (result.config.record_count, result.config.max_concurrency)
            if key not in configs:
                configs[key] = []
            configs[key].append(result)
        
        # Write results for each configuration
        for (record_count, concurrency), config_results in sorted(configs.items()):
            f.write(f"## Configuration: {record_count:,} records, {concurrency} workers\n\n")
            
            # Summary table
            f.write("| Library | Exec Time (s) | Throughput (rec/s) | Peak Memory (MB) | GC (0/1/2) |\n")
            f.write("|---------|---------------|--------------------|--------------------|------------|\n")
            
            for result in sorted(config_results, key=lambda r: r.execution_time_sec):
                gc_str = f"{result.gc_collections.get('gen0', 0)}/{result.gc_collections.get('gen1', 0)}/{result.gc_collections.get('gen2', 0)}"
                f.write(
                    f"| {result.library} | {result.execution_time_sec:.3f} | "
                    f"{result.throughput_per_sec:,.0f} | {result.peak_memory_mb:.2f} | {gc_str} |\n"
                )
            
            f.write("\n")
            
            # Find best performers
            fastest = min(config_results, key=lambda r: r.execution_time_sec)
            most_memory_efficient = min(config_results, key=lambda r: r.peak_memory_mb)
            
            f.write("**Performance Highlights:**\n\n")
            f.write(f"- **Fastest:** {fastest.library} ({fastest.execution_time_sec:.3f}s)\n")
            f.write(f"- **Most Memory Efficient:** {most_memory_efficient.library} ({most_memory_efficient.peak_memory_mb:.2f} MB)\n\n")
            
            # Relative performance
            f.write("**Relative Performance:**\n\n")
            for result in sorted(config_results, key=lambda r: r.execution_time_sec):
                relative_speed = fastest.execution_time_sec / result.execution_time_sec
                f.write(f"- {result.library}: {relative_speed:.2f}x vs fastest\n")
            
            f.write("\n---\n\n")
        
        # Overall insights
        f.write("## Overall Insights\n\n")
        
        # Average throughput by library
        lib_throughputs = {}
        for result in results:
            if result.library not in lib_throughputs:
                lib_throughputs[result.library] = []
            lib_throughputs[result.library].append(result.throughput_per_sec)
        
        f.write("### Average Throughput by Library\n\n")
        for lib, throughputs in sorted(lib_throughputs.items()):
            avg_throughput = sum(throughputs) / len(throughputs)
            f.write(f"- **{lib}:** {avg_throughput:,.0f} records/second\n")
        
        f.write("\n")
        
        # Memory efficiency
        lib_memory = {}
        for result in results:
            if result.library not in lib_memory:
                lib_memory[result.library] = []
            lib_memory[result.library].append(result.peak_memory_mb)
        
        f.write("### Average Memory Usage by Library\n\n")
        for lib, memory_vals in sorted(lib_memory.items()):
            avg_memory = sum(memory_vals) / len(memory_vals)
            f.write(f"- **{lib}:** {avg_memory:.2f} MB\n")


async def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="Run Python streaming DAG library benchmarks")
    parser.add_argument(
        "--libraries",
        type=str,
        default="simple,pydantic,ray",
        help="Comma-separated list of libraries to benchmark (simple,pydantic,ray)"
    )
    parser.add_argument(
        "--sizes",
        type=str,
        default="1000,5000,10000,50000",
        help="Comma-separated list of record counts to test"
    )
    parser.add_argument(
        "--concurrency",
        type=str,
        default="1,2,8",
        help="Comma-separated list of concurrency levels to test"
    )
    parser.add_argument(
        "--output-dir",
        type=str,
        default="results",
        help="Output directory for results"
    )
    
    args = parser.parse_args()
    
    # Parse arguments
    libraries = [lib.strip() for lib in args.libraries.split(',')]
    record_counts = [int(size.strip()) for size in args.sizes.split(',')]
    concurrency_levels = [int(conc.strip()) for conc in args.concurrency.split(',')]
    output_dir = Path(args.output_dir)
    
    print("="*80)
    print("Python Streaming DAG Library Benchmarks")
    print("="*80)
    print(f"Libraries: {', '.join(libraries)}")
    print(f"Record counts: {', '.join(map(str, record_counts))}")
    print(f"Concurrency levels: {', '.join(map(str, concurrency_levels))}")
    print(f"Output directory: {output_dir}")
    print("="*80)
    
    # Run benchmarks
    results = await run_all_benchmarks(record_counts, concurrency_levels, libraries)
    
    # Save results
    save_results(results, output_dir)
    
    print("\n" + "="*80)
    print("Benchmark run complete!")
    print("="*80)


if __name__ == "__main__":
    asyncio.run(main())
