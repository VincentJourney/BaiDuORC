using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;

namespace BaiDuOCR.Model.Entity
{
    [Table("ApplyPictureRecongnize")]
    public class ApplyPictureRecongnize
    {
        [ExplicitKey]
        public Guid id { get; set; }
        public Guid applyid { get; set; }
        public int Lineno { get; set; }
        public string LineContent { get; set; }

        public string OCRResult { get; set; }
    }
}
