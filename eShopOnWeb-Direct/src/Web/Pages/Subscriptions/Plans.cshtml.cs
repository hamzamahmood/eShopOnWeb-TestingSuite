using System;
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

/// <summary>
/// UC1 (hero) — browse the available plans and subscribe. Storefront page, cookie-authenticated; the
/// signed-in customer is resolved via <c>User.Identity.Name</c> (the Maxio customer reference).
/// </summary>
[Authorize]
public class PlansModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public PlansModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyCollection<SubscriptionPlan> Plans { get; private set; } = Array.Empty<SubscriptionPlan>();

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGet()
    {
        await LoadPlansAsync();
    }

    public async Task<IActionResult> OnPostSubscribeAsync(string productHandle)
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));

        if (string.IsNullOrWhiteSpace(productHandle))
        {
            ErrorMessage = "Please choose a plan to subscribe to.";
            await LoadPlansAsync();
            return Page();
        }

        try
        {
            var userReference = User.Identity!.Name!;
            var subscription = await _subscriptionService.SubscribeAsync(userReference, userReference, productHandle);
            StatusMessage = $"You are subscribed to {subscription.ProductName} ({subscription.State}).";
            return RedirectToPage("./Mine");
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage = ex.Message;
            await LoadPlansAsync();
            return Page();
        }
    }

    private async Task LoadPlansAsync()
    {
        try
        {
            Plans = await _subscriptionService.ListPlansAsync();
        }
        catch (BillingProviderException ex)
        {
            // Plans cannot be listed (provider unreachable / bad credentials) — friendly error, no enrollment.
            ErrorMessage = $"Plans are currently unavailable: {ex.Message}";
        }
    }
}
