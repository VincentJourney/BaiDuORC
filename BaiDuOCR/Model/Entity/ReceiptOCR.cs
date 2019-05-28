using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaiDuOCR.Attr;

namespace BaiDuOCR.Model.Entity
{
    public class ReceiptOCR
    {
        public Guid StoreId { get; set; }
        public string StoreName { get; set; }
        public string StoreCode { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime TransDatetime { get; set; }
        public decimal TranAmount { get; set; }
        /// <summary>
        /// 识别原始表Id
        /// </summary>
        [ValidateAttr(IgnoreKey = "RecongnizelId")]
        public Guid RecongnizelId { get; set; }

        public string Base64 { get; set; }
    }
}
