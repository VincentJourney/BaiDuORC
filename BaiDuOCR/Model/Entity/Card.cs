using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Model.Entity
{
    [Table("Card")]
    public class Card
    {
        [ExplicitKey]
        public Guid CardID { get; set; }
        public string CardCode { get; set; }
    }
}
