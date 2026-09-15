using AccountingLeads.Api.Contracts;
using AccountingLeads.Application.Leads.CreateLead;
using AccountingLeads.Application.Leads.GetLeadDetail;
using AccountingLeads.Application.Leads.GetLeads;
using AccountingLeads.Domain.Leads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AccountingLeads.Api.Controllers;

[ApiController]
[Route("api/leads")]
public class LeadsController : ControllerBase
{
    private readonly CreateLeadCommandHandler _createLeadCommandHandler;
    private readonly GetLeadsQueryHandler _getLeadsQueryHandler;
    private readonly GetLeadDetailQueryHandler _getLeadDetailQueryHandler;

    public LeadsController(
        CreateLeadCommandHandler createLeadCommandHandler,
        GetLeadsQueryHandler getLeadsQueryHandler,
        GetLeadDetailQueryHandler getLeadDetailQueryHandler)
    {
        _createLeadCommandHandler = createLeadCommandHandler;
        _getLeadsQueryHandler = getLeadsQueryHandler;
        _getLeadDetailQueryHandler = getLeadDetailQueryHandler;
    }

    [HttpGet]
    [Authorize]
    [RequiredScope("access_as_user")]
    [ProducesResponseType(typeof(PagedResponse<LeadListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeads(
        [FromQuery] LeadStatus? status,
        [FromQuery] LeadPriority? priority,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _getLeadsQueryHandler.HandleAsync(
            new GetLeadsQuery(status, priority, search, page, pageSize, sortBy, sortDescending), cancellationToken);
        var items = result.Items.Select(lead => new LeadListItemResponse(
            lead.Id, lead.Name, lead.CompanyName, lead.LeadStatus, lead.AiProcessingStatus, lead.Priority, lead.CreatedAtUtc)).ToList();

        return Ok(new PagedResponse<LeadListItemResponse>(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [RequiredScope("access_as_user")]
    [ProducesResponseType(typeof(LeadDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeadDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getLeadDetailQueryHandler.HandleAsync(new GetLeadDetailQuery(id), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var response = new LeadDetailResponse(
            result.Id,
            result.Name,
            result.Email,
            result.Phone,
            result.CompanyName,
            result.ClientType,
            result.ApproximateAnnualRevenue,
            result.AccountingSoftware,
            result.Inquiry,
            result.LeadStatus,
            result.AiProcessingStatus,
            result.CreatedAtUtc,
            result.Analysis is null
                ? null
                : new LeadAnalysisResponse(
                    result.Analysis.RequestedServices,
                    result.Analysis.ExtractedClientType,
                    result.Analysis.ExtractedApproximateAnnualRevenue,
                    result.Analysis.ExtractedAccountingSoftware,
                    result.Analysis.Urgency,
                    result.Analysis.ExtractedFilingDeadline,
                    result.Analysis.Notes,
                    result.Analysis.EstimatedDeadlineInDays,
                    result.Analysis.BookkeepingMonthsBehind,
                    result.Analysis.CreatedAtUtc),
            result.Qualification is null
                ? null
                : new LeadQualificationResponse(
                    result.Qualification.Score,
                    result.Qualification.Priority,
                    result.Qualification.Reasons,
                    result.Qualification.RulesVersionApplied,
                    result.Qualification.CreatedAtUtc));

        return Ok(response);
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateLeadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateLeadCommand(
            request.Name,
            request.Email,
            request.Phone,
            request.CompanyName,
            request.ClientType,
            request.ApproximateAnnualRevenue,
            request.AccountingSoftware,
            request.Inquiry);

        var result = await _createLeadCommandHandler.HandleAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            foreach (var (field, errors) in result.ValidationErrors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(field, error);
                }
            }

            return ValidationProblem(ModelState);
        }

        var response = new CreateLeadResponse(result.LeadId!.Value, LeadStatus.New, AiProcessingStatus.Pending);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
