using System.Collections.Generic;

namespace TicketSystemApi.Models
{
    public class TaadeenCaseReportResponse
    {
        public string Filter { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Count { get; set; }
        public int TotalTickets { get; set; }
        public int FilteredTotalTickets { get; set; }
        public int TotalPages { get; set; }
        public List<TaadeenCaseRecordDto> Records { get; set; }
    }

    public class TaadeenCaseRecordDto
    {
        public string TicketID { get; set; }
        public string CreatedBy { get; set; }
        public string AgentName { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCrNumber { get; set; }
        public string CreatedOn { get; set; }
        public string TicketType { get; set; }
        public string MineralClass { get; set; }
        public string Category { get; set; }
        public string SubCategory1 { get; set; }
        public string SubCategory2 { get; set; }
        public string Status { get; set; }
        public string TicketModifiedDateTime { get; set; }
        public string Department { get; set; }
        public string TicketChannel { get; set; }
        public string Description { get; set; }
        public string ModifiedBy { get; set; }
        public string Priority { get; set; }
        public string ResolutionDateTime { get; set; }
        public string CurrentStage { get; set; }
    }
}
