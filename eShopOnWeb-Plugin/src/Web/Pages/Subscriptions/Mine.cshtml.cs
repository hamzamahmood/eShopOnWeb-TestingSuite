using System;
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
public class MineModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAppLogger<MineModel> _logger;

    public MineModel(ISubscriptionService subscriptionService, IAppLogger<MineModel> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    // Same stable reference as OrderController/Checkout (User.Identity.Name) - not the Identity GUID
    // id, so it matches both the PublicApi (whose JWT carries only Name+Role) and OrderPlaced.BuyerId
    // (sourced from Basket.BuyerId, itself keyed by User.Identity.Name).
    private string GetUserId()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        return User.Identity.Name!;
    }

    public IReadOnlyList<SubscriptionDto> Subscriptions { get; private set; } = new List<SubscriptionDto>();

    public Dictionary<string, UsageSummaryDto> UsageSummaries { get; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public RecordUsageInput RecordUsage { get; set; } = new();

    [BindProperty]
    public PreviewPlanChangeInput PreviewPlanChange { get; set; } = new();

    [BindProperty]
    public CommitPlanChangeInput CommitPlanChange { get; set; } = new();

    [BindProperty]
    public LifecycleActionInput Lifecycle { get; set; } = new();

    [BindProperty]
    public CancelInput Cancel { get; set; } = new();

    public ProrationPreviewDto? LastPreview { get; private set; }

    public async Task OnGet()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostRecordUsageAsync()
    {
        // This page binds several forms' worth of [BindProperty] models at once (Razor Pages populates
        // and validates all of them on every POST, regardless of which handler is invoked) - so each
        // handler must validate only its own bound model via TryValidateModel, never ModelState.IsValid,
        // or an empty field from an unrelated form on the page fails this submission too.
        ModelState.Clear();
        if (!TryValidateModel(RecordUsage, nameof(RecordUsage)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            await _subscriptionService.RecordUsageAsync(RecordUsage.SubscriptionId, userId, callerIsAdmin: false, RecordUsage.Quantity, RecordUsage.Memo, requestId, HttpContext.RequestAborted);
            StatusMessage = $"Recorded {RecordUsage.Quantity} unit(s) of usage. It will appear on your next invoice.";
        }
        catch (Exception ex) when (ex is SubscriptionNotFoundException or MeteredComponentMisconfiguredException or BillingProviderException)
        {
            _logger.LogWarning("Record usage failed for user {UserId}: {ErrorMessage}", userId, ex.Message);
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPreviewPlanChangeAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(PreviewPlanChange, nameof(PreviewPlanChange)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        try
        {
            var timing = Enum.Parse<PlanChangeTiming>(PreviewPlanChange.Timing);
            LastPreview = await _subscriptionService.PreviewPlanChangeAsync(PreviewPlanChange.SubscriptionId, userId, callerIsAdmin: false, PreviewPlanChange.TargetProductHandle, timing, HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is SubscriptionNotFoundException or BillingProviderException)
        {
            _logger.LogWarning("Preview plan change failed for user {UserId}: {ErrorMessage}", userId, ex.Message);
            StatusMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCommitPlanChangeAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(CommitPlanChange, nameof(CommitPlanChange)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        try
        {
            await _subscriptionService.CommitPlanChangeAsync(CommitPlanChange.SubscriptionId, userId, callerIsAdmin: false, CommitPlanChange.PreviewToken, HttpContext.RequestAborted);
            StatusMessage = "Your plan change has been applied.";
        }
        catch (Exception ex) when (ex is SubscriptionNotFoundException or StalePreviewException or PaymentVerificationRequiredException or BillingProviderException)
        {
            _logger.LogWarning("Commit plan change failed for user {UserId}: {ErrorMessage}", userId, ex.Message);
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPauseAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Lifecycle, nameof(Lifecycle)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        await RunLifecycleActionAsync(userId, () => _subscriptionService.PauseAsync(Lifecycle.SubscriptionId, userId, false, HttpContext.RequestAborted), "paused");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResumeAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Lifecycle, nameof(Lifecycle)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        await RunLifecycleActionAsync(userId, () => _subscriptionService.ResumeAsync(Lifecycle.SubscriptionId, userId, false, HttpContext.RequestAborted), "resumed");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReactivateAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Lifecycle, nameof(Lifecycle)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        await RunLifecycleActionAsync(userId, () => _subscriptionService.ReactivateAsync(Lifecycle.SubscriptionId, userId, false, HttpContext.RequestAborted), "reactivated");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Cancel, nameof(Cancel)))
        {
            await LoadAsync();
            return Page();
        }

        var userId = GetUserId();
        var timing = Enum.Parse<CancelTiming>(Cancel.Timing);
        await RunLifecycleActionAsync(userId, () => _subscriptionService.CancelAsync(Cancel.SubscriptionId, userId, false, timing, Cancel.Reason, HttpContext.RequestAborted), "canceled");
        return RedirectToPage();
    }

    private async Task RunLifecycleActionAsync(string userId, Func<Task<SubscriptionDto>> action, string pastTenseVerb)
    {
        try
        {
            await action();
            StatusMessage = $"Subscription {pastTenseVerb}.";
        }
        catch (Exception ex) when (ex is SubscriptionNotFoundException or IllegalSubscriptionTransitionException or BillingProviderException)
        {
            _logger.LogWarning("Lifecycle action failed for user {UserId}: {ErrorMessage}", userId, ex.Message);
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadAsync()
    {
        var userId = GetUserId();
        Subscriptions = await _subscriptionService.ListMySubscriptionsAsync(userId, HttpContext.RequestAborted);

        foreach (var subscription in Subscriptions)
        {
            try
            {
                var summary = await _subscriptionService.GetUsageSummaryAsync(subscription.SubscriptionId, userId, callerIsAdmin: false, HttpContext.RequestAborted);
                UsageSummaries[subscription.SubscriptionId] = summary;
            }
            catch (Exception ex) when (ex is MeteredComponentMisconfiguredException or BillingProviderException)
            {
                _logger.LogWarning("Could not load usage summary for subscription {SubscriptionId}: {ErrorMessage}", subscription.SubscriptionId, ex.Message);
            }
        }
    }
}
