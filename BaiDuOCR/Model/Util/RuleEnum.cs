using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Util
{
    /// <summary>
    /// 关键字类型
    /// </summary>
    public enum OCRKeyType
    {
        /// <summary>
        /// 商铺编号
        /// </summary>
        StoreName = 1,
        /// <summary>
        /// 小票号
        /// </summary>
        ReceiptNO,
        /// <summary>
        /// 交易时间
        /// </summary>
        DateTime,
        /// <summary>
        /// 交易金额
        /// </summary>
        Amount

    }


    /// <summary>
    /// 取值方法
    /// </summary>
    public enum GetValueWay
    {
        /// <summary>
        /// 关键字
        /// </summary>
        OCRKey = 1,
        /// <summary>
        /// 关键字后
        /// </summary>
        AfterOCRKey,
        /// <summary>
        /// 隔行
        /// </summary>
        NextLine,
    }
}
