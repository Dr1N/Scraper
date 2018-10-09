using System;

namespace Scraper
{
    class StateChangedEventArgs : EventArgs
    {
        public FinderState State { get; private set; }

        public StateChangedEventArgs(FinderState state)
        {
            State = state;
        }
    }
}