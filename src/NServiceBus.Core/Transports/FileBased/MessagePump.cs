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
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;

    class MessagePump : IPushMessages
    {
        static ILog Logger = LogManager.GetLogger<MessagePump>();

        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
            pipeline = pipe;
            path = Path.Combine("c:\\bus", settings.InputQueue);
            purgeOnStartup = settings.PurgeOnStartup;
        }


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
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                messagePumpTask
            });
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

                foreach (var filePath in Directory.EnumerateFiles(path, "*.txt"))
                {
                    filesFound = true;

                    var messageId = Path.GetFileNameWithoutExtension(filePath);

                    var transaction = new DirectoryBasedTransaction(path);

                    transaction.BeginTransaction(filePath);

                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessFile(transaction, messageId);

                            transaction.Complete();
                        }
                        finally
                        {
                            concurrencyLimiter.Release();
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

        async Task ProcessFile(DirectoryBasedTransaction transaction, string messageId)
        {
            try
            {
                var message = File.ReadAllLines(transaction.FileToProcess);
                var bodyPath = message.First();
                var headers = DeserializeHeaders(message.Skip(1).ToArray());

                string ttbr;

                if (headers.TryGetValue(Headers.TimeToBeReceived, out ttbr))
                {
                    if (DateTimeExtensions.ToUtcDateTime(ttbr) < DateTime.UtcNow)
                    {
                        transaction.Commit();
                        return;
                    }
                }

                using (var bodyStream = new FileStream(bodyPath, FileMode.Open))
                {
                    var context = new ContextBag();
                    context.Set(transaction);

                    var pushContext = new PushContext(new IncomingMessage(messageId, headers, bodyStream), context);
                    await pipeline(pushContext).ConfigureAwait(false);
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
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

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        SemaphoreSlim concurrencyLimiter;

        Task messagePumpTask;

        string path;
        Func<PushContext, Task> pipeline;
        bool purgeOnStartup;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
    }
}