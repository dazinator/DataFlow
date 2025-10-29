================================================================================
DataFlow POC vs Non-POC Extended Performance Comparison
================================================================================

This benchmark tests various combinations of parameters:
- Different record counts (load levels)
- Different concurrency levels
- Different batch sizes

Testing: Load: 1K records
  Parameters: Records=1,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 638 ms | 1,565 rec/sec | 2,745.09 KB
    [POC] 380 ms | 2,631 rec/sec | 1,935.41 KB

Testing: Load: 5K records
  Parameters: Records=5,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 1,496 ms | 3,341 rec/sec | 12,200.63 KB
    [POC] 1,531 ms | 3,264 rec/sec | 7,948.48 KB

Testing: Load: 10K records
  Parameters: Records=10,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 2,959 ms | 3,379 rec/sec | 8,200.39 KB
    [POC] 3,022 ms | 3,308 rec/sec | 15,628.18 KB

Testing: Concurrency: 1 (10K records)
  Parameters: Records=10,000, Concurrency=1, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 11,705 ms | 854 rec/sec | 5,628.59 KB
    [POC] 11,684 ms | 856 rec/sec | 14,007.55 KB

Testing: Concurrency: 2 (10K records)
  Parameters: Records=10,000, Concurrency=2, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 5,957 ms | 1,678 rec/sec | 7,171.58 KB
    [POC] 5,966 ms | 1,676 rec/sec | 14,555.85 KB

Testing: Concurrency: 8 (10K records)
  Parameters: Records=10,000, Concurrency=8, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 1,480 ms | 6,755 rec/sec | 10,246.22 KB
    [POC] 1,532 ms | 6,523 rec/sec | 392.14 KB

Testing: Batch: 50 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=50
--------------------------------------------------------------------------------
    [Non-POC] 2,986 ms | 3,349 rec/sec | 8,159.20 KB
    [POC] 3,008 ms | 3,324 rec/sec | 15,658.13 KB

Testing: Batch: 200 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=200
--------------------------------------------------------------------------------
    [Non-POC] 2,942 ms | 3,399 rec/sec | 8,055.39 KB
    [POC] 2,960 ms | 3,378 rec/sec | 15,618.28 KB

Testing: Batch: 500 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=500
--------------------------------------------------------------------------------
    [Non-POC] 3,017 ms | 3,315 rec/sec | 8,140.75 KB
    [POC] 3,000 ms | 3,333 rec/sec | 15,608.95 KB


================================================================================
SUMMARY - EXTENDED COMPARISON
================================================================================

Load: 1K records
  Config: 1,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=638ms, POC=380ms (ratio=0.60x)
  Memory: Non-POC=2,745KB, POC=1,935KB (ratio=0.71x)

Load: 5K records
  Config: 5,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=1,496ms, POC=1,531ms (ratio=1.02x)
  Memory: Non-POC=12,201KB, POC=7,948KB (ratio=0.65x)

Load: 10K records
  Config: 10,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=2,959ms, POC=3,022ms (ratio=1.02x)
  Memory: Non-POC=8,200KB, POC=15,628KB (ratio=1.91x)

Concurrency: 1 (10K records)
  Config: 10,000 records | Concurrency: 1 | Batch: 100
  Time:   Non-POC=11,705ms, POC=11,684ms (ratio=1.00x)
  Memory: Non-POC=5,629KB, POC=14,008KB (ratio=2.49x)

Concurrency: 2 (10K records)
  Config: 10,000 records | Concurrency: 2 | Batch: 100
  Time:   Non-POC=5,957ms, POC=5,966ms (ratio=1.00x)
  Memory: Non-POC=7,172KB, POC=14,556KB (ratio=2.03x)

Concurrency: 8 (10K records)
  Config: 10,000 records | Concurrency: 8 | Batch: 100
  Time:   Non-POC=1,480ms, POC=1,532ms (ratio=1.04x)
  Memory: Non-POC=10,246KB, POC=392KB (ratio=0.04x)

Batch: 50 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 50
  Time:   Non-POC=2,986ms, POC=3,008ms (ratio=1.01x)
  Memory: Non-POC=8,159KB, POC=15,658KB (ratio=1.92x)

Batch: 200 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 200
  Time:   Non-POC=2,942ms, POC=2,960ms (ratio=1.01x)
  Memory: Non-POC=8,055KB, POC=15,618KB (ratio=1.94x)

Batch: 500 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 500
  Time:   Non-POC=3,017ms, POC=3,000ms (ratio=0.99x)
  Memory: Non-POC=8,141KB, POC=15,609KB (ratio=1.92x)

Extended results saved to: /home/runner/work/lib-dataflow/lib-dataflow/benchmark-results/extended-benchmark_2025-10-29_21-48-20.md
