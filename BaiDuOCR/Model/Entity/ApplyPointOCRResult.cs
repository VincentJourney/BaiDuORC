using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

namespace BaiDuOCR.Model.Entity
{
    [Table("ApplyPointOCRResult")]
    public class ApplyPointOCRResult
    {
        [ExplicitKey]
        public Guid id { get; set; }
        public Guid applyid { get; set; }
        public string StoreCode { get; set; }
        public string ReceiptNo { get; set; }
        public decimal TranAmount { get; set; }
        public DateTime TransDatetime { get; set; }
        public Guid StoreID { get; set; }
        /// <summary>
        /// 识别标记 0 未识别 1 部分识别 2 全部识别
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// 是否需要校验 0 不需要 1 需要 （根据店铺自动积分规则
        /// </summary>
        public int needVerify { get; set; }
        /// <summary>
        /// 校验标记 0 未校验 1 完成校验
        /// </summary>
        public int VerifyStatus { get; set; }
    }
}
