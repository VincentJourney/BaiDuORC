using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace BaiDuOCR
{

    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class Log
    {
        public static LogLevel Level = LogLevel.Info;
        private readonly IHostingEnvironment _hostingEnvironment;
        public Log(IHostingEnvironment hostingEnvironment)
        {
            this._hostingEnvironment = hostingEnvironment;
        }

        public static Action<LogLevel, string, Exception> LogAction = (level, message, ex) =>
        {
            if (level >= Level)
            {
                string root = $"{Directory.GetCurrentDirectory()}/Log/{DateTime.Now.ToString("yyyy-MM-dd")}-logInfo.txt";
                StreamWriter sw = File.AppendText(root);
                var loginfo = $@"
时间:{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}
类型:{level}
基本信息:{message} 
基本信息:{JsonConvert.SerializeObject(ex)}";
                sw.WriteLine(loginfo);
                sw.Close();
            }
        };

        public static void Warn(string Message, Exception ex) => LogAction(LogLevel.Warn, Message, ex);
        public static void Error(string Message, Exception ex) => LogAction(LogLevel.Error, Message, ex);
    }
}
