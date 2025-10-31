"""
Comprehensive benchmark runner that compares .NET POC with Python implementations.
This script orchestrates running both .NET and Python benchmarks and combines results.
"""
import asyncio
import subprocess
import json
import csv
import re
import sys
from pathlib import Path
from datetime import datetime
from typing import List, Dict, Any, Optional
import argparse

# Add current directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

from common_models import BenchmarkConfig, BenchmarkResult
from benchmark_framework import print_result
from simple_async_benchmark import SimpleAsyncBenchmark
from pydantic_benchmark import PydanticBenchmark

# Ray import is conditional
try:
    from ray_benchmark import RayBenchmark
    RAY_AVAILABLE = True
except ImportError:
    RAY_AVAILABLE = False


class DotNetBenchmarkAdapter:
    """Adapter to run .NET POC benchmarks and format results"""
    
    def __init__(self, poc_path: Path):
        self.poc_path = poc_path
        self.benchmarks_path = poc_path / "DataFlow.POC.Benchmarks"
        
    async def run_benchmark(self, config: BenchmarkConfig) -> Optional[BenchmarkResult]:
        """
        Run .NET POC benchmark with given configuration.
        
        Note: This is a placeholder implementation. The .NET benchmarks
        would need to be modified to accept command-line parameters for
        record count and concurrency, and output results in a machine-readable format.
        """
        
        print(f"\n⚠️  .NET benchmark integration not yet implemented")
        print(f"   To add .NET comparison:")
        print(f"   1. Modify POC benchmarks to accept CLI args for record count and concurrency")
        print(f"   2. Add JSON output format to POC benchmark results")
        print(f"   3. Parse and return results here")
        print(f"   Skipping .NET benchmark for now...\n")
        
        # Placeholder result - would be replaced with actual benchmark execution
        return None


async def run_comprehensive_benchmarks(
    record_counts: List[int],
    concurrency_levels: List[int],
    libraries: List[str],
    include_dotnet: bool,
    poc_path: Optional[Path]
) -> List[BenchmarkResult]:
    """Run comprehensive benchmarks across all libraries"""
    
    results = []
    
    # Python benchmarks
    python_benchmarks = {}
    
    if "simple" in libraries:
        python_benchmarks["SimpleAsync"] = SimpleAsyncBenchmark()
    
    if "pydantic" in libraries:
        python_benchmarks["Pydantic"] = PydanticBenchmark()
    
    if "ray" in libraries:
        if RAY_AVAILABLE:
            python_benchmarks["Ray"] = RayBenchmark()
        else:
            print("⚠️  Ray not available, skipping Ray benchmarks")
    
    # .NET benchmark adapter
    dotnet_adapter = None
    if include_dotnet and poc_path:
        dotnet_adapter = DotNetBenchmarkAdapter(poc_path)
    
    # Run benchmarks for each configuration
    for record_count in record_counts:
        for concurrency in concurrency_levels:
            config = BenchmarkConfig(
                record_count=record_count,
                max_concurrency=concurrency,
                name=f"{record_count}rec_{concurrency}workers"
            )
            
            print(f"\n{'='*80}")
            print(f"Configuration: {record_count:,} records, {concurrency} workers")
            print(f"{'='*80}")
            
            # Run Python benchmarks
            for lib_name, benchmark in python_benchmarks.items():
                try:
                    print(f"\n▶ Running {lib_name}...")
                    result = await benchmark.run_benchmark(config)
                    print_result(result, verbose=False)
                    results.append(result)
                    await asyncio.sleep(1)
                except Exception as e:
                    print(f"❌ {lib_name} failed: {e}")
                    import traceback
                    traceback.print_exc()
            
            # Run .NET benchmark
            if dotnet_adapter:
                try:
                    print(f"\n▶ Running .NET POC...")
                    result = await dotnet_adapter.run_benchmark(config)
                    if result:
                        print_result(result, verbose=False)
                        results.append(result)
                except Exception as e:
                    print(f"❌ .NET POC failed: {e}")
            
            await asyncio.sleep(2)
    
    # Cleanup
    if "Ray" in python_benchmarks:
        python_benchmarks["Ray"].shutdown()
    
    return results


def save_comprehensive_results(results: List[BenchmarkResult], output_dir: Path):
    """Save comprehensive benchmark results"""
    output_dir.mkdir(parents=True, exist_ok=True)
    
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    
    # Save as CSV
    csv_path = output_dir / f"comprehensive_results_{timestamp}.csv"
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
    
    print(f"\n✅ Results saved to: {csv_path}")
    
    # Save as JSON
    json_path = output_dir / f"comprehensive_results_{timestamp}.json"
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
    
    print(f"✅ Results saved to: {json_path}")
    
    # Generate markdown summary
    generate_comprehensive_summary(results, output_dir / f"comprehensive_summary_{timestamp}.md")


def generate_comprehensive_summary(results: List[BenchmarkResult], output_path: Path):
    """Generate comprehensive markdown summary comparing all libraries"""
    
    with open(output_path, 'w') as f:
        f.write("# Comprehensive Benchmark: .NET DataFlow POC vs Python Streaming Libraries\n\n")
        f.write(f"*Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*\n\n")
        
        f.write("## Libraries Tested\n\n")
        libraries = set(r.library for r in results)
        for lib in sorted(libraries):
            f.write(f"- **{lib}**\n")
        f.write("\n")
        
        # Group by configuration
        configs = {}
        for result in results:
            key = (result.config.record_count, result.config.max_concurrency)
            if key not in configs:
                configs[key] = []
            configs[key].append(result)
        
        # Results by configuration
        f.write("## Results by Configuration\n\n")
        for (record_count, concurrency), config_results in sorted(configs.items()):
            f.write(f"### {record_count:,} records, {concurrency} workers\n\n")
            
            f.write("| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |\n")
            f.write("|---------|----------|--------------------|--------------|-----------|\n")
            
            for result in sorted(config_results, key=lambda r: r.execution_time_sec):
                gc_str = f"{result.gc_collections.get('gen0', 0)}/{result.gc_collections.get('gen1', 0)}/{result.gc_collections.get('gen2', 0)}"
                f.write(
                    f"| {result.library} | {result.execution_time_sec:.3f} | "
                    f"{result.throughput_per_sec:,.0f} | {result.peak_memory_mb:.2f} | {gc_str} |\n"
                )
            
            f.write("\n")
        
        # Overall insights
        f.write("## Key Insights\n\n")
        
        # Calculate averages per library
        lib_stats = {}
        for result in results:
            if result.library not in lib_stats:
                lib_stats[result.library] = {
                    'throughputs': [],
                    'memories': [],
                    'times': []
                }
            lib_stats[result.library]['throughputs'].append(result.throughput_per_sec)
            lib_stats[result.library]['memories'].append(result.peak_memory_mb)
            lib_stats[result.library]['times'].append(result.execution_time_sec)
        
        f.write("### Average Performance\n\n")
        f.write("| Library | Avg Throughput | Avg Memory | Avg Time |\n")
        f.write("|---------|----------------|------------|----------|\n")
        
        for lib in sorted(lib_stats.keys()):
            stats = lib_stats[lib]
            avg_throughput = sum(stats['throughputs']) / len(stats['throughputs'])
            avg_memory = sum(stats['memories']) / len(stats['memories'])
            avg_time = sum(stats['times']) / len(stats['times'])
            
            f.write(
                f"| {lib} | {avg_throughput:,.0f} rec/s | "
                f"{avg_memory:.2f} MB | {avg_time:.3f}s |\n"
            )
        
        f.write("\n")
        
        f.write("### Recommendations\n\n")
        f.write("Based on the benchmark results:\n\n")
        
        # Find best performers
        best_throughput = max(results, key=lambda r: r.throughput_per_sec)
        best_memory = min(results, key=lambda r: r.peak_memory_mb)
        
        f.write(f"- **Best Throughput:** {best_throughput.library} "
                f"({best_throughput.throughput_per_sec:,.0f} rec/s)\n")
        f.write(f"- **Most Memory Efficient:** {best_memory.library} "
                f"({best_memory.peak_memory_mb:.2f} MB)\n")
        f.write("\n")
        
        f.write("### Next Steps\n\n")
        f.write("1. Complete Ray benchmark implementation and testing\n")
        f.write("2. Integrate .NET POC benchmarks with command-line interface\n")
        f.write("3. Run extended benchmarks with larger datasets (100K+ records)\n")
        f.write("4. Test more concurrency levels (16, 32 workers)\n")
        f.write("5. Add memory pressure scenarios\n")
        f.write("6. Profile CPU utilization patterns\n")
    
    print(f"✅ Summary saved to: {output_path}")


async def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(
        description="Run comprehensive benchmarks comparing .NET and Python streaming libraries"
    )
    parser.add_argument(
        "--libraries",
        type=str,
        default="simple,pydantic",
        help="Comma-separated list of Python libraries (simple,pydantic,ray)"
    )
    parser.add_argument(
        "--sizes",
        type=str,
        default="1000,5000,10000",
        help="Comma-separated list of record counts"
    )
    parser.add_argument(
        "--concurrency",
        type=str,
        default="1,2,8",
        help="Comma-separated list of concurrency levels"
    )
    parser.add_argument(
        "--include-dotnet",
        action="store_true",
        help="Include .NET POC benchmarks (requires implementation)"
    )
    parser.add_argument(
        "--poc-path",
        type=Path,
        default=Path(__file__).parent.parent,
        help="Path to POC directory"
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path(__file__).parent / "results",
        help="Output directory for results"
    )
    
    args = parser.parse_args()
    
    # Parse arguments
    libraries = [lib.strip() for lib in args.libraries.split(',')]
    record_counts = [int(size.strip()) for size in args.sizes.split(',')]
    concurrency_levels = [int(conc.strip()) for conc in args.concurrency.split(',')]
    
    print("="*80)
    print("Comprehensive Streaming DAG Library Benchmarks")
    print("="*80)
    print(f"Python Libraries: {', '.join(libraries)}")
    if args.include_dotnet:
        print(f".NET POC: Enabled")
    print(f"Record counts: {', '.join(map(str, record_counts))}")
    print(f"Concurrency levels: {', '.join(map(str, concurrency_levels))}")
    print(f"Output: {args.output_dir}")
    print("="*80)
    
    # Run benchmarks
    results = await run_comprehensive_benchmarks(
        record_counts,
        concurrency_levels,
        libraries,
        args.include_dotnet,
        args.poc_path if args.include_dotnet else None
    )
    
    # Save results
    if results:
        save_comprehensive_results(results, args.output_dir)
        
        # Generate visualizations
        try:
            from visualize_results import generate_all_charts
            latest_csv = max(args.output_dir.glob("comprehensive_results_*.csv"))
            charts_dir = args.output_dir / "charts"
            print(f"\n📊 Generating visualizations...")
            generate_all_charts(latest_csv, charts_dir)
            print(f"✅ Charts saved to: {charts_dir}")
        except Exception as e:
            print(f"⚠️  Could not generate charts: {e}")
    
    print("\n" + "="*80)
    print("✅ Benchmark run complete!")
    print("="*80)


if __name__ == "__main__":
    asyncio.run(main())
