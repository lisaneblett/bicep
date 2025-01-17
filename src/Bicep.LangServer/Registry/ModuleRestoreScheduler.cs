// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Registry;
using Bicep.Core.Syntax;
using Bicep.LanguageServer.CompilationManager;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bicep.LanguageServer.Registry
{
    public sealed class ModuleRestoreScheduler : IModuleRestoreScheduler, IAsyncDisposable
    {
        private record QueueItem(ICompilationManager CompilationManager, DocumentUri Uri, ImmutableArray<ModuleDeclarationSyntax> References);

        private record CompletionNotification(ICompilationManager CompilationManager, DocumentUri Uri);

        private readonly IModuleDispatcher moduleDispatcher;

        private readonly Queue<QueueItem> queue = new();

        private readonly CancellationTokenSource cancellationTokenSource = new();

        // block on initial wait until signaled
        private readonly ManualResetEventSlim manualResetEvent = new(false);

        private bool disposed = false;
        private Task? consumerTask;

        public ModuleRestoreScheduler(IModuleDispatcher moduleDispatcher)
        {
            this.moduleDispatcher = moduleDispatcher;
        }

        /// <summary>
        /// Requests that the specified modules be restored to the local file system.
        /// Does not wait for the operation to complete and returns immediately.
        /// </summary>
        /// <param name="references">The module references</param>
        public void RequestModuleRestore(ICompilationManager compilationManager, DocumentUri documentUri, IEnumerable<ModuleDeclarationSyntax> references)
        {
            this.CheckDisposed();
            var item = new QueueItem(compilationManager, documentUri, references.ToImmutableArray());
            lock (this.queue)
            {
                this.queue.Enqueue(item);

                // notify consumer about new items
                this.manualResetEvent.Set();
            }
        }

        public void Start()
        {
            this.CheckDisposed();
            this.consumerTask = Task.Factory.StartNew(this.ProcessQueueItems, this.cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async ValueTask DisposeAsync()
        {
            // this is a sealed class - no need for full IDisposable implementation
            if (!this.disposed)
            {
                this.disposed = true;
                if (this.consumerTask is not null)
                {
                    this.cancellationTokenSource.Cancel();

                    try
                    {
                        await this.consumerTask;
                    }
                    catch
                    {
                        // the task never completes and can only be canceled
                        // which is signaled to us by an exception
                    }

                    this.consumerTask = null;
                }
            }
        }

        private void ProcessQueueItems()
        {
            var token = this.cancellationTokenSource.Token;
            while (true)
            {
                this.manualResetEvent.Wait(token);

                var notifications = new HashSet<CompletionNotification>();
                var references = new List<ModuleDeclarationSyntax>();
                lock (this.queue)
                {
                    this.UnsafeCollectModuleReferences(notifications, references);
                    Debug.Assert(this.queue.Count == 0, "this.queue.Count == 0");

                    // queue has been consumed - next iteration should block until more items have been added
                    this.manualResetEvent.Reset();
                }

                // this blocks until restore is completed
                // the dispatcher stores the results internally and manages their lifecycle
                token.ThrowIfCancellationRequested();
                if(!this.moduleDispatcher.RestoreModules(references))
                {
                    // nothing needed to be restored
                    // no need to notify about completion
                    continue;
                }

                // notify compilation manager that restore is completed
                // to recompile the affected modules
                token.ThrowIfCancellationRequested();
                foreach (var notification in notifications)
                {
                    notification.CompilationManager.RefreshCompilation(notification.Uri);
                }
            }
        }

        private void UnsafeCollectModuleReferences(HashSet<CompletionNotification> notifications, List<ModuleDeclarationSyntax> references)
        {
            while (this.queue.TryDequeue(out var item))
            {
                // the record implementation combined with the hashset will dedupe compilation manager/Uri combinations
                notifications.Add(new(item.CompilationManager, item.Uri));
                references.AddRange(item.References);
            }
        }

        private void CheckDisposed()
        {
            if(this.disposed)
            {
                throw new ObjectDisposedException($"The {nameof(ModuleRestoreScheduler)} has already been disposed.", innerException: null);
            }
        }
    }
}
