using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("ApplyPoint")]
    public class ApplyPoint
    {
        [ExplicitKey]
        public Guid ApplyPointID { get; set; }
        public Guid MallID { get; set; }
        public Guid StoreID { get; set; }
        /// <summary>
        /// 小票号
        /// </summary>
        public string ReceiptNo { get; set; }
        /// <summary>
        /// 交易日期
        /// </summary>
        public DateTime TransDate { get; set; }
        /// <summary>
        /// 交易金额
        /// </summary>
        public decimal TransAmt { get; set; } = 0;
        public Guid CardID { get; set; }
        public string MobileNO { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>
        /// 小票图片
        /// </summary>
        public string ReceiptPhoto { get; set; } = "";

        /// <summary>
        /// 0 未审核  1 已审核  2  驳回
        /// </summary>
        public int Status { get; set; } = 1;
        /// <summary>
        /// 识别状态 0 未识别  1 已解析原始数据  2 成功完成关键字匹配 3 未成功完成关键字匹配
        /// </summary>
        public int RecongizeStatus { get; set; } = 1;
        /// <summary>
        /// 校验状态 0 校验失败 1 校验成功
        /// </summary>
        public int VerifyStatus { get; set; } = 0;

        /// <summary>
        /// 审批人
        /// </summary>
        public Guid Auditor { get; set; } = Guid.Empty;
        /// <summary>
        /// 审批时间  ： 当SourceType =7时，审批时间为校验完成时间
        /// </summary>
        public DateTime AuditDate { get; set; } = DateTime.Now;
        /// <summary>
        /// 来源类型 添加类型7=OCR引擎
        /// </summary>
        public int SourceType { get; set; } = 7;
        /// <summary>
        /// 激励日志Id
        /// </summary>
        public Guid RewardCyclelogID { get; set; } = Guid.Empty;
        /// <summary>
        /// 交易ID
        /// </summary>
        public Guid TransID { get; set; } = Guid.Empty;
        public string Remark { get; set; } = "";
        public int Enable { get; set; } = 1;
        public DateTime AddedOn { get; set; } = DateTime.Now;


    }
}
