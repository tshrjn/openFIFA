using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using System.Collections.Generic;

namespace OpenFifa.Editor
{
    public static class TestRunnerHelper
    {
        private static List<string> _results = new List<string>();
        private static int _passed;
        private static int _failed;
        private static int _total;
        private static bool _running;

        public static void Execute()
        {
            RunEditModeTests();
        }

        public static void RunEditModeTests()
        {
            _results.Clear();
            _passed = 0;
            _failed = 0;
            _total = 0;
            _running = true;

            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new TestCallbacks();
            testRunnerApi.RegisterCallbacks(callbacks);

            var filter = new Filter
            {
                testMode = TestMode.EditMode
            };

            Debug.Log("[TestRunner] Starting EditMode test suite...");
            testRunnerApi.Execute(new ExecutionSettings(filter));
        }

        private class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[TestRunner] Run started: {testsToRun.TestCaseCount} test cases");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[TestRunner] ===== RESULTS =====");
                Debug.Log($"[TestRunner] Total: {_total} | Passed: {_passed} | Failed: {_failed}");

                if (_failed > 0)
                {
                    Debug.LogWarning($"[TestRunner] {_failed} FAILURES:");
                    foreach (var f in _results)
                        Debug.LogWarning(f);
                }
                else
                {
                    Debug.Log($"[TestRunner] ALL {_passed} TESTS PASSED!");
                }

                _running = false;
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    _total++;
                    if (result.TestStatus == TestStatus.Passed)
                    {
                        _passed++;
                    }
                    else if (result.TestStatus == TestStatus.Failed)
                    {
                        _failed++;
                        _results.Add($"  FAIL: {result.FullName} — {result.Message}");
                    }
                }
            }
        }
    }
}
