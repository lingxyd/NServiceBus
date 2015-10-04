namespace NServiceBus.Transports.FileBased
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Extensibility;

    class MessagePump : IPushMessages
    {
        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
            pipeline = pipe;

            path = settings.InputQueue;
            purgeOnStartup = settings.PurgeOnStartup;
        }

        string path;
        bool purgeOnStartup;

        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;

            if (purgeOnStartup)
            {
                Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }

            messagePumpTask = Task.Factory.StartNew(() => ProcessMessages(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var allTasks = runningReceiveTasks.Values.Concat(new[] { messagePumpTask });
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            runningReceiveTasks.Clear();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            try
            {
                await InnerProcessMessages().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // For graceful shutdown purposes
            }
            catch (Exception ex)
            {
                Logger.Error("File Message pump failed", ex);
                //await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await ProcessMessages().ConfigureAwait(false);
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var filesFound = false;

                foreach (var file in Directory.EnumerateFiles(path, "*.msg"))
                {
                    filesFound = true;


                    var messageId = Path.GetFileNameWithoutExtension(file);
                    var transactionDir = Path.Combine(path, "tx-" + messageId);
                    Directory.CreateDirectory(transactionDir);

                    var fileToProcess = Path.Combine(transactionDir, messageId + ".incoming");
                    File.Move(file, fileToProcess);

                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var message = File.ReadAllLines(fileToProcess);
                            var bodyPath = message.First();
                            var headers = DeserializeHeaders(message.Skip(1).ToArray());

                            using (var bodyStream = new FileStream(bodyPath, FileMode.Open))
                            {
                                var pushContext = new PushContext(new IncomingMessage(messageId, headers, bodyStream), new ContextBag());
                                await pipeline(pushContext).ConfigureAwait(false);
                            }

                            //todo: commit by moving outgoing messages to their destinations and remove the body file
                        }
                        catch (Exception)
                        {
                            //rollback by moving the file back to the main dir
                            File.Move(fileToProcess, file);
                        }
                        finally
                        {
                            Directory.Delete(transactionDir, true);
                        }

                    }, cancellationToken);

                    task.ContinueWith(t =>
                    {
                        Task toBeRemoved;
                        runningReceiveTasks.TryRemove(t, out toBeRemoved);
                    }, TaskContinuationOptions.ExecuteSynchronously)
                    .Ignore();

                    runningReceiveTasks.AddOrUpdate(task, task, (k, v) => task)
                        .Ignore();
                }

                if (!filesFound)
                {
                    await Task.Delay(10, cancellationToken);
                }
            }

        }

        Dictionary<string, string> DeserializeHeaders(string[] headerLines)
        {
            var headers = new Dictionary<string, string>();
            for (var i = 0; i < headerLines.Count() / 2; i++)
            {
                var index = i * 2;
                headers.Add(headerLines[index], headerLines[index + 1]);

            }
            return headers;
        }


        Task messagePumpTask;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        Func<PushContext, Task> pipeline;

        static ILog Logger = LogManager.GetLogger<MessagePump>();

    }
}