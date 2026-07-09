using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class SubscribeAsync
{
    private const string UserRef = "demouser@microsoft.com";
    private const string Plan = "eshop-pro";
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService CreateService() => new(_billingClient, _publisher, _logger);

    [Fact]
    public async Task EnsuresCustomerThenCreatesSubscriptionAndPublishes()
    {
        _billingClient.EnsureCustomerAsync(UserRef, UserRef, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(42);
        _billingClient.GetSubscriptionsForCustomerAsync(42, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription[0]);
        var created = new CustomerSubscription { Id = 7, State = SubscriptionState.Active, ProductHandle = Plan };
        _billingClient.CreateSubscriptionAsync(42, Plan, Arg.Any<CancellationToken>()).Returns(created);

        var service = CreateService();
        var result = await service.SubscribeAsync(UserRef, UserRef, Plan);

        Assert.Equal(7, result.Id);
        await _billingClient.Received(1).CreateSubscriptionAsync(42, Plan, Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(Arg.Any<SubscriptionActivated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsExistingActiveSubscriptionWithoutCreatingASecondOne()
    {
        var existing = new CustomerSubscription { Id = 99, State = SubscriptionState.Active, ProductHandle = Plan };
        _billingClient.EnsureCustomerAsync(UserRef, UserRef, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(42);
        _billingClient.GetSubscriptionsForCustomerAsync(42, Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        var service = CreateService();
        var result = await service.SubscribeAsync(UserRef, UserRef, Plan);

        Assert.Equal(99, result.Id);
        await _billingClient.DidNotReceive().CreateSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().Publish(Arg.Any<SubscriptionActivated>(), Arg.Any<CancellationToken>());
    }
}
