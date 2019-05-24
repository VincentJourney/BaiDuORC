using Dapper.Contrib.Extensions;
using System;
namespace BaiDuOCR.Model.Entity
{
    [Table("StoreOCRDetail")]
    public class StoreOCRDetail
    {
        /// <summary>
        /// 规则明细ID
        /// </summary>
        [ExplicitKey]
        public Guid RuleDetailId { get; set; }
        /// <summary>
        /// 门店ID
        /// </summary>
        public Guid StoreId { get; set; }
        /// <summary>
        /// 关键字类型  1 StoreNo 2 ReceiptNO 3 DateTime 4 Amount 5 就餐人数
        /// </summary>
        public int OCRKeyType { get; set; }
        /// <summary>
        /// 关键字 例如相关关键字的统配检查
        /// </summary>
        public string OCRKey { get; set; }
        /// <summary>
        /// 取值方法 1 关键字 2 关键字后 3 隔行
        /// </summary>
        public int GetValueWay { get; set; }
        /// <summary>
        /// 隔行数量  可以为0 1代表下一行取数
        /// </summary>
        public int SkipLines { get; set; }
        /// <summary>
        /// 取值规则 写成公式模板，可以取左，取右或者取中间，包括取值长度
        /// </summary>
        public string GetValueRule { get; set; }




    }
}

