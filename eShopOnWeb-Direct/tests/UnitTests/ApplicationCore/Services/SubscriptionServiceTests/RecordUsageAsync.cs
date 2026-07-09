using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class RecordUsageAsync
{
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService CreateService() => new(_billingClient, _publisher, _logger);

    private void SetupMeteredComponent() =>
        _billingClient.GetMeteredComponentAsync(Arg.Any<CancellationToken>())
            .Returns(new BillingComponent { Id = 3033795, Handle = "api-call", Kind = BillingComponentKind.Metered });

    private void SetupActiveSubscription(int id = 5, int customerId = 42) =>
        _billingClient.GetSubscriptionAsync(id, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = id, State = SubscriptionState.Active, CustomerId = customerId });

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RejectsNonPositiveQuantityWithoutCallingProvider(int quantity)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordUsageAsync(5, quantity, null, null));

        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefusesWhenConfiguredComponentIsNotMetered()
    {
        _billingClient.GetMeteredComponentAsync(Arg.Any<CancellationToken>())
            .Returns(new BillingComponent { Id = 1, Handle = "api-call", Kind = BillingComponentKind.QuantityBased });

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() => service.RecordUsageAsync(5, 1, null, null));
        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsWhenSubscriptionNotActive()
    {
        SetupMeteredComponent();
        _billingClient.GetSubscriptionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new CustomerSubscription { Id = 5, State = SubscriptionState.Canceled });

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() => service.RecordUsageAsync(5, 1, null, null));
        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsWhenSubscriptionBelongsToAnotherCustomer()
    {
        SetupMeteredComponent();
        SetupActiveSubscription(id: 5, customerId: 42);
        _billingClient.FindCustomerIdByReferenceAsync("someone-else", Arg.Any<CancellationToken>()).Returns(99);

        var service = CreateService();

        await Assert.ThrowsAsync<BillingProviderException>(() => service.RecordUsageAsync(5, 1, null, "someone-else"));
        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordsUsageAndReturnsPeriodToDateTotal()
    {
        SetupMeteredComponent();
        SetupActiveSubscription();
        _billingClient.GetUsageTotalAsync(5, Arg.Any<CancellationToken>()).Returns(12);

        var service = CreateService();
        var result = await service.RecordUsageAsync(5, 3, "memo", null);

        Assert.Equal(3, result.RecordedQuantity);
        Assert.Equal(12, result.PeriodToDateTotal);
        await _billingClient.Received(1).RecordUsageAsync(5, 3, "memo", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullTotalWhenReadBackFailsButUsageStands()
    {
        SetupMeteredComponent();
        SetupActiveSubscription();
        _billingClient.GetUsageTotalAsync(5, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new BillingProviderException("read-back failed"));

        var service = CreateService();
        var result = await service.RecordUsageAsync(5, 1, null, null);

        Assert.Equal(1, result.RecordedQuantity);
        Assert.Null(result.PeriodToDateTotal);
        await _billingClient.Received(1).RecordUsageAsync(5, 1, null, Arg.Any<CancellationToken>());
    }
}
