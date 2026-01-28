using System;
using System.Linq;
using System.Security.Claims;
using System.Web.Http;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using TicketSystemApi.Services;
using TicketSystemApi.Models;

namespace TicketSystemApi.Controllers
{
    [Authorize]
    [RoutePrefix("api/taadeen/cases")]
    public class TaadeenCaseReportController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";
        private const int MAX_PAGE_SIZE = 100;

        // =========================================================
        // MAIN API
        // =========================================================
        [HttpGet]
        [Route("report")]
        public IHttpActionResult GetReport(
            string filter = "all",
            int page = 1,
            int pageSize = 50)
        {
            try
            {
                pageSize = Math.Min(pageSize, MAX_PAGE_SIZE);

                var identity = (ClaimsIdentity)User.Identity;
                var username = identity.FindFirst(CLAIM_USERNAME)?.Value;
                var password = identity.FindFirst(CLAIM_PASSWORD)?.Value;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Unauthorized();

                var service = new CrmService().GetService1(username, password);

                var query = BuildQuery(filter, page, pageSize);
                var result = service.RetrieveMultiple(query);

                var records = result.Entities.Select(e => new TaadeenCaseRecordDto
                {
                    TicketID = e.GetAttributeValue<string>("ticketnumber"),
                    CreatedBy = e.GetAttributeValue<EntityReference>("createdby")?.Name,
                    AgentName = e.GetAttributeValue<EntityReference>("ownerid")?.Name,
                    CustomerName = e.GetAttributeValue<EntityReference>("customerid")?.Name,

                    // ✅ Polymorphic (Account OR Contact)
                    CustomerCrNumber =
                        e.Contains("acc.new_crnumber")
                            ? ((AliasedValue)e["acc.new_crnumber"]).Value as string
                            : e.Contains("con.new_crnumber")
                                ? ((AliasedValue)e["con.new_crnumber"]).Value as string
                                : null,

                    CreatedOn = ToKsa(e.GetAttributeValue<DateTime?>("createdon")),
                    TicketType = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                    MineralClass = GetFormatted(e, "new_class"),
                    Category = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                    SubCategory1 = e.GetAttributeValue<EntityReference>("new_mainclassification")?.Name,
                    SubCategory2 = e.GetAttributeValue<EntityReference>("new_subclassificationitem")?.Name,
                    Status = GetFormatted(e, "statuscode"),
                    TicketModifiedDateTime = ToKsa(e.GetAttributeValue<DateTime?>("modifiedon")),
                    Department = e.GetAttributeValue<EntityReference>("new_businessunitid")?.Name,
                    TicketChannel = GetFormatted(e, "new_ticketsubmissionchannel"),
                    Description = e.GetAttributeValue<string>("new_description"),
                    ModifiedBy = e.GetAttributeValue<EntityReference>("modifiedby")?.Name,
                    Priority = GetFormatted(e, "prioritycode"),
                    ResolutionDateTime = null,
                    CurrentStage = MapStatusToStage(e)
                }).ToList();

                int totalTickets =
                    filter.Equals("all", StringComparison.OrdinalIgnoreCase)
                        ? result.TotalRecordCount
                        : GetTotalTickets(service);

                int totalPages =
                    (int)Math.Ceiling((double)result.TotalRecordCount / pageSize);

                return Ok(new TaadeenCaseReportResponse
                {
                    Filter = filter,
                    Page = page,
                    PageSize = pageSize,
                    Count = records.Count,
                    TotalTickets = totalTickets,
                    FilteredTotalTickets = result.TotalRecordCount,
                    TotalPages = totalPages,
                    Records = records
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =========================================================
        // QUERY BUILDER (OPTIMIZED)
        // =========================================================
        private QueryExpression BuildQuery(string filter, int page, int pageSize)
        {
            var query = new QueryExpression("incident")
            {
                ColumnSet = new ColumnSet(
                    "ticketnumber",
                    "createdon",
                    "modifiedon",
                    "statuscode",
                    "prioritycode",
                    "new_description",
                    "new_ticketsubmissionchannel",
                    "new_businessunitid",
                    "createdby",
                    "modifiedby",
                    "ownerid",
                    "customerid",
                    "new_tickettype",
                    "new_mainclassification",
                    "new_subclassificationitem",
                    "new_class"
                ),
                PageInfo = new PagingInfo
                {
                    PageNumber = page,
                    Count = pageSize,
                    ReturnTotalRecordCount = true
                }
            };

            query.Orders.Add(new OrderExpression("modifiedon", OrderType.Descending));
            ApplyDateFilter(query, filter);

            // 🔗 Account join
            var acc = query.AddLink(
                "account",
                "customerid",
                "accountid",
                JoinOperator.LeftOuter);
            acc.Columns = new ColumnSet("new_crnumber");
            acc.EntityAlias = "acc";

            // 🔗 Contact join
            var con = query.AddLink(
                "contact",
                "customerid",
                "contactid",
                JoinOperator.LeftOuter);
            con.Columns = new ColumnSet("new_crnumber");
            con.EntityAlias = "con";

            return query;
        }

        // =========================================================
        // FILTERS
        // =========================================================
        private void ApplyDateFilter(QueryExpression query, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                filter.Equals("all", StringComparison.OrdinalIgnoreCase))
                return;

            var ksaZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
            var ksaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ksaZone);

            switch (filter.ToLower())
            {
                case "yesterday":
                    query.Criteria.AddCondition(
                        "modifiedon",
                        ConditionOperator.Between,
                        ksaNow.Date.AddDays(-1),
                        ksaNow.Date.AddSeconds(-1));
                    break;

                case "daily":
                    query.Criteria.AddCondition(
                        "modifiedon",
                        ConditionOperator.OnOrAfter,
                        ksaNow.Date);
                    break;

                case "weekly":
                    query.Criteria.AddCondition(
                        "modifiedon",
                        ConditionOperator.OnOrAfter,
                        ksaNow.Date.AddDays(-7));
                    break;

                case "monthly":
                    query.Criteria.AddCondition(
                        "modifiedon",
                        ConditionOperator.OnOrAfter,
                        new DateTime(ksaNow.Year, ksaNow.Month, 1));
                    break;
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================
        private string ToKsa(DateTime? utc)
        {
            if (!utc.HasValue) return null;
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc),
                zone).ToString("yyyy-MM-dd HH:mm:ss");
        }

        private string GetFormatted(Entity e, string key)
        {
            return e.FormattedValues.TryGetValue(key, out var v) ? v : null;
        }

        private int GetTotalTickets(IOrganizationService service)
        {
            var q = new QueryExpression("incident")
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo
                {
                    PageNumber = 1,
                    Count = 1,
                    ReturnTotalRecordCount = true
                }
            };
            return service.RetrieveMultiple(q).TotalRecordCount;
        }

        private string MapStatusToStage(Entity e)
        {
            var code = e.GetAttributeValue<OptionSetValue>("statuscode")?.Value;
            switch (code)
            {
                case 100000000: return "Ticket Creation";
                case 100000006: return "Approval and Forwarding";
                case 100000002: return "Solution Verification";
                case 100000008: return "Processing";
                case 1: return "Processing- Department";
                case 100000001: return "Return to Customer";
                case 100000003: return "Ticket Closure";
                case 100000005: return "Ticket Reopen";
                case 5: return "Problem Solved";
                case 1000: return "Information Provided";
                case 6: return "Cancelled";
                case 2000: return "Merged";
                case 100000007: return "Close";
                default: return "Unknown";
            }
        }
    }
}
