using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Util
{
    public class ObjectConfig
    {
        /// <summary>
        /// 数据库连接
        /// </summary>
        public string ConnectionString { get; }
        public BaiDuSetting BaiDuSetting { get; set; }
        public CleanFilesInfo CleanFilesInfo { get; set; }
    }

    /// <summary>
    /// 百度API的配置项
    /// </summary>
    public class BaiDuSetting
    {
        public string AppKey { get; set; }
        public string AppSecret { get; set; }
    }

    /// <summary>
    /// 清理文件配置项
    /// </summary>
    public class CleanFilesInfo
    {
        /// <summary>
        /// 需清理的文件夹
        /// </summary>
        public string Folder { get; set; }
        /// <summary>
        /// 文件保存的月份
        /// </summary>
        public string SaveDay { get; set; }
    }
}
