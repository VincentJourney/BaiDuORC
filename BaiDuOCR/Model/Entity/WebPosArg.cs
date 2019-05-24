using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    public class WebPosArg
    {
        public string companyID { get; set; }
        public string orgID { get; set; }
        public string storeID { get; set; }
        public string cashierID { get; set; }
        public decimal discountPercentage { get; set; }
        public string cardID { get; set; }
        public DateTime txnDateTime { get; set; }
        public string receiptNo { get; set; }
    }
}
