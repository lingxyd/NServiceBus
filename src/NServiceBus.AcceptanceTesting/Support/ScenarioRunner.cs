﻿namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Customization;

    public class ScenarioRunner
    {
        public static async Task Run(IList<RunDescriptor> runDescriptors, IList<EndpointBehavior> behaviorDescriptors, IList<IScenarioVerification> shoulds, Func<ScenarioContext, bool> done, int limitTestParallelismTo, Action<RunSummary> reports, Func<Exception, bool> allowedExceptions)
        {
            var totalRuns = runDescriptors.Count();
            var cts = new CancellationTokenSource();
            var po = new ParallelOptions
            {
                CancellationToken = cts.Token
            };
            var maxParallelismSetting = Environment.GetEnvironmentVariable("max_test_parallelism");
            int maxParallelism;

            if (int.TryParse(maxParallelismSetting, out maxParallelism))
            {
                Console.WriteLine("Parallelism limited to: {0}", maxParallelism);

                po.MaxDegreeOfParallelism = maxParallelism;
            }

            if (limitTestParallelismTo > 0)
            {
                po.MaxDegreeOfParallelism = limitTestParallelismTo;
            }

            var results = new ConcurrentBag<RunSummary>();

            try
            {
                var runs = runDescriptors.Select(runDescriptor => Task.Run(async () =>
                {
                    if (po.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Console.WriteLine("{0} - Started @ {1}", runDescriptor.Key, DateTime.Now.ToString(CultureInfo.InvariantCulture));

                    ContextAppender.SetContext(runDescriptor.ScenarioContext);
                    var runResult = await PerformTestRun(behaviorDescriptors, shoulds, runDescriptor, done, allowedExceptions).ConfigureAwait(false);
                    ContextAppender.SetContext(null);

                    Console.WriteLine("{0} - Finished @ {1}", runDescriptor.Key, DateTime.Now.ToString(CultureInfo.InvariantCulture));

                    results.Add(new RunSummary
                    {
                        Result = runResult,
                        RunDescriptor = runDescriptor,
                        Endpoints = behaviorDescriptors
                    });

                    if (runResult.Failed)
                    {
                        cts.Cancel();
                    }
                }, cts.Token));

                await Task.WhenAll(runs).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Test run aborted due to test failures");
            }

            var failedRuns = results.Where(s => s.Result.Failed).ToList();

            foreach (var runSummary in failedRuns)
            {
                DisplayRunResult(runSummary, totalRuns);
            }

            if (failedRuns.Any())
            {
                throw new AggregateException("Test run failed due to one or more exception", failedRuns.Select(f => f.Result.Exception));
            }

            foreach (var runSummary in results.Where(s => !s.Result.Failed))
            {
                DisplayRunResult(runSummary, totalRuns);

                reports?.Invoke(runSummary);
            }
        }

        static void DisplayRunResult(RunSummary summary, int totalRuns)
        {
            var runDescriptor = summary.RunDescriptor;
            var runResult = summary.Result;

            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("Test summary for: {0}", runDescriptor.Key);
            if (totalRuns > 1)
            {
                Console.WriteLine(" - Permutation: {0}({1})", runDescriptor.Permutation, totalRuns);
            }
            Console.WriteLine();

            PrintSettings(runDescriptor.Settings);

            Console.WriteLine();
            Console.WriteLine("Endpoints:");

            foreach (var endpoint in runResult.ActiveEndpoints)
            {
                Console.WriteLine("     - {0}", endpoint);
            }

            if (runResult.Failed)
            {
                Console.WriteLine("Test failed: {0}", runResult.Exception);
            }
            else
            {
                Console.WriteLine("Result: Successful - Duration: {0}", runResult.TotalTime);
            }

            //dump trace and context regardless since asserts outside the should could still fail the test
            Console.WriteLine();
            Console.WriteLine("Context:");

            foreach (var prop in runResult.ScenarioContext.GetType().GetProperties())
            {
                if (prop.Name == "Trace")
                {
                    continue;
                }

                Console.WriteLine("{0} = {1}", prop.Name, prop.GetValue(runResult.ScenarioContext, null));
            }

            Console.WriteLine();
            Console.WriteLine("Trace:");
            Console.WriteLine(runResult.ScenarioContext.Trace);
            Console.WriteLine("------------------------------------------------------");
        }

        static async Task<RunResult> PerformTestRun(IList<EndpointBehavior> behaviorDescriptors, IList<IScenarioVerification> shoulds, RunDescriptor runDescriptor, Func<ScenarioContext, bool> done, Func<Exception, bool> allowedExceptions)
        {
            var runResult = new RunResult
            {
                ScenarioContext = runDescriptor.ScenarioContext
            };

            var runTimer = new Stopwatch();

            runTimer.Start();

            try
            {
                var runners = await InitializeRunners(runDescriptor, behaviorDescriptors).ConfigureAwait(false);

                runResult.ActiveEndpoints = runners.Select(r => r.EndpointName).ToList();

                await PerformScenarios(runDescriptor, runners, () => done(runDescriptor.ScenarioContext), allowedExceptions).ConfigureAwait(false);

                runTimer.Stop();

                foreach (var v in shoulds.Where(s => s.ContextType == runDescriptor.ScenarioContext.GetType()))
                {
                    v.Verify(runDescriptor.ScenarioContext);
                }
            }
            catch (Exception ex)
            {
                runResult.Failed = true;
                runResult.Exception = ex;
            }

            runResult.TotalTime = runTimer.Elapsed;

            return runResult;
        }

        static IDictionary<Type, string> CreateRoutingTable(IEnumerable<EndpointBehavior> behaviorDescriptors)
        {
            var routingTable = new Dictionary<Type, string>();

            foreach (var behaviorDescriptor in behaviorDescriptors)
            {
                routingTable[behaviorDescriptor.EndpointBuilderType] = GetEndpointNameForRun(behaviorDescriptor);
            }

            return routingTable;
        }

        static void PrintSettings(IEnumerable<KeyValuePair<string, string>> settings)
        {
            Console.WriteLine();
            Console.WriteLine("Using settings:");
            foreach (var pair in settings)
            {
                Console.WriteLine("   {0}: {1}", pair.Key, pair.Value);
            }
            Console.WriteLine();
        }

        static async Task PerformScenarios(RunDescriptor runDescriptor, IEnumerable<ActiveRunner> runners, Func<bool> done, Func<Exception, bool> allowedExceptions)
        {
            var cts = new CancellationTokenSource();
            var endpoints = runners.Select(r => r.Instance).ToList();

            await StartEndpoints(endpoints, allowedExceptions, cts).ConfigureAwait(false);
            runDescriptor.ScenarioContext.EndpointsStarted = true;
            await ExecuteWhens(endpoints, allowedExceptions, cts).ConfigureAwait(false);

            var startTime = DateTime.UtcNow;
            var maxTime = runDescriptor.TestExecutionTimeout;

            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            try
            {
                while (!done() && !cts.Token.IsCancellationRequested)
                {
                    if ((DateTime.UtcNow - startTime) > maxTime)
                    {
                        throw new ScenarioException(GenerateTestTimedOutMessage(maxTime));
                    }

                    await Task.Delay(1).ConfigureAwait(false);
                }
            }
            finally
            {
                await StopEndpoints(endpoints).ConfigureAwait(false);
            }

            var exceptions = runDescriptor.ScenarioContext.Exceptions
                        .Where(ex => !allowedExceptions(ex))
                        .ToList();

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        static string GenerateTestTimedOutMessage(TimeSpan maxTime)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"The maximum time limit for this test({maxTime.TotalSeconds}s) has been reached");
            sb.AppendLine("----------------------------------------------------------------------------");

            return sb.ToString();
        }

        static async Task StartEndpoints(IEnumerable<EndpointRunner> endpoints, Func<Exception, bool> allowedExceptions, CancellationTokenSource cts)
        {
            var tasks = endpoints.Select(endpoint => StartEndpoint(endpoint, allowedExceptions, cts));
            var whenAll = Task.WhenAll(tasks);
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            var completedTask = await Task.WhenAny(whenAll, timeoutTask).ConfigureAwait(false);

            if (completedTask.Equals(timeoutTask))
            {
                throw new Exception("Starting endpoints took longer than 2 minutes");
            }
        }

        static async Task StartEndpoint(EndpointRunner endpoint, Func<Exception, bool> allowedExceptions, CancellationTokenSource cts)
        {
            var token = cts.Token;
            var result = await endpoint.Start(token).ConfigureAwait(false);

            if (result.Failed && !allowedExceptions(result.Exception))
            {
                cts.Cancel();
                throw new ScenarioException("Endpoint failed to start", result.Exception);
            }
        }

        static async Task ExecuteWhens(IEnumerable<EndpointRunner> endpoints, Func<Exception, bool> allowedExceptions, CancellationTokenSource cts)
        {
            var tasks = endpoints.Select(endpoint => ExecuteWhens(endpoint, allowedExceptions, cts));
            var whenAll = Task.WhenAll(tasks);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(whenAll, timeoutTask).ConfigureAwait(false);

            if (completedTask.Equals(timeoutTask))
            {
                throw new Exception("Executing given and whens took longer than 2 minutes");
            }
        }

        static async Task ExecuteWhens(EndpointRunner endpoint, Func<Exception, bool> allowedExceptions, CancellationTokenSource cts)
        {
            var token = cts.Token;
            var result = await endpoint.Whens(token);
            if (result.Failed && !allowedExceptions(result.Exception))
            {
                cts.Cancel();
                throw new ScenarioException("Whens failed to execute", result.Exception);
            }
        }

        static async Task StopEndpoints(IEnumerable<EndpointRunner> endpoints)
        {
            // We can get rid of Task.Run when stop is async
            var tasks = endpoints.Select(endpoint => Task.Run(async () =>
            {
                Console.WriteLine("Stopping endpoint: {0}", endpoint.Name());
                var sw = new Stopwatch();
                sw.Start();
                var result = await endpoint.Stop().ConfigureAwait(false);
                sw.Stop();
                if (result.Failed)
                {
                    throw new ScenarioException("Endpoint failed to stop", result.Exception);
                }

                Console.WriteLine("Endpoint: {0} stopped ({1}s)", endpoint.Name(), sw.Elapsed);
            })).ToArray();

            var whenAll = Task.WhenAll(tasks);
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            var completedTask = await Task.WhenAny(whenAll, timeoutTask).ConfigureAwait(false);

            if (completedTask.Equals(timeoutTask))
            {
                throw new Exception("Stopping endpoints took longer than 2 minutes");
            }
        }

        static async Task<List<ActiveRunner>> InitializeRunners(RunDescriptor runDescriptor, IList<EndpointBehavior> behaviorDescriptors)
        {
            var runners = new List<ActiveRunner>();
            var routingTable = CreateRoutingTable(behaviorDescriptors);

            foreach (var behaviorDescriptor in behaviorDescriptors)
            {
                var endpointName = GetEndpointNameForRun(behaviorDescriptor);

                if (endpointName.Length > 77)
                {
                    throw new Exception($"Endpoint name '{endpointName}' is larger than 77 characters and will cause issues with MSMQ queue names. Please rename your test class or endpoint.");
                }

                var runner = new ActiveRunner
                {
                    Instance = new EndpointRunner(),
                    EndpointName = endpointName,
                };

                runner.InitializeTask = Task.Run(() => runner.Instance.Initialize(runDescriptor, behaviorDescriptor, routingTable, endpointName));
                runners.Add(runner);
            }

            await Task.WhenAll(runners.Select(r => r.InitializeTask).ToArray());
            var failedRunner = runners.FirstOrDefault(r => r.InitializeTask.Result.Failed);
            if (failedRunner != null)
            {
                throw new ScenarioException($"Endpoint {failedRunner.Instance.Name()} failed to initialize", failedRunner.InitializeTask.Result.Exception);
            }
            return runners;
        }

        static string GetEndpointNameForRun(EndpointBehavior endpointBehavior)
        {
            return Conventions.EndpointNamingConvention(endpointBehavior.EndpointBuilderType);
        }
    }

    public class RunResult
    {
        public bool Failed { get; set; }

        public Exception Exception { get; set; }

        public TimeSpan TotalTime { get; set; }

        public ScenarioContext ScenarioContext { get; set; }

        public IEnumerable<string> ActiveEndpoints
        {
            get
            {
                if (activeEndpoints == null)
                    activeEndpoints = new List<string>();

                return activeEndpoints;
            }
            set { activeEndpoints = value.ToList(); }
        }

        IList<string> activeEndpoints;
    }

    public class RunSummary
    {
        public RunResult Result { get; set; }

        public RunDescriptor RunDescriptor { get; set; }

        public IEnumerable<EndpointBehavior> Endpoints { get; set; }
    }
}