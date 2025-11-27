using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Strada.Core.Editor.Benchmarking
{
    /// <summary>
    /// Executes predefined performance benchmarks for Strada framework components.
    /// Captures timing and memory metrics for DI resolution, ECS queries, and message dispatch.
    /// </summary>
    public class BenchmarkRunner
    {
        private readonly List<BenchmarkDefinition> _benchmarks = new List<BenchmarkDefinition>();
        private readonly Dictionary<string, BenchmarkThreshold> _thresholds = new Dictionary<string, BenchmarkThreshold>();
        
        public IReadOnlyList<BenchmarkDefinition> Benchmarks => _benchmarks;
        public event Action<BenchmarkResult> OnBenchmarkCompleted;
        public event Action<string> OnBenchmarkStarted;
        
        public BenchmarkRunner()
        {
            RegisterDefaultBenchmarks();
            RegisterDefaultThresholds();
        }
        
        private void RegisterDefaultBenchmarks()
        {
            // DI Container Benchmarks
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "DI_TransientResolve",
                Category = "DI Container",
                Description = "Measures transient service resolution performance",
                DefaultIterations = 10000,
                Execute = RunDITransientBenchmark
            });
            
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "DI_SingletonResolve",
                Category = "DI Container",
                Description = "Measures singleton service resolution performance",
                DefaultIterations = 10000,
                Execute = RunDISingletonBenchmark
            });
            
            // ECS Benchmarks
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "ECS_EntityCreation",
                Category = "ECS",
                Description = "Measures entity creation performance",
                DefaultIterations = 10000,
                Execute = RunECSEntityCreationBenchmark
            });
            
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "ECS_ComponentAdd",
                Category = "ECS",
                Description = "Measures component addition performance",
                DefaultIterations = 10000,
                Execute = RunECSComponentAddBenchmark
            });
            
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "ECS_ComponentQuery",
                Category = "ECS",
                Description = "Measures component query performance",
                DefaultIterations = 1000,
                Execute = RunECSQueryBenchmark
            });
            
            // Message Bus Benchmarks
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "Bus_EventPublish",
                Category = "Message Bus",
                Description = "Measures event publishing performance",
                DefaultIterations = 10000,
                Execute = RunBusEventBenchmark
            });
            
            _benchmarks.Add(new BenchmarkDefinition
            {
                Name = "Bus_CommandDispatch",
                Category = "Message Bus",
                Description = "Measures command dispatch performance",
                DefaultIterations = 10000,
                Execute = RunBusCommandBenchmark
            });
        }
        
        private void RegisterDefaultThresholds()
        {
            // Set reasonable performance thresholds
            _thresholds["DI_TransientResolve"] = new BenchmarkThreshold
            {
                BenchmarkName = "DI_TransientResolve",
                MinOpsPerSecond = 100000,
                MaxAverageTimeMs = 0.01
            };
            
            _thresholds["DI_SingletonResolve"] = new BenchmarkThreshold
            {
                BenchmarkName = "DI_SingletonResolve",
                MinOpsPerSecond = 500000,
                MaxAverageTimeMs = 0.002
            };
            
            _thresholds["ECS_EntityCreation"] = new BenchmarkThreshold
            {
                BenchmarkName = "ECS_EntityCreation",
                MinOpsPerSecond = 50000,
                MaxAverageTimeMs = 0.02
            };
            
            _thresholds["ECS_ComponentQuery"] = new BenchmarkThreshold
            {
                BenchmarkName = "ECS_ComponentQuery",
                MinOpsPerSecond = 10000,
                MaxAverageTimeMs = 0.1
            };
            
            _thresholds["Bus_EventPublish"] = new BenchmarkThreshold
            {
                BenchmarkName = "Bus_EventPublish",
                MinOpsPerSecond = 100000,
                MaxAverageTimeMs = 0.01
            };
        }
        
        public BenchmarkThreshold GetThreshold(string benchmarkName)
        {
            return _thresholds.TryGetValue(benchmarkName, out var threshold) ? threshold : null;
        }
        
        public void SetThreshold(string benchmarkName, BenchmarkThreshold threshold)
        {
            _thresholds[benchmarkName] = threshold;
        }

        
        /// <summary>
        /// Runs a single benchmark by name.
        /// </summary>
        public BenchmarkResult RunBenchmark(string name, int? iterations = null)
        {
            var benchmark = _benchmarks.FirstOrDefault(b => b.Name == name);
            if (benchmark == null)
            {
                return new BenchmarkResult
                {
                    Name = name,
                    Timestamp = DateTime.Now,
                    Passed = false,
                    ErrorMessage = $"Benchmark '{name}' not found"
                };
            }
            
            return RunBenchmark(benchmark, iterations ?? benchmark.DefaultIterations);
        }
        
        /// <summary>
        /// Runs a benchmark definition.
        /// </summary>
        public BenchmarkResult RunBenchmark(BenchmarkDefinition benchmark, int iterations)
        {
            OnBenchmarkStarted?.Invoke(benchmark.Name);
            
            try
            {
                var result = benchmark.Execute(iterations);
                
                // Check threshold if configured
                if (_thresholds.TryGetValue(benchmark.Name, out var threshold))
                {
                    if (!threshold.CheckPassed(result))
                    {
                        result.Passed = false;
                        result.ErrorMessage = threshold.GetFailureReason(result);
                    }
                }
                
                OnBenchmarkCompleted?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new BenchmarkResult
                {
                    Name = benchmark.Name,
                    Category = benchmark.Category,
                    Timestamp = DateTime.Now,
                    Iterations = iterations,
                    Passed = false,
                    ErrorMessage = ex.Message
                };
                OnBenchmarkCompleted?.Invoke(result);
                return result;
            }
        }
        
        /// <summary>
        /// Runs all benchmarks.
        /// </summary>
        public List<BenchmarkResult> RunAllBenchmarks()
        {
            var results = new List<BenchmarkResult>();
            foreach (var benchmark in _benchmarks)
            {
                results.Add(RunBenchmark(benchmark, benchmark.DefaultIterations));
            }
            return results;
        }
        
        /// <summary>
        /// Runs benchmarks in a specific category.
        /// </summary>
        public List<BenchmarkResult> RunCategory(string category)
        {
            var results = new List<BenchmarkResult>();
            foreach (var benchmark in _benchmarks.Where(b => b.Category == category))
            {
                results.Add(RunBenchmark(benchmark, benchmark.DefaultIterations));
            }
            return results;
        }
        
        /// <summary>
        /// Gets all unique categories.
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            return _benchmarks.Select(b => b.Category).Distinct();
        }
        
        #region DI Benchmarks
        
        private BenchmarkResult RunDITransientBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);

            // Create a test container using ContainerBuilder
            var container = new ContainerBuilder()
                .Register<ITestService, TestServiceImpl>(Lifetime.Transient)
                .Build();
            
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                var _ = container.Resolve<ITestService>();
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            container.Dispose();
            
            return BenchmarkResult.Calculate(
                "DI_TransientResolve",
                "DI Container",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        private BenchmarkResult RunDISingletonBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);

            var container = new ContainerBuilder()
                .Register<ITestService, TestServiceImpl>(Lifetime.Singleton)
                .Build();
            
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                var _ = container.Resolve<ITestService>();
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            container.Dispose();
            
            return BenchmarkResult.Calculate(
                "DI_SingletonResolve",
                "DI Container",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        #endregion
        
        #region ECS Benchmarks
        
        private BenchmarkResult RunECSEntityCreationBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);

            var world = new WorldBuilder().Build();
            
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                world.CreateEntity();
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            world.Dispose();
            
            return BenchmarkResult.Calculate(
                "ECS_EntityCreation",
                "ECS",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        private BenchmarkResult RunECSComponentAddBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);

            var world = new WorldBuilder().Build();
            var entities = new Entity[iterations];

            // Pre-create entities
            for (int i = 0; i < iterations; i++)
            {
                entities[i] = world.CreateEntity();
            }

            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                world.AddComponent(entities[i], new TestComponent { Value = i });
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            world.Dispose();
            
            return BenchmarkResult.Calculate(
                "ECS_ComponentAdd",
                "ECS",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        private BenchmarkResult RunECSQueryBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);

            var world = new WorldBuilder().Build();

            // Create entities with components
            for (int i = 0; i < 1000; i++)
            {
                var entity = world.CreateEntity();
                world.AddComponent(entity, new TestComponent { Value = i });
            }

            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                int count = 0;
                world.Entities.ForEach<TestComponent>((int entityIndex, ref TestComponent c) => count++);
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            world.Dispose();
            
            return BenchmarkResult.Calculate(
                "ECS_ComponentQuery",
                "ECS",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        #endregion
        
        #region Bus Benchmarks
        
        private BenchmarkResult RunBusEventBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);
            
            var bus = new StradaBus();
            int receivedCount = 0;
            bus.Subscribe<TestEvent>(e => receivedCount++);
            
            var testEvent = new TestEvent { Data = "test" };
            
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                bus.Publish(testEvent);
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            bus.Dispose();
            
            return BenchmarkResult.Calculate(
                "Bus_EventPublish",
                "Message Bus",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        private BenchmarkResult RunBusCommandBenchmark(int iterations)
        {
            var timings = new double[iterations];
            var sw = new Stopwatch();
            long memoryBefore = GC.GetTotalMemory(true);
            
            var bus = new StradaBus();
            bus.RegisterCommandHandler<TestCommand>(cmd => { /* no-op handler */ });
            
            var testCommand = new TestCommand { Id = 1 };
            
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                bus.Send(testCommand);
                sw.Stop();
                timings[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            long memoryAfter = GC.GetTotalMemory(false);
            bus.Dispose();
            
            return BenchmarkResult.Calculate(
                "Bus_CommandDispatch",
                "Message Bus",
                timings,
                memoryAfter - memoryBefore,
                iterations);
        }
        
        #endregion
        
        #region Test Types
        
        private interface ITestService { }
        private class TestServiceImpl : ITestService { }
        
        private struct TestComponent : IComponent
        {
            public int Value;
        }
        
        private struct TestEvent
        {
            public string Data;
        }
        
        private struct TestCommand
        {
            public int Id;
        }
        
        #endregion
    }
}
