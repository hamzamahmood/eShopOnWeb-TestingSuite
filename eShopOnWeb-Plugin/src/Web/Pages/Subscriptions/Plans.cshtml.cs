using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.Web.Pages.Subscriptions;

[Authorize]
public class PlansModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAppLogger<PlansModel> _logger;

    public PlansModel(ISubscriptionService subscriptionService, IAppLogger<PlansModel> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public IReadOnlyList<PlanDto> Plans { get; private set; } = new List<PlanDto>();

    [BindProperty]
    public SubscribeInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGet()
    {
        Plans = await _subscriptionService.ListPlansAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostSubscribeAsync()
    {
        if (!ModelState.IsValid)
        {
            Plans = await _subscriptionService.ListPlansAsync(HttpContext.RequestAborted);
            return Page();
        }

        // eShopOnWeb identifies the signed-in user by User.Identity.Name (email), the same stable
        // reference OrderController/Checkout already use - not the Identity GUID id, which the
        // PublicApi's JWT never carries (see IdentityTokenClaimService), so both hosts must agree on
        // the same reference for a user's Maxio customer record to be found consistently.
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        var userId = User.Identity.Name!;

        try
        {
            await _subscriptionService.SubscribeAsync(userId, userId, null, null, Input.ProductHandle, HttpContext.RequestAborted);
            StatusMessage = "You're subscribed! Your new plan is shown below.";
            return RedirectToPage("Mine");
        }
        catch (PaymentVerificationRequiredException ex)
        {
            StatusMessage = "Additional payment information is required: " + string.Join(" ", ex.ProviderMessages);
        }
        catch (BillingProviderException ex)
        {
            _logger.LogWarning("Subscribe failed for user {UserId}: {ErrorMessage}", userId, ex.Message);
            StatusMessage = ex.Message;
        }

        Plans = await _subscriptionService.ListPlansAsync(HttpContext.RequestAborted);
        return Page();
    }
}
