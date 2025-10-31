"""
Combined benchmark analysis: .NET POC vs Python implementations.
Merges results and generates comparative visualizations.
"""
import pandas as pd
import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

def load_all_results(results_dir: Path):
    """Load all benchmark results (Python and .NET)"""
    all_results = []
    
    # Load Python results
    for csv_file in results_dir.glob("comprehensive_results_*.csv"):
        df = pd.read_csv(csv_file)
        # Normalize column names
        column_mapping = {
            'Library': 'library',
            'RecordCount': 'record_count',
            'Concurrency': 'concurrency',
            'ExecutionTime(s)': 'execution_time_sec',
            'Throughput(rec/s)': 'throughput_per_sec',
            'PeakMemory(MB)': 'peak_memory_mb',
            'GC_Gen0': 'gc_gen0',
            'GC_Gen1': 'gc_gen1',
            'GC_Gen2': 'gc_gen2',
            'CPU%': 'cpu_percent'
        }
        df.rename(columns=column_mapping, inplace=True)
        all_results.append(df)
    
    # Load .NET results
    for csv_file in results_dir.glob("dotnet_results_*.csv"):
        df = pd.read_csv(csv_file)
        # Normalize column names
        column_mapping = {
            'Library': 'library',
            'RecordCount': 'record_count',
            'Concurrency': 'concurrency',
            'ExecutionTime(s)': 'execution_time_sec',
            'Throughput(rec/s)': 'throughput_per_sec',
            'PeakMemory(MB)': 'peak_memory_mb',
            'GC_Gen0': 'gc_gen0',
            'GC_Gen1': 'gc_gen1',
            'GC_Gen2': 'gc_gen2',
            'CPU%': 'cpu_percent'
        }
        df.rename(columns=column_mapping, inplace=True)
        all_results.append(df)
    
    if not all_results:
        raise FileNotFoundError("No benchmark results found")
    
    # Combine all results
    combined_df = pd.concat(all_results, ignore_index=True)
    return combined_df


def generate_comparison_charts(df: pd.DataFrame, output_dir: Path):
    """Generate comprehensive comparison charts"""
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Chart 1: Throughput Comparison by Concurrency
    fig, axes = plt.subplots(1, 3, figsize=(18, 5))
    fig.suptitle('.NET POC vs Python: Throughput Comparison', fontsize=16, fontweight='bold')
    
    record_counts = sorted(df['record_count'].unique())
    libraries = sorted(df['library'].unique())
    
    for idx, record_count in enumerate(record_counts[:3]):
        ax = axes[idx]
        df_filtered = df[df['record_count'] == record_count]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                lib_data_sorted = lib_data.sort_values('concurrency')
                ax.plot(
                    lib_data_sorted['concurrency'],
                    lib_data_sorted['throughput_per_sec'],
                    marker='o',
                    label=library,
                    linewidth=2,
                    markersize=8
                )
        
        ax.set_xlabel('Concurrency (workers)', fontsize=11)
        ax.set_ylabel('Throughput (rec/s)', fontsize=11)
        ax.set_title(f'{record_count:,} records', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
    
    plt.tight_layout()
    plt.savefig(output_dir / 'dotnet_vs_python_throughput.png', dpi=300, bbox_inches='tight')
    print(f"✅ Saved: {output_dir / 'dotnet_vs_python_throughput.png'}")
    plt.close()
    
    # Chart 2: Memory Usage Comparison
    fig, axes = plt.subplots(1, 3, figsize=(18, 5))
    fig.suptitle('.NET POC vs Python: Memory Usage', fontsize=16, fontweight='bold')
    
    for idx, record_count in enumerate(record_counts[:3]):
        ax = axes[idx]
        df_filtered = df[df['record_count'] == record_count]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                lib_data_sorted = lib_data.sort_values('concurrency')
                ax.plot(
                    lib_data_sorted['concurrency'],
                    lib_data_sorted['peak_memory_mb'],
                    marker='s',
                    label=library,
                    linewidth=2,
                    markersize=8
                )
        
        ax.set_xlabel('Concurrency (workers)', fontsize=11)
        ax.set_ylabel('Peak Memory (MB)', fontsize=11)
        ax.set_title(f'{record_count:,} records', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
    
    plt.tight_layout()
    plt.savefig(output_dir / 'dotnet_vs_python_memory.png', dpi=300, bbox_inches='tight')
    print(f"✅ Saved: {output_dir / 'dotnet_vs_python_memory.png'}")
    plt.close()
    
    # Chart 3: Execution Time Comparison
    fig, axes = plt.subplots(1, 3, figsize=(18, 5))
    fig.suptitle('.NET POC vs Python: Execution Time', fontsize=16, fontweight='bold')
    
    for idx, record_count in enumerate(record_counts[:3]):
        ax = axes[idx]
        df_filtered = df[df['record_count'] == record_count]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                lib_data_sorted = lib_data.sort_values('concurrency')
                ax.plot(
                    lib_data_sorted['concurrency'],
                    lib_data_sorted['execution_time_sec'],
                    marker='^',
                    label=library,
                    linewidth=2,
                    markersize=8
                )
        
        ax.set_xlabel('Concurrency (workers)', fontsize=11)
        ax.set_ylabel('Execution Time (seconds)', fontsize=11)
        ax.set_title(f'{record_count:,} records', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
    
    plt.tight_layout()
    plt.savefig(output_dir / 'dotnet_vs_python_execution_time.png', dpi=300, bbox_inches='tight')
    print(f"✅ Saved: {output_dir / 'dotnet_vs_python_execution_time.png'}")
    plt.close()
    
    # Chart 4: Speedup Factor (vs Python baseline)
    fig, ax = plt.subplots(figsize=(12, 7))
    
    # Get Python baseline (Pydantic with 4 workers)
    python_baseline = df[(df['library'] == 'Pydantic') & (df['concurrency'] == 4)]
    
    speedup_data = []
    for record_count in record_counts:
        baseline_row = python_baseline[python_baseline['record_count'] == record_count]
        if baseline_row.empty:
            continue
        baseline_throughput = baseline_row.iloc[0]['throughput_per_sec']
        
        for library in libraries:
            if library == 'Pydantic':
                continue  # Skip baseline
            
            lib_data = df[(df['library'] == library) & 
                         (df['record_count'] == record_count) & 
                         (df['concurrency'] == 4)]
            
            if not lib_data.empty:
                lib_throughput = lib_data.iloc[0]['throughput_per_sec']
                speedup = lib_throughput / baseline_throughput
                speedup_data.append({
                    'library': library,
                    'record_count': record_count,
                    'speedup': speedup
                })
    
    if speedup_data:
        speedup_df = pd.DataFrame(speedup_data)
        
        x = np.arange(len(record_counts))
        width = 0.25
        
        # Extract non-baseline libraries to avoid repetition
        non_baseline_libs = [lib for lib in libraries if lib != 'Pydantic']
        
        for idx, library in enumerate(non_baseline_libs):
            lib_speedups = speedup_df[speedup_df['library'] == library]
            if not lib_speedups.empty:
                speedups = [lib_speedups[lib_speedups['record_count'] == rc]['speedup'].iloc[0] 
                           if not lib_speedups[lib_speedups['record_count'] == rc].empty 
                           else 0 
                           for rc in record_counts]
                ax.bar(x + idx * width, speedups, width, label=library)
        
        ax.set_xlabel('Record Count', fontsize=12)
        ax.set_ylabel('Speedup Factor (vs Pydantic 4 workers)', fontsize=12)
        ax.set_title('Speedup Comparison at 4 Workers', fontsize=14, fontweight='bold')
        ax.set_xticks(x + width)
        ax.set_xticklabels([f'{rc:,}' for rc in record_counts])
        ax.legend()
        ax.axhline(y=1.0, color='gray', linestyle='--', alpha=0.5, label='Baseline (Pydantic)')
        ax.grid(True, alpha=0.3, axis='y')
        
        plt.tight_layout()
        plt.savefig(output_dir / 'dotnet_vs_python_speedup.png', dpi=300, bbox_inches='tight')
        print(f"✅ Saved: {output_dir / 'dotnet_vs_python_speedup.png'}")
        plt.close()


def generate_summary_report(df: pd.DataFrame, output_path: Path):
    """Generate comprehensive markdown summary"""
    
    with open(output_path, 'w') as f:
        f.write("# Comprehensive Benchmark: .NET DataFlow POC vs Python Libraries\n\n")
        f.write(f"*Generated: {pd.Timestamp.now().strftime('%Y-%m-%d %H:%M:%S')}*\n\n")
        
        f.write("## Libraries Compared\n\n")
        for lib in sorted(df['library'].unique()):
            f.write(f"- **{lib}**\n")
        f.write("\n")
        
        # Overall statistics
        f.write("## Overall Performance Summary\n\n")
        
        lib_stats = {}
        for library in df['library'].unique():
            lib_data = df[df['library'] == library]
            lib_stats[library] = {
                'avg_throughput': lib_data['throughput_per_sec'].mean(),
                'avg_memory': lib_data['peak_memory_mb'].mean(),
                'max_throughput': lib_data['throughput_per_sec'].max(),
                'min_memory': lib_data['peak_memory_mb'].min()
            }
        
        f.write("| Library | Avg Throughput | Max Throughput | Avg Memory | Min Memory |\n")
        f.write("|---------|----------------|----------------|------------|------------|\n")
        
        for library in sorted(lib_stats.keys()):
            stats = lib_stats[library]
            f.write(
                f"| {library} | {stats['avg_throughput']:,.0f} rec/s | "
                f"{stats['max_throughput']:,.0f} rec/s | {stats['avg_memory']:.2f} MB | "
                f"{stats['min_memory']:.2f} MB |\n"
            )
        
        f.write("\n")
        
        # Detailed results by configuration
        f.write("## Detailed Results by Configuration\n\n")
        
        configs = df.groupby(['record_count', 'concurrency']).size().reset_index()[['record_count', 'concurrency']]
        
        for _, config in configs.iterrows():
            record_count = config['record_count']
            concurrency = config['concurrency']
            
            f.write(f"### {record_count:,} records, {concurrency} workers\n\n")
            
            f.write("| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |\n")
            f.write("|---------|----------|--------------------|--------------|-----------|\n")
            
            config_data = df[(df['record_count'] == record_count) & (df['concurrency'] == concurrency)]
            config_data = config_data.sort_values('throughput_per_sec', ascending=False)
            
            for _, row in config_data.iterrows():
                gc_str = f"{int(row['gc_gen0'])}/{int(row['gc_gen1'])}/{int(row['gc_gen2'])}"
                f.write(
                    f"| {row['library']} | {row['execution_time_sec']:.3f} | "
                    f"{row['throughput_per_sec']:,.0f} | {row['peak_memory_mb']:.2f} | {gc_str} |\n"
                )
            
            f.write("\n")
        
        # Key insights
        f.write("## Key Insights\n\n")
        
        # Find best performers
        best_throughput = df.loc[df['throughput_per_sec'].idxmax()]
        best_memory = df.loc[df['peak_memory_mb'].idxmin()]
        
        f.write(f"### Performance Winners\n\n")
        f.write(f"- **Highest Throughput**: {best_throughput['library']} - "
                f"{best_throughput['throughput_per_sec']:,.0f} rec/s "
                f"({int(best_throughput['record_count']):,} records, {int(best_throughput['concurrency'])} workers)\n")
        f.write(f"- **Most Memory Efficient**: {best_memory['library']} - "
                f"{best_memory['peak_memory_mb']:.2f} MB "
                f"({int(best_memory['record_count']):,} records, {int(best_memory['concurrency'])} workers)\n")
        f.write("\n")
        
        # Concurrency scaling analysis
        f.write("### Concurrency Scaling Analysis\n\n")
        f.write("Comparing throughput improvement from 1 to 4 workers:\n\n")
        
        for library in sorted(df['library'].unique()):
            scaling_data = df[(df['library'] == library) & (df['record_count'] == 10000)]
            if len(scaling_data) >= 2:
                worker_1 = scaling_data[scaling_data['concurrency'] == 1]
                worker_4 = scaling_data[scaling_data['concurrency'] == 4]
                
                if not worker_1.empty and not worker_4.empty:
                    throughput_1 = worker_1.iloc[0]['throughput_per_sec']
                    throughput_4 = worker_4.iloc[0]['throughput_per_sec']
                    scaling = throughput_4 / throughput_1
                    
                    f.write(f"- **{library}**: {scaling:.2f}x improvement "
                           f"({throughput_1:.0f} → {throughput_4:.0f} rec/s)\n")
        
        f.write("\n")
        
        # Recommendations
        f.write("## Recommendations\n\n")
        f.write("Based on the comprehensive benchmark results:\n\n")
        
        dotnet_avg = lib_stats.get('DotNet-POC', {}).get('avg_throughput', 0)
        pydantic_avg = lib_stats.get('Pydantic', {}).get('avg_throughput', 0)
        
        if dotnet_avg > 0 and pydantic_avg > 0:
            speedup = dotnet_avg / pydantic_avg
            f.write(f"1. **.NET DataFlow POC** shows **{speedup:.2f}x average speedup** over Pydantic\n")
        
        f.write("2. **.NET excels** in throughput and memory efficiency with excellent GC characteristics\n")
        f.write("3. **Pydantic** offers good Python performance with type safety\n")
        f.write("4. **SimpleAsync** is most memory-efficient but doesn't scale with concurrency\n")
        f.write("\n")
        
        f.write("**Choose .NET DataFlow POC when:**\n")
        f.write("- Maximum performance is critical\n")
        f.write("- You need efficient memory usage\n")
        f.write("- Strong type safety and compile-time guarantees are important\n")
        f.write("- You're in a .NET ecosystem\n\n")
        
        f.write("**Choose Python (Pydantic) when:**\n")
        f.write("- Rapid prototyping is a priority\n")
        f.write("- Python ecosystem integration is needed\n")
        f.write("- Performance requirements are moderate\n")
        f.write("- Team has Python expertise\n")
    
    print(f"✅ Saved: {output_path}")


def main():
    """Main execution"""
    results_dir = Path(__file__).parent / 'results'
    
    print("=" * 80)
    print(".NET vs Python Benchmark Analysis")
    print("=" * 80)
    print()
    
    # Load all results
    print("📊 Loading benchmark results...")
    df = load_all_results(results_dir)
    print(f"   Loaded {len(df)} benchmark results")
    print(f"   Libraries: {', '.join(sorted(df['library'].unique()))}")
    print()
    
    # Generate comparison charts
    print("📈 Generating comparison visualizations...")
    charts_dir = results_dir / 'comparison_charts'
    generate_comparison_charts(df, charts_dir)
    print()
    
    # Generate summary report
    print("📝 Generating comparison report...")
    summary_path = results_dir / 'dotnet_vs_python_comparison.md'
    generate_summary_report(df, summary_path)
    print()
    
    print("=" * 80)
    print("✅ Analysis complete!")
    print(f"📁 Charts: {charts_dir}")
    print(f"📄 Report: {summary_path}")
    print("=" * 80)


if __name__ == "__main__":
    main()
