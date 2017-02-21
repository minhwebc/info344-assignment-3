﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ClassLibrary {
    public class Crawler
    {
        private Dashboard dashboard;
        private ConcurrentDictionary<string, string> visitedLinks;
        private ConcurrentDictionary<string, string> uniqueUrlInSiteMaps;
        private ConcurrentBag<string> disallowedUrls;
        private string CrawlerState;
        private FixedSizedQueue<string> last10Urls;
        private FixedSizedQueue<string> errorsUrl;
        private int counterTable;
        private int numberOfUrlsCrawled;
        private static EventWaitHandle myWaitHandle;

        public Crawler()
        {
            myWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 100;
            visitedLinks = new ConcurrentDictionary<string, string>();
            uniqueUrlInSiteMaps = new ConcurrentDictionary<string, string>();
            disallowedUrls = new ConcurrentBag<string>();
            CrawlerState = "Idle";
            last10Urls = new FixedSizedQueue<string>();
            last10Urls.Limit = 10;
            errorsUrl = new FixedSizedQueue<string>();
            errorsUrl.Limit = 10;
            counterTable = 0;
            numberOfUrlsCrawled = 0;
            dashboard = new Dashboard();
            updateDashboard();
        }

        /// <summary>
        /// Get the state of the crawler (loading, idle, crawling)
        /// </summary>
        /// <returns></returns>
        public string GetCrawlerState()
        {
            return CrawlerState;
        }

        /// <summary>
        /// Start the crawling process
        /// </summary>
        public void Start()
        {
            CrawlerState = "Loading";
            DateTime begin = DateTime.Now;
            TimeSpan time = DateTime.Now.Subtract(begin);
            dashboard.CrawlingFor = time.ToString();
            dashboard.BeganCrawlingAt = begin.ToString();
        }

        /// <summary>
        /// Stop the crawling process
        /// </summary>
        public void Stop()
        {
            CrawlerState = "Idle";
        }

        /// <summary>
        /// Method will crawl the url
        /// </summary>
        /// <param name="url"></param>
        public void CrawlUrl(string url)
        {
            if (CrawlerState.Equals("Idle"))
            {
                return;
            }
            Uri uri = new Uri(url);
            if (url.EndsWith("robots.txt"))
            {
                ThreadPool.QueueUserWorkItem(o => CrawlRobots(uri));
            }
            else
            {
                
                if (!visitedLinks.ContainsKey(uri.AbsoluteUri))
                {
                    ThreadPool.QueueUserWorkItem(o => CrawlPage(uri.AbsoluteUri));
                }
            }
        }


        /// <summary>
        /// Method check if we are done with sitesmap before proceed to the crawling state
        /// </summary>
        private void ProceedThreading()
        {
            if(disallowedUrls.Count >= 43)
            {
                myWaitHandle.Set();
                CrawlerState = "Crawling";
            }
            
        }

        /// <summary>
        /// Crawl robot sites
        /// </summary>
        /// <param name="link"></param>
        public void CrawlRobots(Uri link)
        {
            StreamReader contentReader = GetStreamFromUri(link.AbsoluteUri);
            while (!contentReader.EndOfStream)
            {
                string line = contentReader.ReadLine();
                string[] components = line.Split(':');
                if (components[0].Equals("Sitemap")) 
                {
                    if (link.Authority.Equals("bleacherreport.com"))
                    {
                        if (components[2].Contains("nba"))
                        {
                            ThreadPool.QueueUserWorkItem(o => ProcessSiteMaps(components[1] + ":" + components[2]));
                        }
                    }else
                    {
                        ThreadPool.QueueUserWorkItem(o => ProcessSiteMaps(components[1] + ":" + components[2]));
                    }
                    
                }else if (components[0].Equals("Disallow"))
                {
                    disallowedUrls.Add("http://" + link.Authority + components[1].Substring(1)); //add disallowedUrls so crawlers wont crawl them
                }
            }
            ProceedThreading();
        }


        /// <summary>
        /// Method will determine what to do with a site map, if it contains -index or not
        /// </summary>
        /// <param name="url"></param>
        private void ProcessSiteMaps(string url)
        {
            if (CrawlerState.Equals("Idle"))
            {
                return;
            }
            if (url.Contains("-index"))
            {
                CrawlSiteMapIndex(url);
            }
            else
            {
                CrawlSiteMap(url);
            }
        }

        /// <summary>
        /// Method will crawl site map that has index in it
        /// </summary>
        /// <param name="url"></param>
        private void CrawlSiteMapIndex(string url)
        {
            HtmlDocument htmlContent = GetWebText(url);
            if (htmlContent == null)
                return;
            HtmlNodeCollection sitemaps = htmlContent.DocumentNode.SelectNodes("//sitemap");
            if(sitemaps != null)
            {
                foreach(HtmlNode sitemap in sitemaps)
                {
                    HtmlNode loc = sitemap.SelectSingleNode("//loc");
                    string link = loc.InnerText;
                    if (link.Contains("www.cnn.com"))
                    {
                        try
                        {
                            DateTime sitemapDate = Convert.ToDateTime(sitemap.LastChild.InnerText);
                            int difference = ((DateTime.Now.Year - sitemapDate.Year) * 12) + DateTime.Now.Month - sitemapDate.Month;
                            if (difference <= 2)
                            {
                                ProcessSiteMaps(link);
                            }
                        }
                        catch (Exception e) { };
                    }
                    else if (link.Contains("bleacherreport.com"))
                    {
                        if (link.Contains("nba"))
                        {
                            ProcessSiteMaps(link);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Method will clean up the url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="authority"></param>
        /// <returns></returns>
        private string CleanUrl(string url, string authority)
        {
            string cleanUrl = "";
            if (!url.Contains("realestate.money.cnn.com"))
            {
                if ((url.StartsWith("http://") || url.StartsWith("https://")))
                {
                    if(url.Contains("cnn.com") || url.Contains("bleacherreport.com"))
                        cleanUrl = url;
                }
                else
                {
                    if (url.Contains("cnn.com") || url.Contains("bleacherreport.com"))
                    {
                        if (url.StartsWith("/"))
                        {
                            cleanUrl = "http:" + url;
                        }
                        else
                        {
                            cleanUrl = "http://" + url;
                        }
                    }
                    else
                    {
                        if (url.StartsWith("/"))
                        {
                            cleanUrl = "http://" + authority + url;
                        }
                    }
                }
            }
            return cleanUrl;
        }

        /// <summary>
        /// Crawl "regular" sitemap (without index keyword)
        /// </summary>
        /// <param name="url"></param>
        private void CrawlSiteMap(string url)
        {
            HtmlDocument htmlContent = GetWebText(url);
            if (htmlContent == null)
                return;
            HtmlNodeCollection links = htmlContent.DocumentNode.SelectNodes("//url");
            if (links == null)
                return;
            else
            {
                foreach (HtmlNode link in links)
                {
                    HtmlNode loc = link.SelectSingleNode("//loc");
                    if (!uniqueUrlInSiteMaps.ContainsKey(loc.InnerText))
                    {
                        uniqueUrlInSiteMaps.TryAdd(loc.InnerText, "");
                        Storage.LinkQueue.AddMessage(new CloudQueueMessage(loc.InnerText));
                    }
                }
            }
        }

        /// <summary>
        /// Determine if a url is allowed to be crawled 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public bool IsNotAllowed (string url)
        {
            bool result = false;
            foreach(string disallowedUrl in disallowedUrls)
            {
                if (url.Contains(disallowedUrl))
                {
                    result = true;
                }
            }
            return result;
        }

        /// <summary>
        /// Method will the crawl the page getting out the tile and all the new links to be crawled
        /// </summary>
        /// <param name="url"></param>
        public void CrawlPage(string url)
        {
            myWaitHandle.WaitOne();
            if (CrawlerState.Equals("Idle"))
            {
                return;
            }
            if (url == null)
            {
                return;
            }
            if (IsNotAllowed(url))
            {
                return;
            }
            Interlocked.Add(ref numberOfUrlsCrawled, 1);
            if (!visitedLinks.ContainsKey(url))
            {
                visitedLinks.TryAdd(url, ""); 
                last10Urls.Enqueue(url);
                HtmlDocument htmlText = GetWebText(url);
                if (htmlText == null)
                    return;

                HtmlNode title = htmlText.DocumentNode.SelectSingleNode("//title");
                HtmlNodeCollection linkNodes = htmlText.DocumentNode.SelectNodes("//a");
                if (linkNodes == null)
                {
                    return;
                }
                if (title != null)
                {
                    string pageTitle = title.InnerText;
                    Uri link = new Uri(url);
                    var text = htmlText.DocumentNode.Descendants()
                                  .Where(x => x.NodeType == HtmlNodeType.Text && x.InnerText.Trim().Length > 0)
                                  .Select(x => x.InnerText.Trim());
                    WebPageEntity newPage = new WebPageEntity(pageTitle, link.Authority);
                    newPage.Title = pageTitle;
                    newPage.Url = url;
                    newPage.Text = GetPlainTextFromHtml(htmlText.DocumentNode.OuterHtml); 
                    DateTime timeNow = DateTime.Now;
                    newPage.Date = timeNow.ToString("en-US");
                    TableOperation insertOperation = TableOperation.Insert(newPage);
                    try
                    {
                        Interlocked.Add(ref counterTable, 1);
                        Storage.LinkTable.Execute(insertOperation);
                    }
                    catch (Exception error)
                    {
                        
                    }
                }
                //Find all the link in the current page and add them to the queue
                foreach (HtmlNode link in linkNodes)
                {
                    if (CrawlerState.Equals("Idle"))
                    {
                        return;
                    }
                    HtmlAttribute href = link.Attributes["href"];
                    try
                    {
                        string pageLink = href.Value;
                        Uri FullUrl = new Uri(url);
                        pageLink = CleanUrl(pageLink, FullUrl.Authority);
                        if (pageLink != "")
                        {
                            Uri uriResult;
                            bool result = Uri.TryCreate(pageLink, UriKind.Absolute, out uriResult)
                                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                            if (result)
                            {
                                CloudQueueMessage msg = new CloudQueueMessage(pageLink);
                                Storage.LinkQueue.AddMessage(msg);
                            }
                        }
                    }
                    catch (Exception exc)
                    {

                    }
                }
            }
        }

        /// <summary>
        /// Return the summary of the current dahsboard
        /// </summary>  
        /// <returns></returns>
        public void updateDashboard()
        {
            GetPerfCounters();
            CloudQueue link = Storage.LinkQueue;
            link.FetchAttributes();
            dashboard.CrawlingState = GetCrawlerState();
            dashboard.last10Urls = JsonConvert.SerializeObject(last10Urls.GetSnapshot());
            dashboard.errorUris = JsonConvert.SerializeObject(errorsUrl.GetSnapshot());
            dashboard.SizeOfQueue = (int)link.ApproximateMessageCount;
            dashboard.SizeOfTable = counterTable;
            dashboard.NumberOfUrlsCrawled = visitedLinks.ToArray().Length;
            TableOperation insertOperation = TableOperation.InsertOrReplace(dashboard);
            Storage.DashboardTable.Execute(insertOperation);
        }


        /// <summary>
        /// Get system configuration 
        /// </summary>
        public void GetPerfCounters()
        {
            PerformanceCounter mem = new PerformanceCounter("Memory", "Available MBytes", null);
            PerformanceCounter cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            float perfCounterValue = cpu.NextValue();

            //Thread has to sleep for at least 1 sec for accurate value.
            System.Threading.Thread.Sleep(1000);

            perfCounterValue = cpu.NextValue();
            dashboard.CpuUsage = perfCounterValue;
            dashboard.RamAvailable = mem.NextValue();

            if (GetCrawlerState().Equals("Crawling") || GetCrawlerState().Equals("Loading"))
            {
                DateTime begin = Convert.ToDateTime(dashboard.BeganCrawlingAt);
                TimeSpan timespan = DateTime.Now.Subtract(begin);
                string fmt = "hh\\:mm\\:ss";
                dashboard.CrawlingFor = timespan.Days + ":" + timespan.ToString(fmt);
            }
        }

        /// <summary>
        /// Get the content of a page 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public HtmlDocument GetWebText(string url)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Proxy = null;
            request.UserAgent = "A Web Crawler";
            HtmlDocument doc = null;
            try
            {
                WebResponse response = request.GetResponse();
                Stream streamResponse = response.GetResponseStream();
                StreamReader sreader = new StreamReader(streamResponse);
                string s = "";
                s = sreader.ReadToEnd();
                doc = new HtmlDocument();
                doc.LoadHtml(s);
            }
            catch (WebException e)
            {
                errorsUrl.Enqueue(url + " | " + "Error code: "+ e);
            }
            return doc;
        }

        /// <summary>
        /// Method will filter out all the html tags, script tags, and style tags and just get the plain html text
        /// </summary>
        /// <param name="htmlString">html string</param>
        /// <returns></returns>
        private string GetPlainTextFromHtml(string htmlString)
        {
            string htmlTagPattern = "<.*?>";
            var regexCss = new Regex("(\\<script(.+?)\\</script\\>)|(\\<style(.+?)\\</style\\>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            htmlString = regexCss.Replace(htmlString, string.Empty);
            htmlString = Regex.Replace(htmlString, htmlTagPattern, string.Empty);
            htmlString = Regex.Replace(htmlString, @"^\s+$[\r\n]*", "", RegexOptions.Multiline);
            htmlString = htmlString.Replace("&nbsp;", string.Empty);

            return htmlString;
        }

        /// <summary>
        /// Method for reading robots.text
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private StreamReader GetStreamFromUri(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "A Web Crawler";
            WebResponse response = request.GetResponse();
            Stream streamResponse = response.GetResponseStream();
            StreamReader sreader = new StreamReader(streamResponse);
            return sreader;
        }
    }
}