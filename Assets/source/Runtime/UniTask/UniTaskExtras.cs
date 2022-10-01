using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace JamesFrowen.CSP.UniTaskExtras
{
    public readonly struct CustomYieldAwaitable
    {
        private readonly CustomTiming timing;

        public CustomYieldAwaitable(CustomTiming timing)
        {
            this.timing = timing;
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(timing);
        }

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly CustomTiming timing;

            public Awaiter(CustomTiming timing)
            {
                this.timing = timing;
            }

            public bool IsCompleted => false;

            public void GetResult() { }

            public void OnCompleted(Action continuation)
            {
                CustomTimingHelper.AddContinuation(timing, continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                CustomTimingHelper.AddContinuation(timing, continuation);
            }
        }
    }

    public enum CustomTiming
    {
        FirstNetworkFixedUpdate = 0,
        NetworkFixedUpdate = 1,
        LastNetworkFixedUpdate = 2,
        FirstVisualUpdate = 3,
        VisualUpdate = 4,
        LastVisualUpdate = 5,
    }

    public static class CustomTimingHelper
    {
        private static CustomTimingQueue[] queues;

        public static IPredictionUpdates[] Init()
        {
            if (queues != null)
            {
                // todo should we clear queues? would that cause problems with running unitasks?
                // maybe unitask error?
                Debug.LogWarning($"Replacing existing queues");
            }


            queues = new CustomTimingQueue[6];
            for (var i = 0; i < 6; i++)
            {
                queues[i] = new CustomTimingQueue((CustomTiming)i);
            }
            var updates = new IPredictionUpdates[3];
            updates[0] = new Updates(int.MinValue,
                                     queues[(int)CustomTiming.FirstNetworkFixedUpdate],
                                     queues[(int)CustomTiming.FirstVisualUpdate]);
            updates[1] = new Updates(0,
                                     queues[(int)CustomTiming.NetworkFixedUpdate],
                                     queues[(int)CustomTiming.VisualUpdate]);
            updates[2] = new Updates(int.MaxValue,
                                     queues[(int)CustomTiming.LastNetworkFixedUpdate],
                                     queues[(int)CustomTiming.LastVisualUpdate]);
            return updates;
        }

        internal static void AddContinuation(CustomTiming timing, Action continuation)
        {
            queues[(int)timing].Enqueue(continuation);
        }

        private class Updates : IPredictionUpdates
        {
            private readonly CustomTimingQueue _fixedQueue;
            private readonly CustomTimingQueue _visualQueue;

            public int Order { get; }
            IPredictionTime IPredictionUpdates.PredictionTime { get; set; }

            public Updates(int order, CustomTimingQueue fixedQueue, CustomTimingQueue visualQueue)
            {
                Order = order;
                _fixedQueue = fixedQueue;
                _visualQueue = visualQueue;
            }

            void IPredictionUpdates.InputUpdate() { }
            void IPredictionUpdates.NetworkFixedUpdate() => _fixedQueue.Run();
            void IPredictionUpdates.VisualUpdate() => _visualQueue.Run();
        }
    }

    internal class CustomTimingQueue
    {
        private readonly CustomTiming _timing;
        private readonly Queue<Action> _actionQueue = new Queue<Action>();

#if DEBUG
        private readonly Thread _mainThread;
#endif

        public CustomTimingQueue(CustomTiming timing)
        {
            _timing = timing;
#if DEBUG
            _mainThread = Thread.CurrentThread;
#endif
        }

        public void Enqueue(Action continuation)
        {
#if DEBUG
            if (Thread.CurrentThread != _mainThread)
                Debug.LogError($"CustomTimingQueue is not thread safe, only call on main thread");
#endif
            _actionQueue.Enqueue(continuation);
        }

        // delegate entrypoint.
        public void Run()
        {
            // for debugging, create named stacktrace.
#if DEBUG
            switch (_timing)
            {
                case CustomTiming.NetworkFixedUpdate:
                    NetworkFixedUpdate();
                    break;
                case CustomTiming.VisualUpdate:
                    VisualUpdate();
                    break;
            }
#else
            RunCore();
#endif
        }


        private void NetworkFixedUpdate() => RunCore();
        private void VisualUpdate() => RunCore();

        private void RunCore()
        {
#if DEBUG
            if (Thread.CurrentThread != _mainThread)
                Debug.LogError($"CustomTimingQueue is not thread safe, only call on main thread");
#endif

            while (_actionQueue.Count > 0)
            {
                var action = _actionQueue.Dequeue();
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }
    }
}
