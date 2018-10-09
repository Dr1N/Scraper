using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scraper
{
    enum PageState
    {
        Wait,
        Process,
        Downloading,
        Found,
        NotFound,
        Error
    }

    interface IPage : IComparable
    {
        Uri Uri { get; }

        PageState State { get; set; }

        string Error { get; }

        Task LoadAsync(CancellationToken token);

        bool Search(string search);

        IList<Uri> GetUrls();
    }
}