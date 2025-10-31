"""
Common data models used across all benchmark implementations.
These match the .NET POC data models for fair comparison.
"""
from dataclasses import dataclass
from datetime import datetime
from typing import Optional
from enum import Enum


class RecordCategory(str, Enum):
    """Record category for routing"""
    TYPE_A = "TypeA"
    TYPE_B = "TypeB"
    TYPE_C = "TypeC"


@dataclass
class RawRecord:
    """Raw record from data source"""
    id: int
    data: str
    timestamp: datetime


@dataclass
class ValidatedRecord:
    """Record after validation"""
    id: int
    data: str
    timestamp: datetime
    is_valid: bool


@dataclass
class EnrichedRecord:
    """Record after enrichment"""
    id: int
    data: str
    timestamp: datetime
    is_valid: bool
    category: str
    value: float


@dataclass
class BenchmarkConfig:
    """Configuration for benchmark runs"""
    record_count: int
    max_concurrency: int
    batch_size: int = 100
    name: str = "benchmark"
    
    def __str__(self):
        return f"{self.name}_records{self.record_count}_concurrency{self.max_concurrency}"


@dataclass
class BenchmarkResult:
    """Results from a single benchmark run"""
    library: str
    config: BenchmarkConfig
    execution_time_sec: float
    throughput_per_sec: float
    peak_memory_mb: float
    gc_collections: dict  # e.g., {"gen0": 10, "gen1": 2, "gen2": 1}
    cpu_percent: Optional[float] = None
    
    def __str__(self):
        return (
            f"{self.library}: {self.config.record_count} records in "
            f"{self.execution_time_sec:.2f}s ({self.throughput_per_sec:.0f} rec/s), "
            f"Memory: {self.peak_memory_mb:.1f} MB"
        )
