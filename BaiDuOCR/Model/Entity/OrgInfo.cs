using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("OrgInfo")]
    public class OrgInfo
    {
        [ExplicitKey]
        public Guid OrgId { get; set; }
        public string OrgCode { get; set; }
    }
}
