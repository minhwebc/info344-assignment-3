﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary
{
    public class Dashboard : TableEntity
    {
        public Dashboard()
        {
            this.PartitionKey = "1";
            this.RowKey = "1";
        }

        public string CrawlingState { get; set; }
        public double CpuUsage { get; set; }
        public double RamAvailable { get; set; }
        public string last10Urls { get; set; }
        public int SizeOfQueue { get; set; }
        public int SizeOfTable { get; set; }
        public int NumberOfUrlsCrawled { get; set; }
        public string errorUris { get; set; }
        public string CrawlingFor { get; set; }
        public string BeganCrawlingAt { get; set; }
    }
}