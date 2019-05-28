using BaiDuOCR.Model.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BaiDuOCR.Core
{
    public static class Extension
    {
        public static string ToRString(this Result result) => JsonConvert.SerializeObject(result);
    }
}
