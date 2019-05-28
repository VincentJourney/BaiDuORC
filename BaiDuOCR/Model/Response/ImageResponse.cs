using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Response
{

    public class ImageResponse
    {
        public string fileID { get; set; }

        public string fileType { get; set; }

        public string extName { get; set; }

        public string fileName { get; set; }

        public string fileURL { get; set; }

        public string fileCode { get; set; }

        public string orginalFileName { get; set; }

        public string addedOn { get; set; }

        public string isDelete { get; set; }

        public string fileDescription { get; set; }

        public string sourceSystem { get; set; }

    }
}
