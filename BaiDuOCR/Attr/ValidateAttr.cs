using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Attr
{
    public class ValidateAttr : Attribute
    {
        public ValidateAttr()
        {

        }

        public string IgnoreKey { get; set; }
    }
}
