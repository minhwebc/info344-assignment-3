using ClassLibrary;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [ScriptService]

    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {
        private static Crawler crawler = new Crawler();

        [WebMethod]
        public string HelloWorld()
        {
            return "Hello World";
        }


        [WebMethod]
        public void StartCrawling()
        {
            Storage.CreateStorage();
            CloudQueueMessage msg = new CloudQueueMessage("http://www.cnn.com/robots.txt");
            CloudQueueMessage msg2 = new CloudQueueMessage("http://bleacherreport.com/robots.txt");
            Storage.LinkQueue.Clear();
            Storage.LinkQueue.AddMessage(msg);
            Storage.LinkQueue.AddMessage(msg2);
            Storage.CommandQueue.AddMessage(new CloudQueueMessage("Initialize Crawl"));
            //crawler.SendCommand("New Crawl");
        }

        [WebMethod]
        public void StopCrawling()
        {
            Storage.CommandQueue.AddMessage(new CloudQueueMessage("Stop Crawl"));
        }

        [WebMethod]
        public List<WebPageEntity> GetLinksTable()
        {
            var entities = Storage.LinkTable.ExecuteQuery(new TableQuery<WebPageEntity>()).ToArray();
            List<WebPageEntity> result = new List<WebPageEntity>();
            foreach(WebPageEntity entity in entities)
            {
                result.Add(entity);
            }
            return result;
        }

        [WebMethod]
        public string UpdateDashboard()
        {
            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<Dashboard>("1", "1");

            // Execute the operation.
            TableResult retrievedResult = Storage.DashboardTable.Execute(retrieveOperation);

            // Assign the result to a CustomerEntity object.
            Dashboard dashboard = (Dashboard)retrievedResult.Result;
            JavaScriptSerializer s = new JavaScriptSerializer();
            return s.Serialize(dashboard);
        }
    }
}
