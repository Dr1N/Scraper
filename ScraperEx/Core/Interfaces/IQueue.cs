using System;
using System.Collections.Generic;

namespace Scraper
{
    interface IQueue<T> : IEnumerable<T>, IDisposable where T: class
    {
        void Enqueue(T item);

        T Dequeue();
    }
}
