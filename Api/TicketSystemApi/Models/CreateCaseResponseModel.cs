using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TicketSystemApi.Models
{
    public class CreateCaseResponse
    {
        public Guid CaseId { get; set; }
        public string TicketNumber { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string Status { get; set; }
    }

}