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

// UC2/UC3/UC4 — view and manage the signed-in customer's subscriptions
// (mirrors the Orders view). Customer-facing, so [Authorize] with cookie auth.
[Authorize]
public class MineModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public MineModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyList<CustomerSubscription> Subscriptions { get; private set; } = new List<CustomerSubscription>();
    public IReadOnlyList<SubscriptionPlan> Plans { get; private set; } = new List<SubscriptionPlan>();
    public CustomerSubscription? ActiveSubscription { get; private set; }

    // Set by the preview handler so the view can render a confirmation form (UC3).
    public PlanChangePreview? Preview { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    // UC2 — record pay-as-you-go usage against the active subscription.
    public async Task<IActionResult> OnPostRecordUsageAsync(int quantity, string? memo)
    {
        await GuardedAsync(async () =>
        {
            var usage = await _subscriptionService.RecordUsageForUserAsync(GetUserReference(), quantity, memo);
            StatusMessage = usage.PeriodTotalAvailable
                ? $"Recorded {usage.RecordedQuantity} unit(s). Period-to-date total: {usage.PeriodToDateTotal}. " +
                  "This will appear on your next renewal invoice."
                : $"Recorded {usage.RecordedQuantity} unit(s). It will appear on your next renewal invoice " +
                  "(running total temporarily unavailable).";
        });

        return await ReloadOrPageAsync();
    }

    // UC4 — pause / resume / cancel / reactivate.
    public async Task<IActionResult> OnPostLifecycleAsync(string action, bool endOfPeriod, string? reason)
    {
        if (!Enum.TryParse<SubscriptionLifecycleAction>(action, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(typeof(SubscriptionLifecycleAction), parsed))
        {
            ErrorMessage = "Unknown lifecycle action.";
            await LoadAsync();
            return Page();
        }

        await GuardedAsync(async () =>
        {
            var updated = await _subscriptionService.ChangeLifecycleForUserAsync(GetUserReference(), parsed,
                endOfPeriod, reason);
            StatusMessage = $"Subscription #{updated.Id} is now '{updated.State}'" +
                            (updated.CancelAtEndOfPeriod ? " (pending cancellation at period end)." : ".");
        });

        return await ReloadOrPageAsync();
    }

    // UC3 — preview a plan change; re-renders the page with the preview shown.
    public async Task<IActionResult> OnPostPreviewAsync(string targetPlanHandle, bool applyNow)
    {
        await GuardedAsync(async () =>
        {
            Preview = await _subscriptionService.PreviewPlanChangeForUserAsync(GetUserReference(), targetPlanHandle,
                applyNow);
        });

        await LoadAsync();
        return Page();
    }

    // UC3 — commit the previewed plan change.
    public async Task<IActionResult> OnPostChangePlanAsync(string targetPlanHandle, bool applyNow,
        long confirmedAmountDueInCents)
    {
        await GuardedAsync(async () =>
        {
            var updated = await _subscriptionService.ChangePlanForUserAsync(GetUserReference(), targetPlanHandle,
                applyNow, confirmedAmountDueInCents);
            StatusMessage = applyNow
                ? $"Plan changed to {updated.PlanName ?? updated.PlanHandle}."
                : $"Plan change to '{targetPlanHandle}' scheduled for the next renewal.";
        });

        return await ReloadOrPageAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var userReference = GetUserReference();
            Subscriptions = await _subscriptionService.GetSubscriptionsForUserAsync(userReference);
            Plans = await _subscriptionService.GetAvailablePlansAsync();
            ActiveSubscription = await _subscriptionService.GetActiveSubscriptionForUserAsync(userReference);
        }
        catch (BillingConfigurationException ex)
        {
            ErrorMessage ??= ex.Message;
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage ??= ex.Message;
        }
    }

    // Runs an action, translating billing failures into a friendly page error
    // without ever throwing out of the page handler.
    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (BillingConfigurationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task<IActionResult> ReloadOrPageAsync()
    {
        if (ErrorMessage != null)
        {
            await LoadAsync();
            return Page();
        }

        return RedirectToPage();
    }

    private string GetUserReference()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        return User!.Identity!.Name!;
    }
}
