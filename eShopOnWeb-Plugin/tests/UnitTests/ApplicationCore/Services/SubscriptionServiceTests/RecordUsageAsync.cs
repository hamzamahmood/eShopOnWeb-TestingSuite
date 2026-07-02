using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class RecordUsageAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    private static SubscriptionDto OwnedSubscription(string owner) =>
        new("999", owner, "eshop-pro", "Pro Plan", 299m, SubscriptionState.Active, 299m, null);

    [Fact]
    public async System.Threading.Tasks.Task VerifiesComponentAndRecordsUsageForOwner()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription("buyer@test.com"));
        _idempotencyCache.TryClaim(Arg.Any<string>()).Returns(true);
        _billingClient.RecordUsageAsync("999", 1m, "memo", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new UsageDto("u1", 1m, "memo", null));

        var sut = BuildSut();
        var result = await sut.RecordUsageAsync("999", "buyer@test.com", callerIsAdmin: false, 1m, "memo", "req-1", default);

        Assert.Equal("u1", result.UsageId);
        await _billingClient.Received(1).VerifyMeteredComponentAsync(Arg.Any<System.Threading.CancellationToken>());
        await _billingClient.Received(1).RecordUsageAsync("999", 1m, "memo", Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task ThrowsNotFoundWhenCallerDoesNotOwnSubscriptionAndIsNotAdmin()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription("someone-else@test.com"));

        var sut = BuildSut();
        await Assert.ThrowsAsync<SubscriptionNotFoundException>(
            () => sut.RecordUsageAsync("999", "buyer@test.com", callerIsAdmin: false, 1m, null, "req-1", default));

        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task AdminCanRecordUsageOnAnyoneElsesSubscription()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription("someone-else@test.com"));
        _idempotencyCache.TryClaim(Arg.Any<string>()).Returns(true);
        _billingClient.RecordUsageAsync("999", 1m, null, Arg.Any<System.Threading.CancellationToken>())
            .Returns(new UsageDto("u1", 1m, null, null));

        var sut = BuildSut();
        var result = await sut.RecordUsageAsync("999", "admin@test.com", callerIsAdmin: true, 1m, null, "req-1", default);

        Assert.Equal("u1", result.UsageId);
    }

    [Fact]
    public async System.Threading.Tasks.Task DuplicateRequestIdIsIgnoredWithoutCallingBillingProvider()
    {
        _billingClient.ReadSubscriptionAsync("999", Arg.Any<System.Threading.CancellationToken>()).Returns(OwnedSubscription("buyer@test.com"));
        _idempotencyCache.TryClaim(Arg.Any<string>()).Returns(false);

        var sut = BuildSut();
        var result = await sut.RecordUsageAsync("999", "buyer@test.com", callerIsAdmin: false, 1m, null, "req-1", default);

        Assert.Equal(0m, result.Quantity);
        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>());
    }
}
