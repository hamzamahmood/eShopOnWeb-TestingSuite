using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class ListMySubscriptionsAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIdempotencyCache _idempotencyCache = Substitute.For<IIdempotencyCache>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildSut() => new(_billingClient, _publisher, _idempotencyCache, _logger);

    [Fact]
    public async System.Threading.Tasks.Task ReturnsEmptyWithoutCallingListWhenNoCustomerExists()
    {
        _billingClient.FindCustomerIdAsync("nobody@test.com", Arg.Any<System.Threading.CancellationToken>())
            .Returns((string?)null);

        var sut = BuildSut();
        var result = await sut.ListMySubscriptionsAsync("nobody@test.com", default);

        Assert.Empty(result);
        await _billingClient.DidNotReceive().ListCustomerSubscriptionsAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>());
    }
}
