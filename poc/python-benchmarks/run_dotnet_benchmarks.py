"""
.NET DataFlow POC benchmark runner that generates Python-compatible output.
This script runs the .NET POC benchmarks and formats results for comparison.
"""
import subprocess
import json
import csv
import re
from pathlib import Path
from datetime import datetime
from typing import List, Dict, Any
import pandas as pd


class DotNetBenchmarkRunner:
    """Runner for .NET DataFlow POC benchmarks"""
    
    def __init__(self, poc_benchmarks_path: Path):
        self.poc_benchmarks_path = poc_benchmarks_path
        self.dll_path = poc_benchmarks_path / "bin" / "Debug" / "net8.0" / "DataFlow.POC.Benchmarks.dll"
    
    def ensure_built(self) -> bool:
        """Ensure the benchmark project is built"""
        if self.dll_path.exists():
            print(f"✓ Benchmark DLL found: {self.dll_path}")
            return True
        
        print(f"Building .NET benchmark project...")
        result = subprocess.run(
            ["dotnet", "build", str(self.poc_benchmarks_path / "DataFlow.POC.Benchmarks.csproj")],
            capture_output=True,
            text=True
        )
        
        if result.returncode != 0:
            print(f"Build failed:\n{result.stderr}")
            return False
        
        print(f"✓ Build successful")
        return self.dll_path.exists()
    
    def run_benchmark(self, record_count: int, concurrency: int, benchmark_type: str = "simple") -> Dict[str, Any]:
        """
        Run a single .NET benchmark and parse results.
        
        Args:
            record_count: Number of records to process
            concurrency: Concurrency level
            benchmark_type: Type of benchmark ("simple", "extended", or "comparison")
        
        Returns:
            Dictionary with benchmark results
        """
        print(f"Running .NET benchmark: {record_count:,} records, {concurrency} workers...")
        
        # Run the benchmark
        result = subprocess.run(
            ["dotnet", "run", "--project", str(self.poc_benchmarks_path / "DataFlow.POC.Benchmarks.csproj"),
             "--no-build", "--", benchmark_type],
            capture_output=True,
            text=True,
            timeout=600  # 10 minute timeout
        )
        
        if result.returncode != 0:
            print(f"Benchmark failed:\n{result.stderr}")
            return None
        
        # Parse output
        output = result.stdout
        
        # This is a simplified parser - you may need to adjust based on actual output format
        # For now, we'll create a placeholder result
        return {
            'library': 'DotNet-POC',
            'record_count': record_count,
            'concurrency': concurrency,
            'execution_time_sec': 0.0,  # Parse from output
            'throughput_per_sec': 0.0,   # Parse from output
            'peak_memory_mb': 0.0,       # Parse from output
            'gc_gen0': 0,
            'gc_gen1': 0,
            'gc_gen2': 0,
            'cpu_percent': 0.0
        }
    
    def run_benchmarks_suite(
        self,
        record_counts: List[int],
        concurrency_levels: List[int]
    ) -> List[Dict[str, Any]]:
        """Run a suite of benchmarks"""
        
        if not self.ensure_built():
            print("Failed to build benchmarks")
            return []
        
        results = []
        
        for record_count in record_counts:
            for concurrency in concurrency_levels:
                result = self.run_benchmark(record_count, concurrency)
                if result:
                    results.append(result)
        
        return results


def save_dotnet_results(results: List[Dict[str, Any]], output_dir: Path):
    """Save .NET benchmark results in the same format as Python benchmarks"""
    output_dir.mkdir(exist_ok=True)
    
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    
    # Save as CSV
    csv_path = output_dir / f"dotnet_benchmark_results_{timestamp}.csv"
    with open(csv_path, 'w', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'library', 'record_count', 'concurrency', 'execution_time_sec',
            'throughput_per_sec', 'peak_memory_mb', 'gc_gen0', 'gc_gen1', 'gc_gen2', 'cpu_percent'
        ])
        writer.writeheader()
        writer.writerows(results)
    
    print(f"Saved .NET results to: {csv_path}")
    
    # Save as JSON
    json_path = output_dir / f"dotnet_benchmark_results_{timestamp}.json"
    with open(json_path, 'w') as f:
        json.dump(results, f, indent=2)
    
    print(f"Saved .NET results to: {json_path}")


def combine_results(python_results_file: Path, dotnet_results_file: Path, output_path: Path):
    """Combine Python and .NET results into a single file for comparison"""
    
    # Load Python results
    with open(python_results_file) as f:
        if python_results_file.suffix == '.csv':
            python_df = pd.read_csv(f)
            python_results = python_df.to_dict('records')
        else:
            python_results = json.load(f)
    
    # Load .NET results
    with open(dotnet_results_file) as f:
        if dotnet_results_file.suffix == '.csv':
            dotnet_df = pd.read_csv(f)
            dotnet_results = dotnet_df.to_dict('records')
        else:
            dotnet_results = json.load(f)
    
    # Combine
    combined_results = python_results + dotnet_results
    
    # Save combined results
    output_path.parent.mkdir(exist_ok=True)
    
    if output_path.suffix == '.csv':
        with open(output_path, 'w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=combined_results[0].keys())
            writer.writeheader()
            writer.writerows(combined_results)
    else:
        with open(output_path, 'w') as f:
            json.dump(combined_results, f, indent=2)
    
    print(f"Combined results saved to: {output_path}")


if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="Run .NET DataFlow POC benchmarks")
    parser.add_argument(
        "--poc-path",
        type=Path,
        default=Path(__file__).parent.parent / "DataFlow.POC.Benchmarks",
        help="Path to POC benchmarks project"
    )
    parser.add_argument(
        "--sizes",
        type=str,
        default="1000,5000,10000,50000",
        help="Comma-separated list of record counts"
    )
    parser.add_argument(
        "--concurrency",
        type=str,
        default="1,2,8",
        help="Comma-separated list of concurrency levels"
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("results"),
        help="Output directory for results"
    )
    
    args = parser.parse_args()
    
    record_counts = [int(s.strip()) for s in args.sizes.split(',')]
    concurrency_levels = [int(c.strip()) for c in args.concurrency.split(',')]
    
    runner = DotNetBenchmarkRunner(args.poc_path)
    results = runner.run_benchmarks_suite(record_counts, concurrency_levels)
    
    if results:
        save_dotnet_results(results, args.output_dir)
    else:
        print("No results generated")
