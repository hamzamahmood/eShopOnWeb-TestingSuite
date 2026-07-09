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

public class ChangePlanAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService CreateService() => new(_billingClient, _publisher, _logger);

    [Fact]
    public async Task PreviewRejectsChangeToSamePlan()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active, ProductHandle = "eshop-pro" });

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() =>
            service.PreviewPlanChangeAsync(5, "eshop-pro", PlanChangeTiming.Immediate));
    }

    [Fact]
    public async Task PreviewRejectsWhenSubscriptionCanceled()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Canceled, ProductHandle = "eshop-pro" });

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() =>
            service.PreviewPlanChangeAsync(5, "basic-plan", PlanChangeTiming.Immediate));
    }

    [Fact]
    public async Task CommitRejectsStalePreview()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active, ProductHandle = "eshop-pro" });
        // Fresh preview differs from the confirmed one → stale.
        _billingClient.PreviewPlanChangeAsync(5, "basic-plan", PlanChangeTiming.Immediate, Arg.Any<CancellationToken>())
            .Returns(new ProrationPreview { ChargeInCents = 500 });

        var confirmed = new ProrationPreview { ChargeInCents = 100 };
        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() =>
            service.ChangePlanAsync(5, "basic-plan", PlanChangeTiming.Immediate, confirmed));
        await _billingClient.DidNotReceive().ChangePlanAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PlanChangeTiming>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitAppliesAndPublishesWhenPreviewMatches()
    {
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Active, ProductHandle = "eshop-pro" });
        var preview = new ProrationPreview { ChargeInCents = 100, PaymentDueInCents = 100 };
        _billingClient.PreviewPlanChangeAsync(5, "basic-plan", PlanChangeTiming.Immediate, Arg.Any<CancellationToken>())
            .Returns(preview);
        var updated = new CustomerSubscription { Id = 5, State = SubscriptionState.Active, ProductHandle = "basic-plan" };
        _billingClient.ChangePlanAsync(5, "basic-plan", PlanChangeTiming.Immediate, Arg.Any<CancellationToken>())
            .Returns(updated);

        var confirmed = new ProrationPreview { ChargeInCents = 100, PaymentDueInCents = 100 };
        var service = CreateService();
        var result = await service.ChangePlanAsync(5, "basic-plan", PlanChangeTiming.Immediate, confirmed);

        Assert.Equal("basic-plan", result.ProductHandle);
        await _billingClient.Received(1).ChangePlanAsync(5, "basic-plan", PlanChangeTiming.Immediate, Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(Arg.Any<SubscriptionPlanChanged>(), Arg.Any<CancellationToken>());
    }
}
