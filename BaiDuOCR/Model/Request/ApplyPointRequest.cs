using BaiDuOCR.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Request
{
    public class ApplyPointRequest
    {
        public string cardId { get; set; }
        public ReceiptOCR receiptOCR { get; set; }
    }
}
