using System.Collections.Generic;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class RecordUsageForUserIfSubscribedAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    [Fact]
    public async System.Threading.Tasks.Task NoOpWhenUserHasNoBillingCustomer()
    {
        _billingClient.FindCustomerIdAsync("nobody@test.com", Arg.Any<System.Threading.CancellationToken>()).Returns((string?)null);

        var sut = BuildSut();
        await sut.RecordUsageForUserIfSubscribedAsync("nobody@test.com", 1m, "memo", "req-1", default);

        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task NoOpWhenUserHasNoActiveSubscription()
    {
        _billingClient.FindCustomerIdAsync("buyer@test.com", Arg.Any<System.Threading.CancellationToken>()).Returns("12345");
        _billingClient.ListCustomerSubscriptionsAsync("12345", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<SubscriptionDto>
            {
                new("1", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Canceled, 0m, null)
            });

        var sut = BuildSut();
        await sut.RecordUsageForUserIfSubscribedAsync("buyer@test.com", 1m, "memo", "req-1", default);

        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task RecordsUsageOnTheActiveSubscriptionWhenOneExists()
    {
        _billingClient.FindCustomerIdAsync("buyer@test.com", Arg.Any<System.Threading.CancellationToken>()).Returns("12345");
        _billingClient.ListCustomerSubscriptionsAsync("12345", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<SubscriptionDto>
            {
                new("1", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null)
            });
        _billingClient.ReadSubscriptionAsync("1", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new SubscriptionDto("1", "buyer@test.com", "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null));
        _idempotencyCache.TryClaim(Arg.Any<string>()).Returns(true);

        var sut = BuildSut();
        await sut.RecordUsageForUserIfSubscribedAsync("buyer@test.com", 1m, "memo", "req-1", default);

        await _billingClient.Received(1).RecordUsageAsync("1", 1m, "memo", Arg.Any<System.Threading.CancellationToken>());
    }
}
