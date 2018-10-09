using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Scraper
{
    class WebPage : IPage, INotifyPropertyChanged
    {
        #region Constants

        private readonly string LinkTag = "a";
        private readonly string UrlAttr = "href";
        private readonly string HttpPrefix = "http://";
        private readonly string HttpsPrefix = "https://";

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged(string name)
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region Fields and Properties

        private Uri uri;
        public Uri Uri
        {
            get
            {
                return uri;
            }
            private set
            {
                uri = value;
                OnPropertyChanged("Uri");
            }
        }

        private PageState state;
        public PageState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                OnPropertyChanged("State");
            }
        }

        private string error;
        public string Error
        {
            get
            {
                return error;
            }
            private set
            {
                error = value;
                OnPropertyChanged("Error");
            }
        }

        private string Content { get; set; }

        #endregion

        public WebPage(Uri uri)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Error = null;
            State = PageState.Wait;
        }

        #region IPage Implementation

        /// <summary>
        /// Load page from internet.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        public async Task LoadAsync(CancellationToken token)
        {
            State = PageState.Downloading;
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    using (var response = await httpClient.GetAsync(Uri, token))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            Content = await response.Content.ReadAsStringAsync();
                        }
                        else
                        {
                            State = PageState.Error;
                            Error = $"Http Error: {response.StatusCode}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    State = PageState.Error;
                    Error = ex.Message;
                }
            }
        }

        /// <summary>
        /// Search text on page. 
        /// Call <see cref="LoadAsync(CancellationToken)"/> before calling.
        /// If the page is not loaded or param is empty return false
        /// </summary>
        /// <param name="search">Search text</param>
        /// <returns>true if found otherwise false</returns>
        public bool Search(string search)
        {
            var result = false;
            try
            {
                if (!String.IsNullOrEmpty(search) && !String.IsNullOrEmpty(Content))
                {
                    result = Content.IndexOf(search) != -1;
                    State = result ? PageState.Found : PageState.NotFound;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebPage.Search Error: {ex.Message}");
                State = PageState.Error;
                Error = ex.Message;
            }
            
            return result;
        }

        /// <summary>
        /// Return Urls from page.
        /// Call <see cref="LoadAsync(CancellationToken)"/> before calling.
        /// If the page is not loaded return empty List
        /// </summary>
        /// <returns>List of Uri from page</returns>
        public IList<Uri> GetUrls()
        {
            var result = new List<Uri>(); //HashSet?
            try
            {
                if (!String.IsNullOrEmpty(Content))
                {
                    var document = (new HtmlParser()).Parse(Content);
                    var links = document.QuerySelectorAll(LinkTag);
                    foreach (var link in links)
                    {
                        var href = link.GetAttribute(UrlAttr);
                        if (href == null)
                        {
                            continue;
                        }
                        //relative url
                        if (!href.StartsWith(HttpPrefix) && !href.StartsWith(HttpsPrefix))
                        {
                            href = Uri.Scheme + "://" + Uri.Host.Trim('/') + "/" + href.Trim('/');
                        }
                        //validate
                        if (!String.IsNullOrEmpty(href) && Uri.TryCreate(href, UriKind.Absolute, out Uri uri))
                        {
                            result.Add(uri);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebPage.GetUrls Error: {ex.Message}");
                State = PageState.Error;
                Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region IComparable Implementation

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return -1;
            }
            if (!(obj is WebPage))
            {
                return -1;
            }

            var webPage = obj as WebPage;
            return String.Compare(webPage.Uri.AbsoluteUri.Trim('/'), Uri.AbsoluteUri.Trim('/'), true);
        }

        #endregion
    }
}