using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Scraper
{
    class Finder : IFinder, IDisposable
    {
        #region IFinder Properties and Events

        public event EventHandler<StateChangedEventArgs> StateChanged = delegate { };
        public event EventHandler<double> ProgressChanged = delegate { };

        private Uri startUri;
        public Uri StartUri
        {
            get => startUri;
            private set
            {
                startUri = value ?? throw new ArgumentNullException(nameof(startUri));
            }
        }

        private int maxPagesCount;
        public int MaxPagesCount
        {
            get => maxPagesCount;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException($"{nameof(MaxPagesCount)} must be greater than zero");
                }
                maxPagesCount = value;
            }
        }

        private string searchText;
        public string SearchText
        {
            get => searchText;
            private set
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(searchText));
                }
                searchText = value;
            }
        }

        private int maxThreadCount;
        public int MaxThreadsCount
        {
            get => maxThreadCount;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException($"{nameof(MaxPagesCount)} must be greater than zero");
                }
                maxThreadCount = value;
            }
        }

        #endregion

        #region Synchronization

        private readonly EventWaitHandle pauseEvent;

        private CancellationTokenSource cancellationSource;

        #endregion

        #region Properties and Fields

        public IQueue<IPage> Pages { get; private set; }

        public FinderState State
        {
            get
            {
                lock (stateLocker)
                {
                    return state;
                }
            }
            private set
            {
                lock (stateLocker)
                {
                    state = value;
                    StateChanged.Invoke(this, new StateChangedEventArgs(state));
                }
            }
        }

        private readonly object stateLocker = new object();

        private int processedPages;

        private FinderState state;

        private IList<Task> Workers { get; set; }

        private readonly System.Timers.Timer autoStopTimer;

        #endregion

        #region Life Cycle

        public Finder()
        {
            pauseEvent = new ManualResetEvent(true);
            autoStopTimer = new System.Timers.Timer(250);
            autoStopTimer.Elapsed += AutoStop;
            State = FinderState.Ready;
        }

        #endregion

        #region IDisposable Implementation

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    cancellationSource?.Dispose();
                    pauseEvent?.Dispose();
                    Pages?.Dispose();
                    autoStopTimer?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region IFinder Implementation

        /// <summary>
        /// Begin search
        /// </summary>
        /// <param name="uri">Initial page uri</param>
        /// <param name="search">Searched text</param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ObjectDisposedException"/>
        /// <exception cref="Exception"/>
        public void Start(Uri uri, string search)
        {
            CheckDisposed();
            if (State == FinderState.Starting)
            {
                return;
            }
            State = FinderState.Starting;
            if (MaxThreadsCount > MaxPagesCount)
            {
                State = FinderState.Stopped;
                throw new ArgumentException($"Number of MaxThreadsCount must be less than MaxPagesCount");
            }
            StartUri = uri;
            SearchText = search;
            processedPages = 0;
            ProgressChanged.Invoke(this, 0);

            try
            {
                //Page Collection
                cancellationSource = new CancellationTokenSource();
                Pages = new PageCollection(MaxThreadsCount, MaxPagesCount, cancellationSource.Token);
                Pages.Enqueue(new WebPage(StartUri));

                //Create worker Tasks
                Workers = new List<Task>();
                for (int i = 0; i < MaxThreadsCount; i++)
                {
                    Workers.Add(new Task(ProcessPage, cancellationSource.Token));
                }

                //Start workers
                foreach (var item in Workers)
                {
                    item.Start();
                }
                autoStopTimer.Start();
                State = FinderState.Work;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Finder.Start Error: {ex.Message}");
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Stop search
        /// </summary>
        /// <exception cref="ObjectDisposedException"/>
        public void Stop()
        {
            CheckDisposed();
            if (State == FinderState.Stopping)
            {
                return;
            }
            State = FinderState.Stopping;
            if (autoStopTimer.Enabled)
            {
                autoStopTimer.Stop();
            }
            CancelWorkers();
            if (Workers != null && Workers.Count > 0)
            {
                Task.WaitAll(Workers.ToArray());
            }
            Workers?.Clear();
            State = FinderState.Stopped;
        }

        /// <summary>
        /// Pause search
        /// </summary>
        /// <exception cref="ObjectDisposedException"/>
        public void Pause()
        {
            CheckDisposed();
            if (State == FinderState.Work)
            {
                pauseEvent.Reset();
                State = FinderState.Paused;
            }
        }

        /// <summary>
        /// Resume search after pause
        /// </summary>
        /// <exception cref="ObjectDisposedException"/>
        public void Resume()
        {
            CheckDisposed();
            if (State == FinderState.Paused)
            {
                pauseEvent.Set();
                State = FinderState.Work;
            }
        }

        #endregion

        #region Private Methods

        //Task action
        private void ProcessPage()
        {
            Debug.WriteLine($"_____Start: {Thread.CurrentThread.ManagedThreadId}");
            while (!cancellationSource.IsCancellationRequested)
            {
                try
                {
                    pauseEvent.WaitOne();
                    //If stopped during pause
                    cancellationSource.Token.ThrowIfCancellationRequested();
                    IPage webPage = Pages.Dequeue();
                    if (webPage != null)
                    {
                        webPage.LoadAsync(cancellationSource.Token).Wait(cancellationSource.Token);
                        if (webPage.State != PageState.Error)
                        {
                            Task.Delay(1000).Wait();
                            webPage.Search(SearchText);
                            Enqueue(webPage.GetUrls().Select(u => new WebPage(u)));
                        }
                        ReportProgress();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Finder.ProcessPage Error: {ex.Message}");
                }
            }
            Debug.WriteLine($"_____End {Thread.CurrentThread.ManagedThreadId}");
        }
        
        //Auto Stop
        private void AutoStop(object sender, ElapsedEventArgs e)
        {
            if ((Pages as PageCollection)?.Sleeping == MaxThreadsCount)
            {
                (sender as System.Timers.Timer)?.Stop();
                RunAsync(Stop);
            }
        }

        private void Enqueue(IPage webPage)
        {
            if (webPage != null)
            {
                Pages.Enqueue(webPage);
            }
        }

        private void Enqueue(IEnumerable<IPage> webPages)
        {
            if (webPages != null)
            {
                try
                {
                    foreach (var page in webPages)
                    {
                        Enqueue(page);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Finder.Enqueue Error: {ex.Message}");
                }
            }            
        }
        
        private void CancelWorkers()
        {
            try
            {
                //Cancel tasks
                if (cancellationSource != null && !cancellationSource.IsCancellationRequested)
                {
                    cancellationSource.Cancel();
                }
                //Wake up paused tasks
                pauseEvent.Set();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Finder.CancelWorkers Error: {ex.Message}");
            }
        }

        private void ReportProgress()
        {
            Interlocked.Increment(ref processedPages);
            var percent = (double)processedPages * 100 / MaxPagesCount;
            RunAsync(() => ProgressChanged.Invoke(this, percent));
        }

        private void RunAsync(Action action)
        {
            if (action == null)
            {
                return;
            }
            //Do not block the thread to avoid deadlock
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch { }
            });
        }

        private void CheckDisposed()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException("Finder");
            }
        }

        #endregion
    }
}