using System;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using LinkedInData.Ui.Model;
using GalaSoft.MvvmLight.Command;
using System.Threading.Tasks;
using CefSharp;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Threading;
using System.Web;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using System.Net;
using System.Text;

namespace LinkedInData.Ui.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// See http://www.mvvmlight.net
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private string _browserUrl = "http://www.linkedin.it";

        /// <summary>
        /// Gets the BrowserUrl property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string BrowserUrl
        {
            get
            {
                return _browserUrl;
            }
            set
            {
                Set(ref _browserUrl, value);
            }
        }

        private int _startFrom = Convert.ToInt32((Properties.Settings.Default["StartFrom"]));

        /// <summary>
        /// Gets the Page property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int StartFrom
        {
            get
            {
                return _startFrom;
            }
            set
            {
                Set(ref _startFrom, value);
                Properties.Settings.Default["StartFrom"] = value;
                Properties.Settings.Default.Save();
            }
        }

        private int _count = 200;

        /// <summary>
        /// Gets the Count property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                Set(ref _count, value);
            }
        }


        private string _progress = string.Empty;

        /// <summary>
        /// Gets the Progress property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string Progress
        {
            get
            {
                return _progress;
            }
            set
            {
                Set(ref _progress, value);
            }
        }

        private bool _executing = false;

        /// <summary>
        /// Gets the Progress property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public bool Executing
        {
            get
            {
                return _executing;
            }
            set
            {
                Set(ref _executing, value);
            }
        }


        public RelayCommand StartScan { get; set; }

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            StartScan = new RelayCommand(InternalStartScan);
        }

        private async void InternalStartScan()
        {
            Executing = true;

            CookieContainer cookieContainer = new CookieContainer();
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            Cef.GetGlobalCookieManager().VisitAllCookies(new webview_cookies(cookieContainer, resetEvent));

            resetEvent.WaitOne();

            CookieAwareWebClient wc = new CookieAwareWebClient(cookieContainer);
            wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.116 Safari/537.36";
            wc.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            wc.Headers[HttpRequestHeader.AcceptLanguage] = "it-IT,it;q=0.8,en-US;q=0.6,en;q=0.4,de-DE;q=0.2,de;q=0.2";
            wc.Encoding = Encoding.UTF8;

            string startUrl = "https://www.linkedin.com/people/filters-and-conns?fetchConnsFromDB=false";

            string pagerUrl = "https://www.linkedin.com/people/conn-list-view?fetchConnsFromDB=false&pageNum={0}";

            bool success = false;
            System.IO.MemoryStream ms = new System.IO.MemoryStream();

            await Task.Factory.StartNew(() =>
            {
                List<LinkedInContact> contactsList = new List<LinkedInContact>();

                try
                {
                    int pageNum = StartFrom / 10, total = 0;

                    string page = wc.DownloadString(new Uri(startUrl));
                    HtmlDocument document = new HtmlDocument();
                    document.LoadHtml(page);

                    var totalNode = document.DocumentNode.SelectSingleNode("//li[@id='allConns']//em");
                    if (totalNode != null)
                        int.TryParse(totalNode.InnerText.Replace("(", "").Replace(")", ""), out total);


                    Progress = $"0 / {Count} ({total})";

                    while (((pageNum * 10) -  StartFrom) < Count && pageNum < total / 10)
                    {
                        try
                        {
                            if (pageNum > 0)
                            {
                                Thread.Sleep(200);
                                page = wc.DownloadString(new Uri(string.Format(pagerUrl, pageNum)));
                                document = new HtmlDocument();
                                document.LoadHtml(page);
                            }

                            var contactsNodes = document.DocumentNode.SelectNodes("//ul[@class='conx-list']/li");
                            if (contactsNodes != null)
                            {
                                int pageContactIndex = 1;
                                foreach (var contactNode in contactsNodes)
                                {
                                    try
                                    {
                                        if (contactNode.Attributes["id"] != null)
                                        {
                                            Thread.Sleep(200);

                                            Progress = $"{ pageNum * 10 + pageContactIndex} / {total}";

                                            LinkedInContact contact = new LinkedInContact();

                                            contact.ID = contactNode.Attributes["id"].Value;

                                            var contactDetailsHtml = wc.DownloadString(string.Format("https://www.linkedin.com/people/conn-details?i=&contactMemberID={0}", contactNode.Attributes["id"].Value));
                                            HtmlDocument contactDetailsDoc = new HtmlDocument();
                                            contactDetailsDoc.LoadHtml(contactDetailsHtml);

                                            var contactNameNode = contactDetailsDoc.DocumentNode.SelectSingleNode(".//h4[@class='connection-name']/a");
                                            if (contactNameNode != null)
                                            {
                                                contact.Name = HttpUtility.HtmlDecode(contactNameNode.InnerText.Trim('\n', ' ', '\r'));
                                                contact.Url = contactNameNode.Attributes["href"].Value;
                                            }

                                            var phoneNode = contactDetailsDoc.DocumentNode.SelectSingleNode(".//dl/dt//text()[contains(., 'Telefono')]");
                                            if (phoneNode != null)
                                            {
                                                contact.Phone = Regex.Replace(phoneNode.ParentNode.NextSibling.NextSibling.InnerText, "<[^>]*>", "").Trim('\n', ' ', '\r');
                                                if (contact.Phone.Contains("\n"))
                                                    contact.Phone = contact.Phone.Substring(0, contact.Phone.IndexOf('\n'));
                                            }

                                            var emailNode = contactDetailsDoc.DocumentNode.SelectSingleNode(".//dl/dt//text()[contains(., 'Email')]");
                                            if (emailNode != null)
                                            {
                                                var emailHrefNode = emailNode.ParentNode.NextSibling.NextSibling.SelectSingleNode(".//a");
                                                if (emailHrefNode != null)
                                                    contact.Email = emailHrefNode.InnerText.Trim('\n', ' ', '\r');
                                            }

                                            var jobNode = contactNode.SelectSingleNode(".//span[@class='conn-headline']");
                                            if (jobNode != null)
                                            {
                                                int aziendaNode = jobNode.InnerHtml.IndexOf("- <");
                                                if (aziendaNode > 0)
                                                    contact.Job = jobNode.InnerHtml.Substring(0, jobNode.InnerHtml.IndexOf("- <")).Trim('\n', ' ', '\r');
                                                else
                                                    contact.Job = jobNode.InnerHtml.Trim('\n', ' ', '\r');
                                                var companyNode = jobNode.SelectSingleNode(".//span[@class='company-name']");
                                                if (companyNode != null)
                                                    contact.Company = companyNode.InnerText;
                                            }
                                            contactsList.Add(contact);
                                            pageContactIndex++;
                                        }
                                    }
                                    catch
                                    {

                                    }
                                }
                                pageNum++;
                            }
                        }
                        catch
                        {

                        }
                    }

                    StartFrom = StartFrom + Count;

                    ClosedXML.Excel.XLWorkbook wb = new ClosedXML.Excel.XLWorkbook();
                    var sheet = wb.AddWorksheet("Contatti");

                    sheet.Cell(1, 1).Value = "Id";
                    sheet.Cell(1, 2).Value = "Nome";
                    sheet.Cell(1, 3).Value = "Url";
                    sheet.Cell(1, 4).Value = "Telefono";
                    sheet.Cell(1, 5).Value = "Email";
                    sheet.Cell(1, 6).Value = "Azienda";
                    sheet.Cell(1, 7).Value = "Ruolo";

                    for (int i = 0; i < contactsList.Count; i++)
                    {
                        var contact = contactsList[i];

                        sheet.Cell(i + 2, 1).Value = contact.ID;
                        sheet.Cell(i + 2, 2).Value = contact.Name;
                        sheet.Cell(i + 2, 3).Value = contact.Url;
                        sheet.Cell(i + 2, 4).Value = contact.Phone ?? "-";
                        sheet.Cell(i + 2, 4).SetDataType(XLCellValues.Text);
                        sheet.Cell(i + 2, 5).Value = contact.Email;
                        sheet.Cell(i + 2, 6).Value = contact.Company;
                        sheet.Cell(i + 2, 7).Value = contact.Job;
                    }

                    sheet.Columns(1, 1).AdjustToContents();
                    sheet.Columns(2, 2).AdjustToContents();
                    sheet.Columns(3, 3).Width = 50;
                    sheet.Columns(4, 4).AdjustToContents();
                    sheet.Columns(5, 5).AdjustToContents();
                    sheet.Columns(6, 6).AdjustToContents();
                    sheet.Columns(7, 7).AdjustToContents();

                    wb.SaveAs(ms);

                    success = true;

                }
                catch
                {
                    Messenger.Default.Send(new NotificationMessage<OperationResult>(this, new OperationResult() { Result = false }, "Error"));
                }
            });

            Executing = false;

            if (success)
            {
                Messenger.Default.Send(new NotificationMessage<OperationResult>(this, new OperationResult() { Result = true, Content = ms.ToArray() }, "Success"));
            }

        }

        class webview_cookies : ICookieVisitor
        {
            private CookieContainer CookieContainer;
            private ManualResetEvent ResetEvent;

            public webview_cookies(CookieContainer container, ManualResetEvent resetEvent)
            {
                CookieContainer = container;
                ResetEvent = resetEvent;
            }

            public void Dispose()
            {

            }

            public bool Visit(CefSharp.Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                CookieContainer.Add(new System.Net.Cookie(cookie.Name, cookie.Value) { Domain = cookie.Domain, HttpOnly = cookie.HttpOnly, Path = cookie.Path, Secure = cookie.Secure });

                if (count == total - 1)
                    ResetEvent.Set();
                return true;
            }
        }


        public class CookieAwareWebClient : WebClient
        {
            public CookieContainer CookieContainer { get; set; }
            public Uri Uri { get; set; }

            public CookieAwareWebClient()
                : this(new CookieContainer())
            {
            }

            public CookieAwareWebClient(CookieContainer cookies)
            {
                this.CookieContainer = cookies;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request is HttpWebRequest)
                {
                    (request as HttpWebRequest).CookieContainer = this.CookieContainer;
                }
                HttpWebRequest httpRequest = (HttpWebRequest)request;
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                return httpRequest;
            }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                WebResponse response = base.GetWebResponse(request);

                return response;
            }
        }

        ////public override void Cleanup()
        ////{
        ////    // Clean up if needed

        ////    base.Cleanup();
        ////}
    }
}