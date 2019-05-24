using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("Company")]
    public class Company
    {
        [ExplicitKey]
        public Guid CompanyId { get; set; }
        public string CompanyCode { get; set; }
    }
}
