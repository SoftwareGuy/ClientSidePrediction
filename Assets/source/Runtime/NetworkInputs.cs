/*******************************************************
 * Copyright (C) 2021 James Frowen <JamesFrowenDev@gmail.com>
 * 
 * This file is part of JamesFrowen ClientSidePrediction
 * 
 * The code below can not be copied and/or distributed without the express
 * permission of James Frowen
 *******************************************************/

namespace JamesFrowen.CSP
{
    public struct NetworkInputs<TInput>
    {
        public TInput Current;
        public TInput Previous;

        public NetworkInputs(TInput current, TInput previous)
        {
            Current = current;
            Previous = previous;
        }
    }
}
