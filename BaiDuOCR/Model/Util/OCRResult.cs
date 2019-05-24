using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Util
{
    public class OCRResult
    {
        public string log_id { get; set; }
        public int words_result_num { get; set; }
        public List<Words> words_result { get; set; }
    }

    public class Words
    {
        public string words { get; set; }

    }
}
