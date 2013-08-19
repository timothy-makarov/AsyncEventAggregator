using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncEventAggregator
{
    public sealed class AsyncEventHub
    {
        private const string EventTypeNotFoundExceptionMessage = @"Event type not found!";
        private const string SubscribersNotFoundExceptionMessage = @"Subscribers not found!";
        private const string FailedToAddSubscribersExceptionMessage = @"Failed to add subscribers!";
        private const string FailedToGetEventHandlerTaskFactoriesExceptionMessage = @"Failed to get event handler task factories!";
        private const string FailedToAddEventHandlerTaskFactoriesExceptionMessage = @"Failed to add event handler task factories!";
        private const string FailedToGetSubscribersExceptionMessage = @"Failed to get subscribers!";
        private const string FailedToRemoveEventHandlerTaskFactories = @"Failed to remove event handler task factories!";

        private readonly TaskFactory _factory;

        /// <summary>
        ///     Dictionary(EventType, Dictionary(Sender, EventHandlerTaskFactories))
        /// </summary>
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, ConcurrentBag<object>>> _hub;

        public AsyncEventHub()
        {
            _factory = Task.Factory;

            _hub = new ConcurrentDictionary<Type, ConcurrentDictionary<object, ConcurrentBag<object>>>();
        }

        public Task<Task[]> Publish<TEvent>(object sender, Task<TEvent> eventDataTask)
        {
            var taskCompletionSource = new TaskCompletionSource<Task[]>();

            _factory.StartNew(
                () =>
                    {
                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {
                            ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;

                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers.Count > 0)
                                {
                                    _factory.ContinueWhenAll(
                                        new ConcurrentBag<Task>(
                                            new ConcurrentBag<object>(subscribers.Keys)
                                                .Where(p => p != sender && subscribers.ContainsKey(p))
                                                .Select(p =>
                                                    {
                                                        ConcurrentBag<object> eventHandlerTaskFactories;

                                                        bool isFailed = !subscribers.TryGetValue(p, out eventHandlerTaskFactories);

                                                        return new
                                                            {
                                                                IsFailed = isFailed,
                                                                EventHandlerTaskFactories = eventHandlerTaskFactories
                                                            };
                                                    })
                                                .SelectMany(
                                                    p =>
                                                        {
                                                            if (p.IsFailed)
                                                            {
                                                                var innerTaskCompletionSource = new TaskCompletionSource<Task>();
                                                                innerTaskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                                                return new ConcurrentBag<Task>(new[] {innerTaskCompletionSource.Task});
                                                            }

                                                            return new ConcurrentBag<Task>(
                                                                p.EventHandlerTaskFactories
                                                                 .Select(q =>
                                                                 {
                                                                     try
                                                                     {
                                                                         return ((Func<Task<TEvent>, Task>)q)(eventDataTask);
                                                                     }
                                                                     catch (Exception ex)
                                                                     {
                                                                         return _factory.FromException<object>(ex);
                                                                     }
                                                                 }));
                                                        }))
                                            .ToArray(),
                                        taskCompletionSource.SetResult);
                                }
                                else
                                {
                                    taskCompletionSource.SetException(new Exception(SubscribersNotFoundExceptionMessage));
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(SubscribersNotFoundExceptionMessage));
                            }
                        }
                        else
                        {
                            taskCompletionSource.SetException(new Exception(EventTypeNotFoundExceptionMessage));
                        }
                    });

            return taskCompletionSource.Task;
        }

        public Task Subscribe<TEvent>(object sender, Func<Task<TEvent>, Task> eventHandlerTaskFactory)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _factory.StartNew(
                () =>
                    {
                        ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;
                        ConcurrentBag<object> eventHandlerTaskFactories;

                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {
                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers.ContainsKey(sender))
                                {
                                    if (subscribers.TryGetValue(sender, out eventHandlerTaskFactories))
                                    {
                                        eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                        taskCompletionSource.SetResult(null);
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                                else
                                {
                                    eventHandlerTaskFactories = new ConcurrentBag<object>();

                                    if (subscribers.TryAdd(sender, eventHandlerTaskFactories))
                                    {
                                        eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                        taskCompletionSource.SetResult(null);
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToAddEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                            }
                        }
                        else
                        {
                            subscribers = new ConcurrentDictionary<object, ConcurrentBag<object>>();

                            if (_hub.TryAdd(eventType, subscribers))
                            {
                                eventHandlerTaskFactories = new ConcurrentBag<object>();

                                if (subscribers.TryAdd(sender, eventHandlerTaskFactories))
                                {
                                    eventHandlerTaskFactories.Add(eventHandlerTaskFactory);
                                    taskCompletionSource.SetResult(null);
                                }
                                else
                                {
                                    taskCompletionSource.SetException(new Exception(FailedToAddEventHandlerTaskFactoriesExceptionMessage));
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToAddSubscribersExceptionMessage));
                            }
                        }
                    });

            return taskCompletionSource.Task;
        }

        public Task Unsubscribe<TEvent>(object sender)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _factory.StartNew(
                () =>
                    {
                        Type eventType = typeof (TEvent);

                        if (_hub.ContainsKey(eventType))
                        {
                            ConcurrentDictionary<object, ConcurrentBag<object>> subscribers;

                            if (_hub.TryGetValue(eventType, out subscribers))
                            {
                                if (subscribers == null)
                                {
                                    taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                                }
                                else
                                {
                                    if (subscribers.ContainsKey(sender))
                                    {
                                        ConcurrentBag<object> eventHandlerTaskFactories;

                                        if (subscribers.TryRemove(sender, out eventHandlerTaskFactories))
                                        {
                                            taskCompletionSource.SetResult(null);
                                        }
                                        else
                                        {
                                            taskCompletionSource.SetException(new Exception(FailedToRemoveEventHandlerTaskFactories));
                                        }
                                    }
                                    else
                                    {
                                        taskCompletionSource.SetException(new Exception(FailedToGetEventHandlerTaskFactoriesExceptionMessage));
                                    }
                                }
                            }
                            else
                            {
                                taskCompletionSource.SetException(new Exception(FailedToGetSubscribersExceptionMessage));
                            }
                        }
                        else
                        {
                            taskCompletionSource.SetException(new Exception(EventTypeNotFoundExceptionMessage));
                        }
                    });

            return taskCompletionSource.Task;
        }
    }
}