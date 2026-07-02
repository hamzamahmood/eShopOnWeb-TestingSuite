using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class PreviewAndCommitPlanChangeAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    private static SubscriptionDto OwnedSubscription() =>
        new("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null);

    [Fact]
    public async System.Threading.Tasks.Task PreviewStoresAndReturnsAPreviewToken()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription());
        _billingClient.PreviewPlanChangeAsync("999", "basic-plan", PlanChangeTiming.Immediate, Arg.Any<System.Threading.CancellationToken>())
            .Returns(new PlanChangeQuoteDto("999", "eshop-pro", "basic-plan", PlanChangeTiming.Immediate, -50m, System.DateTimeOffset.UtcNow));
        _idempotencyCache.StorePreview(Arg.Any<ProrationPreviewDto>()).Returns("token-123");

        var sut = BuildSut();
        var preview = await sut.PreviewPlanChangeAsync("999", "buyer@test.com", callerIsAdmin: false, "basic-plan", PlanChangeTiming.Immediate, default);

        Assert.Equal("token-123", preview.PreviewToken);
        Assert.Equal(-50m, preview.ProratedAmount);
    }

    [Fact]
    public async System.Threading.Tasks.Task CommitThrowsStalePreviewWhenTokenIsUnknown()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription());
        _idempotencyCache.TakePreview("missing-token").Returns((ProrationPreviewDto?)null);

        var sut = BuildSut();
        await Assert.ThrowsAsync<StalePreviewException>(
            () => sut.CommitPlanChangeAsync("999", "buyer@test.com", callerIsAdmin: false, "missing-token", default));

        await _billingClient.DidNotReceive().CommitPlanChangeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PlanChangeTiming>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task CommitThrowsStalePreviewWhenTheAmountHasChangedSincePreview()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription());
        var cachedPreview = new ProrationPreviewDto("999", "eshop-pro", "basic-plan", PlanChangeTiming.Immediate, -50m, System.DateTimeOffset.UtcNow, "token-123");
        _idempotencyCache.TakePreview("token-123").Returns(cachedPreview);
        // The provider now quotes a different amount than what was previewed (e.g. the billing period boundary was crossed).
        _billingClient.PreviewPlanChangeAsync("999", "basic-plan", PlanChangeTiming.Immediate, Arg.Any<System.Threading.CancellationToken>())
            .Returns(new PlanChangeQuoteDto("999", "eshop-pro", "basic-plan", PlanChangeTiming.Immediate, -40m, System.DateTimeOffset.UtcNow));

        var sut = BuildSut();
        await Assert.ThrowsAsync<StalePreviewException>(
            () => sut.CommitPlanChangeAsync("999", "buyer@test.com", callerIsAdmin: false, "token-123", default));

        await _billingClient.DidNotReceive().CommitPlanChangeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PlanChangeTiming>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task CommitSucceedsWhenTheFreshQuoteMatchesThePreview()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription());
        var cachedPreview = new ProrationPreviewDto("999", "eshop-pro", "basic-plan", PlanChangeTiming.Immediate, -50m, System.DateTimeOffset.UtcNow, "token-123");
        _idempotencyCache.TakePreview("token-123").Returns(cachedPreview);
        _billingClient.PreviewPlanChangeAsync("999", "basic-plan", PlanChangeTiming.Immediate, Arg.Any<System.Threading.CancellationToken>())
            .Returns(new PlanChangeQuoteDto("999", "eshop-pro", "basic-plan", PlanChangeTiming.Immediate, -50m, System.DateTimeOffset.UtcNow));
        _billingClient.CommitPlanChangeAsync("999", "basic-plan", PlanChangeTiming.Immediate, Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "basic-plan", "Basic Plan", 29m, SubscriptionState.Active, 29m, null));

        var sut = BuildSut();
        var result = await sut.CommitPlanChangeAsync("999", "buyer@test.com", callerIsAdmin: false, "token-123", default);

        Assert.Equal("basic-plan", result.ProductHandle);
    }
}
