using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class CancelAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    [Fact]
    public async System.Threading.Tasks.Task ThrowsIllegalTransitionWhenAlreadyCanceled()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Canceled, 0m, null));

        var sut = BuildSut();
        await Assert.ThrowsAsync<IllegalSubscriptionTransitionException>(
            () => sut.CancelAsync("999", "buyer@test.com", callerIsAdmin: false, CancelTiming.Immediate, null, default));

        await _billingClient.DidNotReceive().CancelAsync(Arg.Any<string>(), Arg.Any<CancelTiming>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelsActiveSubscriptionWithGivenTimingAndReason()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));
        _billingClient.CancelAsync("999", CancelTiming.EndOfPeriod, "no longer needed", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));

        var sut = BuildSut();
        await sut.CancelAsync("999", "buyer@test.com", callerIsAdmin: false, CancelTiming.EndOfPeriod, "no longer needed", default);

        await _billingClient.Received(1).CancelAsync("999", CancelTiming.EndOfPeriod, "no longer needed", Arg.Any<System.Threading.CancellationToken>());
    }
}
