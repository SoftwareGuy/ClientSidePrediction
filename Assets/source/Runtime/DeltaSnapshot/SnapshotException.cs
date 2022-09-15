/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

using System;

namespace JamesFrowen.DeltaSnapshot
{
    [Serializable]
    public class SnapshotException : Exception
    {
        public SnapshotException() { }
        public SnapshotException(string message) : base(message) { }
        public SnapshotException(string message, Exception inner) : base(message, inner) { }
        protected SnapshotException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
