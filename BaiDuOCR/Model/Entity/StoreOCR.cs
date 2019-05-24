using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("StoreOCR")]
    public class StoreOCR
    {
        [ExplicitKey]
        public Guid ruleid { get; set; }
        public Guid storeid { get; set; }
        /// <summary>
        /// 最小合法小票金额
        /// </summary>
        public decimal MinValidReceiptValue { get; set; }
        /// <summary>
        /// 最大合法小票金额
        /// </summary>
        public decimal MaxValidReceiptValue { get; set; }
        /// <summary>
        /// 日自动交易笔数 每日最大自动积分记录，0代表不限制
        /// </summary>
        public int MaxTicketPerDay { get; set; }
        /// <summary>
        /// 启动自动积分  0 不启动 1 启动
        /// </summary>
        public int Enabled { get; set; }
        /// <summary>
        /// POS类型  0 租用POS 1 非标POS
        /// </summary>
        public int POSType { get; set; }
        /// <summary>
        /// POS门店代码 记录标准POS在POS系统中的交易代码用于检验
        /// </summary>
        public string POSSID { get; set; }
        /// <summary>
        /// 是否校验 1 校验，0 不校验
        /// </summary>
        public int needVerify { get; set; }
    }
}
