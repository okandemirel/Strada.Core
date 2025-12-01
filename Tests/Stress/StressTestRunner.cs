using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace Strada.Core.Tests.Stress
{
    public static class StressTestRunner
    {
        public static async Task RunAsync(string testName, Func<Task> testAction, int iterations = 1)
        {
            UnityEngine.Debug.Log($"[StressTest] Starting: {testName} (Iterations: {iterations})");
            var sw = Stopwatch.StartNew();

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    await testAction();
                }
                sw.Stop();
                UnityEngine.Debug.Log($"[StressTest] Completed: {testName} in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                UnityEngine.Debug.LogError($"[StressTest] Failed: {testName} after {sw.ElapsedMilliseconds}ms. Error: {ex}");
                throw;
            }
        }

        public static void Run(string testName, Action testAction, int iterations = 1)
        {
            UnityEngine.Debug.Log($"[StressTest] Starting: {testName} (Iterations: {iterations})");
            var sw = Stopwatch.StartNew();

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    testAction();
                }
                sw.Stop();
                UnityEngine.Debug.Log($"[StressTest] Completed: {testName} in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                UnityEngine.Debug.LogError($"[StressTest] Failed: {testName} after {sw.ElapsedMilliseconds}ms. Error: {ex}");
                throw;
            }
        }
    }
}
