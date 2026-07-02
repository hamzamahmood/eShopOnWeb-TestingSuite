using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class PauseAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    [Fact]
    public async System.Threading.Tasks.Task PausesActiveSubscriptionAndPublishesStateChanged()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));
        _billingClient.PauseAsync("999", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.OnHold, 299m, null));

        var sut = BuildSut();
        var result = await sut.PauseAsync("999", "buyer@test.com", callerIsAdmin: false, default);

        Assert.Equal(SubscriptionState.OnHold, result.State);
        await _publisher.Received(1).Publish(
            Arg.Is<SubscriptionStateChanged>(e => e.OldState == SubscriptionState.Active && e.NewState == SubscriptionState.OnHold),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task ThrowsIllegalTransitionWhenSubscriptionIsNotActive()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.OnHold, 299m, null));

        var sut = BuildSut();
        await Assert.ThrowsAsync<IllegalSubscriptionTransitionException>(
            () => sut.PauseAsync("999", "buyer@test.com", callerIsAdmin: false, default));

        await _billingClient.DidNotReceive().PauseAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>());
    }
}
