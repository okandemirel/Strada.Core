using System;
using System.Collections.Generic;

namespace Strada.Core.Editor.Benchmarking
{
    /// <summary>
    /// Result of a single benchmark execution.
    /// </summary>
    [Serializable]
    public class BenchmarkResult
    {
        public string Name;
        public string Category;
        public DateTime Timestamp;
        public int Iterations;
        public double TotalTimeMs;
        public double AverageTimeMs;
        public double OperationsPerSecond;
        public long MemoryAllocatedBytes;
        public double MinTimeMs;
        public double MaxTimeMs;
        public double StandardDeviation;
        public bool Passed;
        public string ErrorMessage;
        
        /// <summary>
        /// Calculates statistics from raw timing data.
        /// </summary>
        public static BenchmarkResult Calculate(string name, string category, double[] timingsMs, long memoryBytes, int iterations)
        {
            if (timingsMs == null || timingsMs.Length == 0)
            {
                return new BenchmarkResult
                {
                    Name = name,
                    Category = category,
                    Timestamp = DateTime.Now,
                    Iterations = iterations,
                    Passed = false,
                    ErrorMessage = "No timing data available"
                };
            }
            
            double sum = 0;
            double min = double.MaxValue;
            double max = double.MinValue;
            
            foreach (var t in timingsMs)
            {
                sum += t;
                if (t < min) min = t;
                if (t > max) max = t;
            }
            
            double average = sum / timingsMs.Length;
            double totalTime = sum;

            double sumSquaredDiff = 0;
            foreach (var t in timingsMs)
            {
                double diff = t - average;
                sumSquaredDiff += diff * diff;
            }
            double stdDev = Math.Sqrt(sumSquaredDiff / timingsMs.Length);

            double opsPerSecond = iterations > 0 && totalTime > 0 
                ? (iterations / (totalTime / 1000.0))
                : 0;

            return new BenchmarkResult
            {
                Name = name,
                Category = category,
                Timestamp = DateTime.Now,
                Iterations = iterations,
                TotalTimeMs = totalTime,
                AverageTimeMs = average,
                OperationsPerSecond = opsPerSecond,
                MemoryAllocatedBytes = memoryBytes,
                MinTimeMs = min == double.MaxValue ? 0 : min,
                MaxTimeMs = max == double.MinValue ? 0 : max,
                StandardDeviation = stdDev,
                Passed = true
            };
        }
    }
    
    /// <summary>
    /// Comparison between two benchmark results.
    /// </summary>
    [Serializable]
    public class BenchmarkComparison
    {
        public BenchmarkResult Baseline;
        public BenchmarkResult Current;
        public double PercentageChange;
        public bool IsRegression;
        
        /// <summary>
        /// Compares two benchmark results and determines if there's a regression.
        /// </summary>
        /// <param name="baseline">The baseline result to compare against.</param>
        /// <param name="current">The current result.</param>
        /// <param name="regressionThreshold">Percentage threshold for regression detection (default 10%).</param>
        public static BenchmarkComparison Compare(BenchmarkResult baseline, BenchmarkResult current, double regressionThreshold = 10.0)
        {
            if (baseline == null || current == null)
            {
                return new BenchmarkComparison
                {
                    Baseline = baseline,
                    Current = current,
                    PercentageChange = 0,
                    IsRegression = false
                };
            }

            double percentageChange = 0;
            if (baseline.AverageTimeMs > 0)
            {
                percentageChange = ((current.AverageTimeMs - baseline.AverageTimeMs) / baseline.AverageTimeMs) * 100.0;
            }
            
            return new BenchmarkComparison
            {
                Baseline = baseline,
                Current = current,
                PercentageChange = percentageChange,
                IsRegression = percentageChange > regressionThreshold
            };
        }
    }

    
    /// <summary>
    /// Definition of a benchmark that can be executed.
    /// </summary>
    public class BenchmarkDefinition
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int DefaultIterations { get; set; } = 1000;
        public Func<int, BenchmarkResult> Execute { get; set; }
        public double? MinimumThreshold { get; set; }
        public double? MaximumTimeMs { get; set; }
    }
    
    /// <summary>
    /// Threshold configuration for benchmark warnings.
    /// </summary>
    [Serializable]
    public class BenchmarkThreshold
    {
        public string BenchmarkName;
        public double? MinOpsPerSecond;
        public double? MaxAverageTimeMs;
        public long? MaxMemoryBytes;
        
        public bool CheckPassed(BenchmarkResult result)
        {
            if (result == null) return false;
            
            if (MinOpsPerSecond.HasValue && result.OperationsPerSecond < MinOpsPerSecond.Value)
                return false;
            
            if (MaxAverageTimeMs.HasValue && result.AverageTimeMs > MaxAverageTimeMs.Value)
                return false;
            
            if (MaxMemoryBytes.HasValue && result.MemoryAllocatedBytes > MaxMemoryBytes.Value)
                return false;
            
            return true;
        }
        
        public string GetFailureReason(BenchmarkResult result)
        {
            if (result == null) return "No result";
            
            var reasons = new List<string>();
            
            if (MinOpsPerSecond.HasValue && result.OperationsPerSecond < MinOpsPerSecond.Value)
                reasons.Add($"Ops/sec {result.OperationsPerSecond:F0} < minimum {MinOpsPerSecond.Value:F0}");
            
            if (MaxAverageTimeMs.HasValue && result.AverageTimeMs > MaxAverageTimeMs.Value)
                reasons.Add($"Avg time {result.AverageTimeMs:F3}ms > maximum {MaxAverageTimeMs.Value:F3}ms");
            
            if (MaxMemoryBytes.HasValue && result.MemoryAllocatedBytes > MaxMemoryBytes.Value)
                reasons.Add($"Memory {result.MemoryAllocatedBytes} bytes > maximum {MaxMemoryBytes.Value} bytes");
            
            return reasons.Count > 0 ? string.Join("; ", reasons) : "Passed";
        }
    }
    
    /// <summary>
    /// Collection of benchmark results for persistence.
    /// </summary>
    [Serializable]
    public class BenchmarkSession
    {
        public string SessionId;
        public DateTime Timestamp;
        public string UnityVersion;
        public string Platform;
        public List<BenchmarkResult> Results = new List<BenchmarkResult>();
        
        public static BenchmarkSession Create()
        {
            return new BenchmarkSession
            {
                SessionId = Guid.NewGuid().ToString("N").Substring(0, 8),
                Timestamp = DateTime.Now,
                UnityVersion = UnityEngine.Application.unityVersion,
                Platform = UnityEngine.Application.platform.ToString()
            };
        }
    }
}
