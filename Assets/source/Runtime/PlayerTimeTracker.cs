/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;

namespace JamesFrowen.CSP
{
    internal class PlayerTimeTracker : ITickNotifyTracker
    {
        // todo use this to collect metrics about client (eg ping, rtt, etc)
        public double LastReceivedClientTime;
        public int? lastReceivedInput = null;

        public bool ReadyForWorldState;

        public int? LastAckedTick { get; private set; }

        public void SetLastAcked(int tick)
        {
            if (LastAckedTick.HasValue)
                LastAckedTick = Math.Max(LastAckedTick.Value, tick);
            else
                LastAckedTick = tick;
        }
        public void ClearLastAcked()
        {
            LastAckedTick = null;
        }
    }
}
