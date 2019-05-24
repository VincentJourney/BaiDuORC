using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("Store")]
    public class Store
    {
        [ExplicitKey]
        public Guid StoreId { get; set; }
        public string StoreCode { get; set; }
        public string StoreName { get; set; }
        /// <summary>
        /// 是否为非标POS 1为标准 
        /// </summary>
        public string IsStandardPOS { get; set; }

        public Guid MallID { get; set; }
        public Guid CompanyID { get; set; }
        public Guid OrgID { get; set; }
    }
}
