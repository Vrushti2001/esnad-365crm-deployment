using System;

namespace TicketSystemApi.Models
{
    public class CreateCaseRequest
    {
        public string Title { get; set; }

        // Optional – Direct GUID
        public Guid? CustomerId { get; set; }
        public string CustomerType { get; set; }   // "account" or "contact"

        // Contact search
        public string FirstName { get; set; }
        public string LastName { get; set; }

        // Account search
        public string CompanyName { get; set; }

        // ===============================
        // Option Sets
        // ===============================

        public int? NewClass { get; set; }
        public string NewClassLabel { get; set; }

        public int? PriorityCode { get; set; }
        public string PriorityLabel { get; set; }

        public int? NewBeneficiaryType { get; set; }
        public string NewBeneficiaryTypeLabel { get; set; }

        public int? NewTicketSubmissionChannel { get; set; }
        public string NewTicketSubmissionChannelLabel { get; set; }

        // Multiline text
        public string Description { get; set; }
    }
}
