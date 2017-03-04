using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WebRole2
{
    public static class Storage
    {
        public static CloudStorageAccount StorageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=crawler1234;AccountKey=pP+6N/s5APUNjV3vbtUCM3+QNJPbOvTQ41h9h35Wx/3DDg1CVL4p7fzULu2lXRMAnwyjg+5TbLmefsis73eYeg==");
        public static CloudQueueClient QueueClient = StorageAccount.CreateCloudQueueClient();
        public static CloudTableClient TableClient = StorageAccount.CreateCloudTableClient();
        public static CloudQueue LinkQueue = QueueClient.GetQueueReference("linkqueue");
        public static CloudQueue CommandQueue = QueueClient.GetQueueReference("commandqueue");
        public static CloudTable LinkTable = TableClient.GetTableReference("linktablc");
        public static CloudTable DashboardTable = TableClient.GetTableReference("dashboardtable");
    }
}
