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
/// The customer's subscription management surface (UC2 usage, UC3 plan change with proration preview,
/// UC4 lifecycle). Cookie-authenticated; all actions operate on the signed-in customer's own subscriptions.
/// </summary>
[Authorize]
public class MineModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public MineModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyCollection<CustomerSubscription> Subscriptions { get; private set; } = Array.Empty<CustomerSubscription>();
    public IReadOnlyCollection<SubscriptionPlan> Plans { get; private set; } = Array.Empty<SubscriptionPlan>();

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    // Populated when a plan-change preview has been requested, so the view can show the amounts and a
    // confirm form (UC3 preview -> confirm).
    public ProrationPreview? Preview { get; private set; }
    public int PreviewSubscriptionId { get; private set; }
    public string PreviewTargetHandle { get; private set; } = string.Empty;
    public PlanChangeTiming PreviewTiming { get; private set; }

    public async Task OnGet()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostRecordUsageAsync(int subscriptionId, int quantity, string? memo)
    {
        var userReference = GetUserReference();
        try
        {
            var result = await _subscriptionService.RecordUsageAsync(subscriptionId, quantity, memo, userReference);
            StatusMessage = result.PeriodToDateTotal is int total
                ? $"Recorded {result.RecordedQuantity} unit(s). Period-to-date total: {total} (billed on your next invoice)."
                : $"Recorded {result.RecordedQuantity} unit(s) (billed on your next invoice). Running total is currently unavailable.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is BillingProviderException or ArgumentException)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPreviewPlanChangeAsync(int subscriptionId, string targetProductHandle, string timing)
    {
        _ = GetUserReference();
        var parsedTiming = ParseTiming(timing);
        try
        {
            Preview = await _subscriptionService.PreviewPlanChangeAsync(subscriptionId, targetProductHandle, parsedTiming);
            PreviewSubscriptionId = subscriptionId;
            PreviewTargetHandle = targetProductHandle;
            PreviewTiming = parsedTiming;
        }
        catch (Exception ex) when (ex is BillingProviderException or ArgumentException)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostChangePlanAsync(int subscriptionId, string targetProductHandle, string timing,
        int proratedAdjustmentInCents, int chargeInCents, int paymentDueInCents, int creditAppliedInCents)
    {
        _ = GetUserReference();
        var parsedTiming = ParseTiming(timing);
        var confirmedPreview = new ProrationPreview
        {
            TargetProductHandle = targetProductHandle,
            Timing = parsedTiming,
            ProratedAdjustmentInCents = proratedAdjustmentInCents,
            ChargeInCents = chargeInCents,
            PaymentDueInCents = paymentDueInCents,
            CreditAppliedInCents = creditAppliedInCents
        };

        try
        {
            var updated = await _subscriptionService.ChangePlanAsync(subscriptionId, targetProductHandle, parsedTiming, confirmedPreview);
            StatusMessage = parsedTiming == PlanChangeTiming.AtRenewal
                ? $"Plan change to {updated.ProductName} scheduled for your next renewal."
                : $"Plan changed to {updated.ProductName}.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is BillingProviderException or ArgumentException)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLifecycleAsync(int subscriptionId, string action, string? reason)
    {
        _ = GetUserReference();
        if (!Enum.TryParse<SubscriptionLifecycleAction>(action, ignoreCase: true, out var parsedAction))
        {
            ErrorMessage = $"Unknown action '{action}'.";
            await LoadAsync();
            return Page();
        }

        try
        {
            var updated = await _subscriptionService.ChangeLifecycleAsync(subscriptionId, parsedAction, reason);
            StatusMessage = $"Subscription {updated.Id} is now {updated.State}.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is BillingProviderException or ArgumentException)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    private string GetUserReference()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        return User.Identity!.Name!;
    }

    private async Task LoadAsync()
    {
        var userReference = GetUserReference();
        try
        {
            Subscriptions = await _subscriptionService.GetMySubscriptionsAsync(userReference);
            Plans = await _subscriptionService.ListPlansAsync();
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage ??= $"Your subscriptions are currently unavailable: {ex.Message}";
        }
    }

    private static PlanChangeTiming ParseTiming(string? timing) =>
        Enum.TryParse<PlanChangeTiming>(timing, ignoreCase: true, out var parsed) ? parsed : PlanChangeTiming.Immediate;
}
