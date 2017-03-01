using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ClassLibrary
{
    public static class Storage
    {
        public static CloudStorageAccount StorageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=minhwebc1;AccountKey=uUTJXnkY/kZlNKBNjUlu3JcEBc6n40jTQLrfPNsWUQy/jj42JLKQGFIygALKN4dUkFASp5guHZybxKjVvb4p0Q==");
        public static CloudQueueClient QueueClient = StorageAccount.CreateCloudQueueClient();
        public static CloudTableClient TableClient = StorageAccount.CreateCloudTableClient();
        public static CloudQueue LinkQueue = QueueClient.GetQueueReference("linkqueue");
        public static CloudQueue CommandQueue = QueueClient.GetQueueReference("commandqueue");
        public static CloudTable LinkTable = TableClient.GetTableReference("linktablb");
        public static CloudTable DashboardTable = TableClient.GetTableReference("dashboardtablee");

        public static void CreateStorage()
        {
            LinkQueue.CreateIfNotExists();
            CommandQueue.CreateIfNotExists();
            LinkTable.CreateIfNotExists();
            DashboardTable.CreateIfNotExists();
        }
    }
}
