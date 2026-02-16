using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web.Http;
using TicketSystemApi.Models;
using TicketSystemApi.Services;

namespace TicketSystemApi.Controllers
{
    [Authorize]
    [RoutePrefix("api/cases")]
    public class CreateCaseController : ApiController
    {
        [HttpPost]
        [Route("create")]
        public IHttpActionResult CreateCase(CreateCaseRequest request)
        {
            try
            {
                var identity = (ClaimsIdentity)User.Identity;
                var username = identity.FindFirst("crm_username")?.Value;
                var password = identity.FindFirst("crm_password")?.Value;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Unauthorized();

                if (request == null)
                    return BadRequest("Request body is missing.");

                if (string.IsNullOrWhiteSpace(request.Title))
                    return BadRequest("Title is required.");

                var service = new CrmService().GetService1(username, password);
                var incident = new Entity("incident");

                incident["title"] = request.Title;

                // ======================================
                // CUSTOMER LOGIC (AUTO PICK FIRST MATCH)
                // ======================================

                if (request.CustomerId.HasValue && request.CustomerId != Guid.Empty)
                {
                    var type = request.CustomerType?.ToLower() == "contact"
                        ? "contact"
                        : "account";

                    incident["customerid"] =
                        new EntityReference(type, request.CustomerId.Value);
                }
                else if (!string.IsNullOrWhiteSpace(request.CompanyName))
                {
                    var normalizedCompany = Normalize(request.CompanyName);

                    var query = new QueryExpression("account")
                    {
                        ColumnSet = new ColumnSet("accountid", "name")
                    };

                    var accounts = service.RetrieveMultiple(query).Entities;

                    var matchedAccount = accounts
                        .FirstOrDefault(a =>
                            Normalize(a.GetAttributeValue<string>("name")) == normalizedCompany
                        );

                    if (matchedAccount == null)
                        return BadRequest("Company not found.");

                    incident["customerid"] =
                        new EntityReference("account", matchedAccount.Id);
                }
                else if (!string.IsNullOrWhiteSpace(request.FirstName) &&
                         !string.IsNullOrWhiteSpace(request.LastName))
                {
                    var normalizedFirst = Normalize(request.FirstName);
                    var normalizedLast = Normalize(request.LastName);

                    var query = new QueryExpression("contact")
                    {
                        ColumnSet = new ColumnSet("contactid", "firstname", "lastname")
                    };

                    var contacts = service.RetrieveMultiple(query).Entities;

                    var matchedContact = contacts
                        .FirstOrDefault(c =>
                            Normalize(c.GetAttributeValue<string>("firstname")) == normalizedFirst &&
                            Normalize(c.GetAttributeValue<string>("lastname")) == normalizedLast
                        );

                    if (matchedContact == null)
                        return BadRequest("Contact not found.");

                    incident["customerid"] =
                        new EntityReference("contact", matchedContact.Id);
                }
                else
                {
                    return BadRequest("Provide CustomerId OR CompanyName OR FirstName and LastName.");
                }

                // ======================================
                // OPTION SETS
                // ======================================

                if (request.NewClass.HasValue)
                    incident["new_class"] = new OptionSetValue(request.NewClass.Value);
                else if (!string.IsNullOrWhiteSpace(request.NewClassLabel))
                    incident["new_class"] =
                        new OptionSetValue(MapNewClass(request.NewClassLabel));

                if (request.PriorityCode.HasValue)
                    incident["prioritycode"] =
                        new OptionSetValue(request.PriorityCode.Value);
                else if (!string.IsNullOrWhiteSpace(request.PriorityLabel))
                    incident["prioritycode"] =
                        new OptionSetValue(MapPriority(request.PriorityLabel));

                if (request.NewBeneficiaryType.HasValue)
                    incident["new_beneficiarytype"] =
                        new OptionSetValue(request.NewBeneficiaryType.Value);
                else if (!string.IsNullOrWhiteSpace(request.NewBeneficiaryTypeLabel))
                    incident["new_beneficiarytype"] =
                        new OptionSetValue(MapBeneficiaryType(request.NewBeneficiaryTypeLabel));

                if (request.NewTicketSubmissionChannel.HasValue)
                    incident["new_ticketsubmissionchannel"] =
                        new OptionSetValue(request.NewTicketSubmissionChannel.Value);
                else if (!string.IsNullOrWhiteSpace(request.NewTicketSubmissionChannelLabel))
                    incident["new_ticketsubmissionchannel"] =
                        new OptionSetValue(MapSubmissionChannel(request.NewTicketSubmissionChannelLabel));

                if (!string.IsNullOrWhiteSpace(request.Description))
                    incident["description"] = request.Description;

                // ======================================
                // CREATE RECORD
                // ======================================

                var caseId = service.Create(incident);

                // ======================================
                // FORCE FORM TYPE TO MINISTRY (VALUE = 2)
                // ======================================

                var updateEntity = new Entity("incident");
                updateEntity.Id = caseId;
                updateEntity["new_formtype"] = new OptionSetValue(2);
                service.Update(updateEntity);

                // ======================================
                // RETRIEVE CREATED RECORD
                // ======================================

                var createdCase = service.Retrieve(
                    "incident",
                    caseId,
                    new ColumnSet("ticketnumber", "createdon", "statuscode")
                );

                return Ok(new
                {
                    CaseId = caseId,
                    TicketNumber = createdCase.GetAttributeValue<string>("ticketnumber"),
                    CreatedOn = createdCase.GetAttributeValue<DateTime?>("createdon"),
                    Status = createdCase.FormattedValues.Contains("statuscode")
                                ? createdCase.FormattedValues["statuscode"]
                                : null,
                    Message = "Case created successfully"
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception($"Create Case failed: {ex.Message}")
                );
            }
        }

        // ======================================
        // NORMALIZER
        // ======================================

        private string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var cleaned = Regex.Replace(input.Trim(), @"\s+", " ");
            return cleaned.ToLower();
        }

        // ======================================
        // MAPPERS
        // ======================================

        private int MapNewClass(string label)
        {
            switch (label.ToLower())
            {
                case "a": return 1;
                case "b": return 2;
                case "c": return 3;
                case "not applicable": return 4;
                default: throw new Exception("Invalid Class label.");
            }
        }

        private int MapPriority(string label)
        {
            switch (label.ToLower())
            {
                case "critical": return 1;
                case "high": return 2;
                case "medium": return 3;
                case "low": return 4;
                default: throw new Exception("Invalid Priority label.");
            }
        }

        private int MapBeneficiaryType(string label)
        {
            switch (label.ToLower())
            {
                case "individual": return 1;
                case "company": return 2;
                case "investor": return 3;
                case "investor representative": return 4;
                case "other": return 5;
                default: throw new Exception("Invalid Beneficiary Type label.");
            }
        }

        private int MapSubmissionChannel(string label)
        {
            switch (label.ToLower())
            {
                case "ministry": return 10;
                case "email": return 100000000;
                case "phone calls": return 1;
                case "live chat": return 2;
                case "branches": return 3;
                case "contact us": return 4;
                case "book an appointment": return 9;
                case "whatsapp": return 5;
                case "chatbot": return 6;
                default: throw new Exception("Invalid Submission Channel label.");
            }
        }
    }
}
