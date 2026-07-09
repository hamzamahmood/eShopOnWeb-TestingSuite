using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Web.Pages.Subscriptions;

// UC1 — browse plans and subscribe (mirrors Pages/Basket/Index). Customer-facing,
// so [Authorize] with cookie auth (plan §2.4).
[Authorize]
public class PlansModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public PlansModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyList<SubscriptionPlan> Plans { get; private set; } = new List<SubscriptionPlan>();
    public CustomerSubscription? ActiveSubscription { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync(string planHandle)
    {
        if (string.IsNullOrWhiteSpace(planHandle))
        {
            await LoadAsync();
            ErrorMessage = "Please choose a plan to subscribe to.";
            return Page();
        }

        var userReference = GetUserReference();
        try
        {
            var subscription = await _subscriptionService.SubscribeAsync(userReference, planHandle);
            StatusMessage =
                $"You are subscribed to {subscription.PlanName ?? subscription.PlanHandle} (subscription #{subscription.Id}).";
            return RedirectToPage("/Subscriptions/Mine");
        }
        catch (BillingConfigurationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var userReference = GetUserReference();
        try
        {
            Plans = await _subscriptionService.GetAvailablePlansAsync();
            ActiveSubscription = await _subscriptionService.GetActiveSubscriptionForUserAsync(userReference);
        }
        catch (BillingConfigurationException ex)
        {
            ErrorMessage ??= ex.Message;
        }
        catch (BillingProviderException ex)
        {
            // Friendly error on the Plans page; no enrollment is attempted (UC1 failure path).
            ErrorMessage ??= ex.Message;
        }
    }

    private string GetUserReference()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        return User!.Identity!.Name!;
    }
}
