using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class ChangeLifecycleAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService CreateService() => new(_billingClient, _publisher, _logger);

    [Fact]
    public async Task RejectsIllegalTransitionWithoutCallingProvider()
    {
        // Resuming an active subscription is illegal.
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active });

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() =>
            service.ChangeLifecycleAsync(5, SubscriptionLifecycleAction.Resume, null));
        await _billingClient.DidNotReceive().ApplyLifecycleActionAsync(Arg.Any<int>(), Arg.Any<SubscriptionLifecycleAction>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppliesLegalTransitionAndPublishesStateChange()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active });
        _billingClient.ApplyLifecycleActionAsync(5, SubscriptionLifecycleAction.Pause, null, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.OnHold });

        var service = CreateService();
        var result = await service.ChangeLifecycleAsync(5, SubscriptionLifecycleAction.Pause, null);

        Assert.Equal(SubscriptionState.OnHold, result.State);
        await _billingClient.Received(1).ApplyLifecycleActionAsync(5, SubscriptionLifecycleAction.Pause, null, Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(Arg.Any<SubscriptionStateChanged>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAllowedFromCanceled()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Canceled });
        _billingClient.ApplyLifecycleActionAsync(5, SubscriptionLifecycleAction.Reactivate, null, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active });

        var service = CreateService();
        var result = await service.ChangeLifecycleAsync(5, SubscriptionLifecycleAction.Reactivate, null);

        Assert.Equal(SubscriptionState.Active, result.State);
    }
}
