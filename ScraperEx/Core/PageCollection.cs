using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Data;

namespace Scraper
{
    class PageCollection : ObservableCollection<IPage>, IQueue<IPage>
    {
        #region Private Fields

        private readonly object observableCollectionLock;

        private readonly object collectionLock;

        private readonly SemaphoreSlim queueSemaphore;

        private readonly CancellationToken cancellationToken;

        //For autostop (сомнительная инициатива =))
        private int sleepingThreads;
        public int Sleeping => sleepingThreads;

        #endregion

        #region Properties

        /// <summary>
        /// Maximum number of concurrent threads
        /// </summary>
        private int Slots { get; set; }

        /// <summary>
        /// Maximum number of pages
        /// </summary>
        private int Capacity { get; set; }

        #endregion

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="slots">Maximum number of concurrent threads</param>
        /// <param name="capacity">Maximum number of pages</param>
        /// <param name="token">Cancellation Token</param>
        /// <exception cref="ArgumentNullException"
        /// <exception cref="ArithmeticException"
        public PageCollection(int slots, int capacity, CancellationToken token)
        {
            if (slots > capacity)
            {
                throw new ArgumentException($"Number of {nameof(slots)} must be less than {nameof(capacity)}");
            }
            Slots = slots;
            Capacity = capacity;
            cancellationToken = token;
            observableCollectionLock = new object();
            collectionLock = new object();
            queueSemaphore = new SemaphoreSlim(0, slots);
            
            //Syncronize collection (.Net 4.5+). Needed for safe INotifyCollectionChanged implementation
            BindingOperations.EnableCollectionSynchronization(this, observableCollectionLock);
        }

        #region IQueue Implementation

        /// <summary>
        /// Аdd item to collection and 'wake up' the thread if needed
        /// </summary>
        /// <param name="item">IPage item</param>
        public void Enqueue(IPage item)
        {
            CheckDisposed();
            try
            {
                //Add item 
                lock (collectionLock)
                {
                    //Check collection capacity
                    if (this.Count >= Capacity)
                    {
                        return;
                    }
                    //Do not add already existing
                    if (!this.Any(w => w.CompareTo(item) == 0))
                    {
                        this.Add(item);
                        //Give slot for waiting thread
                        GiveSlot();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PageCollection.Enqueue Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get item from the collection
        /// If there is no item to put the thread to 'sleep'
        /// </summary>
        /// <returns>Item or null</returns>
        public IPage Dequeue()
        {
            CheckDisposed();
            IPage result = null;
            lock (collectionLock)
            {
                try
                {
                    //Get first waiting page
                    result = this.Where(w => w.State == PageState.Wait).FirstOrDefault();
                    //Set 'Process' state web page, that would not get twice
                    if (result != null)
                    {
                        result.State = PageState.Process;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PageCollection.Dequeue Error: {ex.Message}");
                    result = null;
                }
            }
            if (result == null)
            {
                try
                {
                    //Wait signal
                    WaitPage();
                    Interlocked.Decrement(ref sleepingThreads);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PageCollection.Dequeue Error: {ex.Message}");
                }
            }
           
            return result;
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
                    queueSemaphore?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private void GiveSlot()
        {
            if (queueSemaphore.CurrentCount < Slots)
            {
                queueSemaphore.Release();
            }
        }

        private void WaitPage()
        {
            Interlocked.Increment(ref sleepingThreads);
            queueSemaphore.Wait(cancellationToken);
        }

        private void CheckDisposed()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException("PageCollection");
            }
        }

        #endregion
    }
}