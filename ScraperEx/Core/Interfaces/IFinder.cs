using System;

namespace Scraper
{
    enum FinderState
    {
        Ready,
        Starting,
        Work,
        Paused,
        Stopping,
        Stopped,
    }

    interface IFinder
    {
        #region Events

        event EventHandler<StateChangedEventArgs> StateChanged;
        event EventHandler<double> ProgressChanged;

        #endregion

        #region Properties

        IQueue<IPage> Pages { get; }

        int MaxThreadsCount { get; set; }

        int MaxPagesCount { get; set; }

        FinderState State { get; }

        #endregion

        #region Public

        void Start(Uri startUri, string search);

        void Stop();

        void Pause();

        void Resume();

        #endregion
    }
}
