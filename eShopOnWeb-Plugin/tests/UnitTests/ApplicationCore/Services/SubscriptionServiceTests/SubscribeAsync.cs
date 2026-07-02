using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class SubscribeAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    [Fact]
    public async System.Threading.Tasks.Task FindsOrCreatesCustomerThenCreatesSubscription()
    {
        _billingClient.FindOrCreateCustomerAsync("buyer@test.com", "buyer@test.com", null, null, Arg.Any<System.Threading.CancellationToken>())
            .Returns("12345");
        var subscription = new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null);
        _billingClient.CreateSubscriptionAsync("12345", "eshop-pro", Arg.Any<System.Threading.CancellationToken>())
            .Returns(subscription);

        var sut = BuildSut();
        var result = await sut.SubscribeAsync("buyer@test.com", "buyer@test.com", null, null, "eshop-pro", default);

        Assert.Equal("999", result.SubscriptionId);
        await _billingClient.Received(1).FindOrCreateCustomerAsync("buyer@test.com", "buyer@test.com", null, null, Arg.Any<System.Threading.CancellationToken>());
        await _billingClient.Received(1).CreateSubscriptionAsync("12345", "eshop-pro", Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishesSubscriptionActivated()
    {
        _billingClient.FindOrCreateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns("12345");
        var subscription = new SubscriptionDto("999", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null);
        _billingClient.CreateSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(subscription);

        var sut = BuildSut();
        await sut.SubscribeAsync("buyer@test.com", "buyer@test.com", null, null, "eshop-pro", default);

        await _publisher.Received(1).Publish(
            Arg.Is<SubscriptionActivated>(e => e.CustomerReference == "buyer@test.com" && e.SubscriptionId == "999" && e.ProductHandle == "eshop-pro"),
            Arg.Any<System.Threading.CancellationToken>());
    }
}
