/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using JamesFrowen.PositionSync;
using Mirage;
using Mirage.Logging;
using UnityEngine;

namespace JamesFrowen.PositionSync
{
    /// <summary>
    /// Synchronizes time between server and client via regular messages between server and client.
    /// <para>Can be used for snapshot interpolation</para>
    /// </summary>
    /// <remarks>
    /// This class will speed up or slow down the client time scale, depending if it is ahead or behind the lastest server time.
    /// <para>
    /// Every update we add DeltaTime * TimeScale to client time.
    /// </para>
    /// <para>
    /// On the server, when an update is performed, the server will send a message back with its time.<br/>
    /// When the client receives this message, it calculates the difference between server time and its own local time.<br/>
    /// This difference is stored in a moving average, which is smoothed out.
    /// </para>
    /// <para>
    /// If the calculated difference is greater or less than a threshold then we adjust the client time scale by speeding up or slowing down.<br/>
    /// If the calculated difference is between our defined threshold times, client time scale is set back to normal.
    /// </para>
    /// <para>
    /// This client time can then be used to snapshot interpolation using <c>InterpolationTime = ClientTime - Offset</c>
    /// </para>
    /// <para>
    /// Some other implementations include the offset in the time scale calculations itself,
    /// So that Client time is always (2) intervals behind the received server time. <br/>
    /// Moving that offset to outside this class should still give the same results.
    /// We are just trying to make the difference equal to 0 instead of negative offset.
    /// Then subtracking offset from the ClientTime before we do the interpolation
    /// </para>
    /// </remarks>
    public class InterpolationTime
    {
        private static readonly ILogger logger = LogFactory.GetLogger<InterpolationTime>();
        private bool intialized;

        /// <summary>
        /// The time value that the client uses to interpolate
        /// </summary>
        private float _clientTime;

        /// <summary>
        /// The client will multiply deltaTime by this scale time value each frame
        /// </summary>
        private float clientScaleTime;
        private readonly ExponentialMovingAverage diffAvg;

        /// <summary>
        /// How much above the goalOffset difference are we allowed to go before changing the timescale
        /// </summary>
        private readonly float positiveThreshold;

        /// <summary>
        /// How much below the goalOffset difference are we allowed to go before changing the timescale
        /// </summary>
        private readonly float negativeThreshold;
        private readonly float fastScale = 1.01f;
        private const float normalScale = 1f;
        private readonly float slowScale = 0.99f;

        /// <summary>
        /// Is the difference between previous time and new time too far apart?
        /// If so, reset the client time.
        /// </summary>
        private readonly float _skipAheadThreshold;
        private float _clientDelay;

        // Used for debug purposes. Move along...
        private float _latestServerTime;

        /// <summary>
        /// Timer that follows server time
        /// </summary>
        public float ClientTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _clientTime;
        }
        /// <summary>
        /// Returns the last time received by the server
        /// </summary>
        public float LatestServerTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _latestServerTime;
        }

        [System.Obsolete("Use Time instead")]
        public float InterpolationTimeField
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time;
        }

        /// <summary>
        /// Current time to use for interpolation 
        /// </summary>
        public float Time
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _clientTime - _clientDelay;
        }

        public float ClientDelay
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _clientDelay;
            set => _clientDelay = value;
        }

        // Used for debug purposes. Move along...
        public float DebugScale => clientScaleTime;

        /// <param name="diffThreshold">How far off client time can be before changing its speed. A good recommended value is half of SyncInterval.</param>
        /// <param name="movingAverageCount">How many ticks are used for averaging purposes, you may need to increase or decrease with frame rate.</param>
        public InterpolationTime(float tickInterval, float diffThreshold = 0.5f, float timeScale = 0.01f, float skipThreshold = 2.5f, float tickDelay = 2, int movingAverageCount = 30)
        {
            positiveThreshold = tickInterval * diffThreshold;
            negativeThreshold = -positiveThreshold;
            _skipAheadThreshold = tickInterval * skipThreshold;

            fastScale = normalScale + timeScale;
            slowScale = normalScale - timeScale;

            _clientDelay = tickInterval * tickDelay;

            diffAvg = new ExponentialMovingAverage(movingAverageCount);

            // Client should always start at normal time scale.
            clientScaleTime = normalScale;
        }

        /// <summary>
        /// Updates the client time.
        /// </summary>
        /// <param name="deltaTime"></param>
        public void OnUpdate(float deltaTime)
        {
            _clientTime += deltaTime * clientScaleTime;
        }

        /// <summary>
        /// Updates <see cref="clientScaleTime"/> to keep <see cref="ClientTime"/> in line with <see cref="LatestServerTime"/>
        /// </summary>
        /// <param name="serverTime"></param>
        public void OnMessage(float serverTime)
        {
            // only check this if we are intialized
            if (intialized)
                logger.Assert(serverTime > _latestServerTime, $"Received message out of order. Server Time: {serverTime} vs New Time: {_latestServerTime}");

            _latestServerTime = serverTime;

            // If this is the first message, set the client time to the server difference.
            // If we're too far behind, then we should reset things too.

            // todo check this is correct
            if (!intialized)
            {
                InitNew(serverTime);
                return;
            }

            // Calculate the difference.
            var diff = serverTime - _clientTime;

            // Are we falling behind?
            if (serverTime - _clientTime > _skipAheadThreshold)
            {
                logger.LogWarning($"Client fell behind, skipping ahead. Server Time: {serverTime:0.00}, Difference: {diff:0.00}");
                InitNew(serverTime);
                return;
            }

            diffAvg.Add(diff);

            // Adjust the client time scale with the appropriate value.
            AdjustClientTimeScale((float)diffAvg.Value);

            // todo add trace level
            if (logger.LogEnabled()) logger.Log($"st: {serverTime:0.00}, ct: {_clientTime:0.00}, diff: {diff * 1000:0.0}, wanted: {diffAvg.Value * 1000:0.0}, scale: {clientScaleTime}");
        }

        /// <summary>
        /// Call this when start new client to reset timer
        /// </summary>
        public void Reset()
        {
            // mark this so first server method will call InitNew
            intialized = false;
            _latestServerTime = 0;
        }

        /// <summary>
        /// Initializes and resets the system.
        /// </summary>
        private void InitNew(float serverTime)
        {
            _clientTime = serverTime;
            clientScaleTime = normalScale;
            diffAvg.Reset();
            intialized = true;
        }

        /// <summary>
        /// Adjusts the client time scale based on the provided difference.
        /// </summary>
        private void AdjustClientTimeScale(float diff)
        {
            // Difference is calculated between server and client.
            // So if that difference is positive, we can run the client faster to catch up.
            // However, if it's negative, we need to slow the client down otherwise we run out of snapshots.            
            // Ideally, we want the difference vs the goal to be as close to 0 as possible.

            // Server's ahead of us, we need to speed up.
            if (diff > positiveThreshold)
                clientScaleTime = fastScale;
            // Server is falling behind us, we need to slow down.
            else if (diff < negativeThreshold)
                clientScaleTime = slowScale;
            // Server and client are on par ("close enough"). Run at normal speed.
            else
                clientScaleTime = normalScale;
        }
    }
    public interface ISnapshotInterpolator<T>
    {
        T Lerp(T a, T b, float alpha);
    }
    public class SnapshotBuffer<T>
    {
        private static readonly ILogger logger = LogFactory.GetLogger("JamesFrowen.PositionSync.SnapshotBuffer");

        internal struct Snapshot
        {
            /// <summary>
            /// Server Time
            /// </summary>
            public readonly double time;
            public readonly T state;

            public Snapshot(T state, double time) : this()
            {
                this.state = state;
                this.time = time;
            }
        }

        private readonly List<Snapshot> buffer = new List<Snapshot>();
        private readonly ISnapshotInterpolator<T> interpolator;

        internal IReadOnlyList<Snapshot> DebugBuffer => buffer;

        public SnapshotBuffer(ISnapshotInterpolator<T> interpolator)
        {
            this.interpolator = interpolator;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count == 0;
        }
        public int SnapshotCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count;
        }

        private Snapshot First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[0];
        }
        private Snapshot Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[buffer.Count - 1];
        }

        public void AddSnapShot(T state, double serverTime)
        {
            if (!IsEmpty && serverTime < Last.time)
                throw new ArgumentException($"Can not add snapshot to buffer. This would cause the buffer to be out of order. Last t={Last.time:0.000}, new t={serverTime:0.000}");

            buffer.Add(new Snapshot(state, serverTime));
        }

        /// <summary>
        /// Gets a snapshot to use for interpolation purposes.
        /// <para>This method should not be called when there are no snapshots in the buffer.</para>
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public T GetLinearInterpolation(double now)
        {
            if (buffer.Count == 0)
                throw new InvalidOperationException("No snapshots in buffer.");

            // first snapshot
            if (buffer.Count == 1)
            {
                if (logger.LogEnabled())
                    logger.Log("First snapshot");

                return First.state;
            }

            // if first snapshot is after now, there is no "from", so return same as first snapshot
            if (First.time > now)
            {
                if (logger.LogEnabled())
                    logger.Log($"No snapshots for t = {now:0.000}, using earliest t = {buffer[0].time:0.000}");

                return First.state;
            }

            // if last snapshot is before now, there is no "to", so return last snapshot
            // this can happen if server hasn't sent new data
            // there could be no new data from either lag or because object hasn't moved
            if (Last.time < now)
            {
                if (logger.LogEnabled())
                    logger.Log($"No snapshots for t = {now:0.000}, using first t = {buffer[0].time:0.000}, last t = {Last.time:0.000}");
                return Last.state;
            }

            // edge cases are returned about, if code gets to this for loop then a valid from/to should exist...
            for (var i = 0; i < buffer.Count - 1; i++)
            {
                var from = buffer[i];
                var to = buffer[i + 1];
                var fromTime = buffer[i].time;
                var toTime = buffer[i + 1].time;

                // if between times, then use from/to
                if (fromTime <= now && now <= toTime)
                {
                    var alpha = (float)Clamp01((now - fromTime) / (toTime - fromTime));
                    // todo add trace log
                    if (logger.LogEnabled()) logger.Log($"alpha:{alpha:0.000}");

                    return interpolator.Lerp(from.state, to.state, alpha);
                }
            }

            // If not, then this is our final stand.
            logger.LogError("Should never be here! Code should have return from if or for loop above.");
            return Last.state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            else return v;
        }

        /// <summary>
        /// Removes snapshots older than <paramref name="oldTime"/>.
        /// </summary>
        /// <param name="oldTime"></param>
        public void RemoveOldSnapshots(double oldTime)
        {
            // Loop from newest to oldest...
            for (var i = buffer.Count - 1; i >= 0; i--)
            {
                // Is this one older than oldTime? If so, evict it.
                if (buffer[i].time < oldTime)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Clears the snapshots buffer.
        /// </summary>
        public void ClearBuffer()
        {
            buffer.Clear();
        }

        // Used for debug purposes. Move along...
        public string ToDebugString(double now)
        {
            if (buffer.Count == 0) { return "Buffer Empty"; }

            var builder = new StringBuilder();
            builder.AppendLine($"count:{buffer.Count}, minTime:{buffer[0].time:0.000}, maxTime:{buffer[buffer.Count - 1].time:0.000}");
            for (var i = 0; i < buffer.Count; i++)
            {
                if (i != 0)
                {
                    var fromTime = buffer[i - 1].time;
                    var toTime = buffer[i].time;
                    // if between times, then use from/to
                    if (fromTime <= now && now <= toTime)
                    {
                        builder.AppendLine($"                    <-----");
                    }
                }

                builder.AppendLine($"  {i}: {buffer[i].time:0.000}");
            }
            return builder.ToString();
        }
    }
}
namespace JamesFrowen.CSP
{
    public struct InterpolationTick
    {
        public readonly int Tick1;
        public readonly int Tick2;
        public readonly float Delta;

        public InterpolationTick(int tick1) : this()
        {
            Tick1 = tick1;
        }
        public InterpolationTick(int tick1, int tick2, float delta)
        {
            Tick1 = tick1;
            Tick2 = tick2;
            Delta = delta;
        }

        public static implicit operator InterpolationTick(int t) => new InterpolationTick(t);

        public class Interpolator : ISnapshotInterpolator<InterpolationTick>
        {
            InterpolationTick ISnapshotInterpolator<InterpolationTick>.Lerp(InterpolationTick a, InterpolationTick b, float alpha)
            {
                return new InterpolationTick(a.Tick1, a.Tick2, alpha);
            }
        }
    }

    public class ClientInterpolation : IPredictionUpdates
    {
        public const int USE_LOCAL_STATE = -1;
        private readonly SnapshotBuffer<InterpolationTick> _buffer;
        private readonly InterpolationTime _timeSync;
        private IPredictionTime _time;

        public InterpolationTick? Interpolation;
        public bool TryGetInterpolation(out InterpolationTick interpolation)
        {
            interpolation = Interpolation.GetValueOrDefault();
            return Interpolation.HasValue;
        }

        int IPredictionUpdates.Order => int.MinValue;
        IPredictionTime IPredictionUpdates.PredictionTime
        {
            get => _time;
            set
            {
                Debug.Assert(_time == value);
                _time = value;
            }
        }

        public ClientInterpolation(IPredictionTime time)
        {
            _time = time;
            _buffer = new SnapshotBuffer<InterpolationTick>(new InterpolationTick.Interpolator());
            _timeSync = new InterpolationTime(time.FixedDeltaTime);
        }

        public void OnMessage(int tick)
        {
            var serverTime = tick * _time.FixedDeltaTime;
            if (_buffer.IsEmpty)
            {
                _buffer.AddSnapShot(USE_LOCAL_STATE, serverTime - _time.FixedDeltaTime);
            }

            _buffer.AddSnapShot(tick, serverTime);
        }

        public void VisualUpdate()
        {
            _timeSync.OnUpdate((float)_time.DeltaTime);


            if (_buffer.IsEmpty)
            {
                Interpolation = null;
                return;
            }


            var snapshotTime = _timeSync.Time;
            var state = _buffer.GetLinearInterpolation(snapshotTime);

            Interpolation = state;

            // remove snapshots older than 2times sync interval, they will never be used by Interpolation
            var removeTime = snapshotTime - (_timeSync.ClientDelay * 1.5f);
            _buffer.RemoveOldSnapshots(removeTime);
        }

        void IPredictionUpdates.InputUpdate() { }
        void IPredictionUpdates.NetworkFixedUpdate() { }
    }
}
