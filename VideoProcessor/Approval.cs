using System;
using System.Collections.Generic;
using System.Text;

namespace VideoProcessor
{
    public class Approval
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string OrchestrationId { get; set; }
    }
}
