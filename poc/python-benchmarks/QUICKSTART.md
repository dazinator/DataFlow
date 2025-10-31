# Quick Start Guide: Running Python Benchmarks

## Prerequisites

- Python 3.12+ installed
- .NET 8.0 SDK (for .NET POC comparison - optional)
- 2+ GB RAM recommended
- Linux/macOS/Windows

## Quick Setup

```bash
# Navigate to benchmark directory
cd poc/python-benchmarks

# Create virtual environment
python3 -m venv venv

# Activate virtual environment
# On Linux/macOS:
source venv/bin/activate
# On Windows:
# venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

## Running Benchmarks

### Option 1: Quick Test (Recommended for first run)
```bash
python run_comprehensive_benchmarks.py \
    --sizes 1000,5000 \
    --concurrency 1,2 \
    --libraries simple,pydantic
```

### Option 2: Full Benchmark Suite
```bash
python run_comprehensive_benchmarks.py \
    --sizes 1000,5000,10000,50000 \
    --concurrency 1,2,4,8 \
    --libraries simple,pydantic,ray
```

### Option 3: Individual Library Testing
```bash
# Test specific library
python simple_async_benchmark.py
python pydantic_benchmark.py
python ray_benchmark.py
```

## Viewing Results

### Generated Files

After running benchmarks, you'll find in the `results/` directory:

- `comprehensive_results_[timestamp].csv` - Raw data (CSV format)
- `comprehensive_results_[timestamp].json` - Raw data (JSON format)
- `comprehensive_summary_[timestamp].md` - Markdown summary report
- `charts/` - Directory with visualization charts:
  - `throughput_comparison.png` - Throughput across configurations
  - `execution_time_comparison.png` - Execution time comparison
  - `memory_comparison.png` - Memory usage comparison
  - `concurrency_scaling.png` - Concurrency scaling efficiency
  - `efficiency_heatmap.png` - Throughput per MB memory

### View Summary
```bash
# View latest summary
cat results/comprehensive_summary_*.md | tail -100
```

## Generating Custom Visualizations

```bash
python visualize_results.py results/comprehensive_results_*.csv --output-dir results/custom_charts
```

## Understanding the Results

### Key Metrics

1. **Throughput (rec/s)**: Higher is better
   - Measures how many records processed per second
   - Critical for understanding overall performance

2. **Execution Time (s)**: Lower is better
   - Total time to process all records
   - Shows absolute performance

3. **Peak Memory (MB)**: Lower is better
   - Maximum memory used during execution
   - Important for resource-constrained environments

4. **GC Collections (gen0/gen1/gen2)**: Lower is better
   - Garbage collection events
   - Indicates memory pressure

### Interpreting Concurrency Scaling

- **Linear scaling**: 2x workers = ~2x throughput (ideal)
- **Sub-linear scaling**: 2x workers < 2x throughput (common)
- **No scaling**: 2x workers = same throughput (bottleneck)

Example from results:
```
Pydantic at 1000 records:
- 1 worker:  827 rec/s
- 2 workers: 1,480 rec/s (1.79x) ✓ Good scaling
- 4 workers: 2,569 rec/s (3.10x) ✓ Excellent scaling

SimpleAsync at 1000 records:
- 1 worker:  852 rec/s
- 2 workers: 855 rec/s (1.00x) ✗ No scaling (sequential bottleneck)
- 4 workers: 853 rec/s (1.00x) ✗ No scaling
```

## Troubleshooting

### Issue: Ray import errors
```bash
# Solution: Ray requires specific setup
pip install ray[default]
# Or skip Ray in benchmarks:
python run_comprehensive_benchmarks.py --libraries simple,pydantic
```

### Issue: Out of memory errors
```bash
# Solution: Reduce workload size
python run_comprehensive_benchmarks.py --sizes 1000,5000 --concurrency 1,2
```

### Issue: Charts not generating
```bash
# Solution: Ensure matplotlib backend is available
pip install matplotlib pillow
# Use non-interactive backend (already set in code)
```

## Customizing Benchmarks

### Adding New Workload Sizes
```python
# Edit run_comprehensive_benchmarks.py
parser.add_argument("--sizes", default="1000,5000,10000,50000,100000")
```

### Adding Custom Libraries

1. Create new file `my_library_benchmark.py`
2. Extend `BenchmarkRunner` class
3. Implement `run_benchmark()` method
4. Import in `run_comprehensive_benchmarks.py`

Example:
```python
# my_library_benchmark.py
from benchmark_framework import BenchmarkRunner
from common_models import BenchmarkConfig, BenchmarkResult

class MyLibraryBenchmark(BenchmarkRunner):
    def __init__(self):
        super().__init__("MyLibrary")
    
    async def run_benchmark(self, config: BenchmarkConfig) -> BenchmarkResult:
        with self.measure_performance(config) as result_container:
            # Your benchmark code here
            pass
        return result_container['result']
```

## Advanced Options

### Custom Output Directory
```bash
python run_comprehensive_benchmarks.py --output-dir /path/to/results
```

### Including .NET POC (requires implementation)
```bash
python run_comprehensive_benchmarks.py --include-dotnet --poc-path ../
```

### Verbose Output
```python
# Edit run_comprehensive_benchmarks.py
from benchmark_framework import print_result

# Change verbose=False to verbose=True
print_result(result, verbose=True)
```

## Performance Tips

1. **Run on idle system**: Close other applications
2. **Consistent environment**: Same machine for all tests
3. **Multiple runs**: Average results from 3-5 runs
4. **Warm-up**: First run may be slower (JIT, caching)
5. **Resource limits**: Check system RAM and CPU cores

## Next Steps

1. Review `BENCHMARK_ANALYSIS.md` for detailed findings
2. Check `results/charts/` for visual comparisons
3. Run extended benchmarks with your target workloads
4. Compare with .NET POC (pending integration)

## Getting Help

- Check existing results in `results/` directory
- Review benchmark implementation in `*_benchmark.py` files
- See `BENCHMARK_ANALYSIS.md` for methodology
- Refer to `README.md` for overview

## Example Session

```bash
$ cd poc/python-benchmarks
$ python3 -m venv venv
$ source venv/bin/activate
(venv) $ pip install -r requirements.txt
(venv) $ python run_comprehensive_benchmarks.py --sizes 1000,5000 --concurrency 1,2
================================================================================
Comprehensive Streaming DAG Library Benchmarks
================================================================================
Python Libraries: simple, pydantic
Record counts: 1000, 5000
Concurrency levels: 1, 2
Output: results
================================================================================

... benchmark output ...

✅ Benchmark run complete!
================================================================================

(venv) $ cat results/comprehensive_summary_*.md
# Comprehensive Benchmark: .NET DataFlow POC vs Python Streaming Libraries
...
```

---

*For questions or issues, refer to the main README.md or create a GitHub issue.*
