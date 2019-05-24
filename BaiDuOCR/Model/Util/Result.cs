using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Util
{
    public class Result
    {
        public Result(bool Success, string Messages, Object Data)
        {
            this.Success = Success;
            this.Message = Messages;
            this.Data = Data;
        }
        public string Message { get; set; }
        public Object Data { get; set; }
        public bool Success { get; set; }
    }
}
