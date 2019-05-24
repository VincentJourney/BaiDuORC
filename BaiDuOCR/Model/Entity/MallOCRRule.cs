using System;
namespace BaiDuOCR.Model.Entity
{

    public class MallOCRRule
    {
        /// <summary>
        ///  主键 规则Id
        /// </summary>
        public Guid ruleid { get; set; }
        /// <summary>
        ///  广场Id
        /// </summary>
        public Guid MallID { get; set; }
        /// <summary>
        /// 服务器地址
        /// </summary>
        public string POSServeURL { get; set; }
        /// <summary>
        /// 校验用户名
        /// </summary>
        public string POSServerUser { get; set; }
        /// <summary>
        /// 用户密码
        /// </summary>
        public string POSServerPassword { get; set; }
        /// <summary>
        /// 服务Token
        /// </summary>
        public string POSServerToken { get; set; }
        /// <summary>
        /// OCR服务地址
        /// </summary>
        public string OCRServerURL { get; set; }
        /// <summary>
        /// OCR服务地址
        /// </summary>
        public string OCRServerAccess { get; set; }
        /// <summary>
        /// OCR服务访问信息
        /// </summary>
        public string OCRServerToken { get; set; }

    }
}

