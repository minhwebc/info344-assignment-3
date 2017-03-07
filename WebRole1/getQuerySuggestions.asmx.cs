using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Text;
using System.Web.Script.Services;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure; // Namespace for CloudConfigurationManager
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.WindowsAzure.Storage.Table;
using ClassLibrary;
using System.IO.Compression;

namespace WebRole1
{
    /// <summary>
    /// Summary description for getQuerySuggestions
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [ScriptService]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class getQuerySuggestions : System.Web.Services.WebService
    {
        private static MyTrie storage;
        private static int numberOfTitle = 0;
        private static string lastTitle;
        private static Dictionary<string, List<WebPageEntity>> cache = new Dictionary<string, List<WebPageEntity>>();
        /// <summary>
        /// Download the file
        /// </summary>
        /// <returns>the location of the downloaded file</returns>
        [WebMethod]
        public string DownloadFile()
        {
            // Parse the connection string and return a reference to the storage account.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("first");
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            //string fileName = System.IO.Path.GetTempFileName();
            // Loop over items within the container and output the length and URI.
            string fileName = "";
            if (container.Exists())
            {
                foreach (IListBlobItem item in container.ListBlobs(null, false))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob blob = (CloudBlockBlob)item;
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference("pagecountfilter");
                        //fileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString() + "\\text.txt";
                        using (var fileStream = System.IO.File.OpenWrite(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString() + "\\text.txt"))
                        {
                            blockBlob.DownloadToStream(fileStream);
                        }
                        //Console.WriteLine("Block blob of length {0}: {1}", blob.Properties.Length, blob.Uri);
                        //using (var fileStream = System.IO.File.OpenWrite("@C:\Users\"))
                    }
                }
            }
            return fileName;
        }

        /// <summary>
        /// Reference to the blob, read the blob and buld the trie 
        /// </summary>
        /// <returns>the status of the process</returns>
        [WebMethod]
        public string BuildTrie()
        {
            storage = new MyTrie();
            // Retrieve storage account from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("first");

            // Retrieve reference to a blob named "myblob.txt"
            CloudBlockBlob blob = container.GetBlockBlobReference("pagecountfilter");
            using (var stream = blob.OpenRead())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                    while (!reader.EndOfStream && ramCounter.NextValue() > 20)
                    {
                        string result = "";
                        try
                        {
                            string line = reader.ReadLine();
                            string[] lineComponent = line.Split('|');
                            string word = lineComponent[0];
                            int pageCount = Int32.Parse(lineComponent[1]);
                            word = word.Trim();
                            result = word;
                            storage.Add(word, pageCount);
                            numberOfTitle++;
                            lastTitle = word;
                        }
                        catch (Exception e)
                        {
                            return ("{0} Exception caught." + e + "at word " + result);
                        }
                    }
                    if (reader.EndOfStream)
                    {
                        updateDashboard();
                        return "end of file";
                    }
                }
            }
            return "out of memory ?";
        }

        /// <summary>
        /// Search trie and generate a list of suggestions baed on user input word
        /// </summary>
        /// <param name="prefix">user input word</param>
        /// <returns></returns>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SearchTrie(string prefix)
        {
            List<string> result = new List<string>();
            try
            {
                result = storage.GetWords(prefix.ToLower());
            }catch(Exception e)
            {

            }
            return new JavaScriptSerializer().Serialize(result.ToArray());
        }

        [WebMethod]
        public string ClearCache()
        {
            cache.Clear();
            return "success";
        }

        [WebMethod]
        public void updateDashboard()
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<Dashboard>("1", "1");
            TableResult retrievedResult = Storage.DashboardTable.Execute(retrieveOperation);
            Dashboard dashboard = (Dashboard)retrievedResult.Result;
            dashboard.numberOfTitle = numberOfTitle;
            dashboard.lastTitle = lastTitle;
            TableOperation insertOperation = TableOperation.InsertOrReplace(dashboard);
            Storage.DashboardTable.Execute(insertOperation);
        }

        /// <summary>
        /// search all the word suggestion for the word input by the user if there are less than 10 result
        /// </summary>
        /// <param name="prefix">user input word1</param>
        /// <returns>list of suggestions of the user input word</returns>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SearchSuggestions(string prefix)
        {
            List<string> result = storage.GetSuggestions(prefix.ToLower());
            return new JavaScriptSerializer().Serialize(result.ToArray());
        }

        /// <summary>
        /// Add word into the trie if there are no result
        /// </summary>
        /// <param name="prefix">word to be addd</param>
        /// <returns>message by the server</returns>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string AddWord(string prefix)
        {
            storage.Add(prefix.ToLower(), 0);
            numberOfTitle++;
            lastTitle = prefix;
            updateDashboard();
            return "success";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<WebPageEntity> GetLinks1(string input)
        {
            List<WebPageEntity> result = new List<WebPageEntity>();
            input = input.ToLower();
            string[] word = input.Split(' ');
            string firstWord = word[0];
            var entities = Storage.LinkTable.ExecuteQuery(new TableQuery<WebPageEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, firstWord)));
            Dictionary<string, WebPageEntity> keepingTrack = new Dictionary<string, WebPageEntity>();
            foreach (WebPageEntity entity in entities)
            {
                keepingTrack.Add(entity.Title, entity);
            }
            List<string> keyList = new List<string>(keepingTrack.Keys);
            var wantedWords = input.Split();
            var result1 = from s in keyList
                          let words = s.Split()
                          select new
                          {
                              String = s,
                              MatchedCount = wantedWords.Count(ww => words.Contains(ww))
                          } into e
                          where e.MatchedCount > 0
                          orderby e.MatchedCount descending 
                          select e.String;
            foreach(string key in keyList)
            {
                result.Add(keepingTrack[key]);
            }
            return result;
        }

        public string Decompress(string compressedText)
        {
            byte[] gzBuffer = Convert.FromBase64String(compressedText);
            using (MemoryStream ms = new MemoryStream())
            {
                int msgLength = BitConverter.ToInt32(gzBuffer, 0);
                ms.Write(gzBuffer, 4, gzBuffer.Length - 4);

                byte[] buffer = new byte[msgLength];

                ms.Position = 0;
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetLinks(string input)
        {
            if (cache.ContainsKey(input))
            {
                return new JavaScriptSerializer().Serialize(cache[input].ToArray());
            }
            List<WebPageEntity> resultQuery = new List<WebPageEntity>();
            input = input.ToLower();
            string[] wordsInput = input.Split(' ');
            foreach (string word in wordsInput)
            {
                TableQuery<WebPageEntity> query = new TableQuery<WebPageEntity>()
                                                  .Where(TableQuery.GenerateFilterCondition("PartitionKey",
                                                      QueryComparisons.Equal, word));
                var list = Storage.LinkTable.ExecuteQuery(query).ToList();
                foreach(WebPageEntity url in list)
                {
                    resultQuery.Add(url);
                }
            };
            long epochTicks = new DateTime(1970, 1, 1).Ticks;

            var results = resultQuery
                .GroupBy(x => x.Url)
                .Select(x => new Tuple<WebPageEntity, int>(x.First(), x.ToList().Count()))
                .OrderByDescending(x => x.Item2)
                .ThenByDescending(x => DateTime.Parse(x.Item1.Date))
                .Select(x => x.Item1)
                .ToList();
            foreach(WebPageEntity entity in results)
            {
                entity.Text = Decompress(entity.Text);
                foreach (string word in wordsInput)
                {
                    try
                    {
                        string replacement = string.Format("<b>{0}</b>", "$0");
                        entity.Text = Regex.Replace(entity.Text, Regex.Escape(word), replacement, RegexOptions.IgnoreCase);
                        entity.Title = Regex.Replace(entity.Title, Regex.Escape(word), replacement, RegexOptions.IgnoreCase);
                    }
                    catch (Exception e)
                    {

                    }
                }
                try
                {
                    entity.Text = entity.Text.Substring(0, 2000);
                }catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }
            //Dictionary<string, WebPageEntity> result = new Dictionary<string, WebPageEntity>();
            if(cache.Count >= 100)
            {
                cache.Clear();
            }
            if(results.Count >= 10)
                cache.Add(input, results);
            return new JavaScriptSerializer().Serialize(results.ToArray());
        }
    }
}

