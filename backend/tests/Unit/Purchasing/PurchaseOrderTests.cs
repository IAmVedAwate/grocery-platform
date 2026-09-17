using Domain.Purchasing;
using Xunit;

namespace Unit.Purchasing;

public class PurchaseOrderTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    private static PurchaseOrder CreateOrder(int quantity = 10) =>
        new(StoreId, SupplierId, [(ProductId, quantity, 5.00m)]);

    [Fact]
    public void Approve_WithoutSubmitFirst_ThrowsInvalidOperationException()
    {
        var order = CreateOrder();
        Assert.Throws<InvalidOperationException>(() => order.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Receive_WithoutApprovalFirst_ThrowsInvalidOperationException()
    {
        var order = CreateOrder();
        order.Submit();

        Assert.Throws<InvalidOperationException>(() =>
            order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 5 }));
    }

    [Fact]
    public void FullSubmitApproveReceive_TransitionsToReceived()
    {
        var order = CreateOrder(quantity: 10);
        order.Submit();
        order.Approve(Guid.NewGuid());

        order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 10 });

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.True(order.Items.Single().IsFullyReceived);
    }

    [Fact]
    public void PartialReceive_TransitionsToPartiallyReceived_NotReceived()
    {
        var order = CreateOrder(quantity: 10);
        order.Submit();
        order.Approve(Guid.NewGuid());

        order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 4 });

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(4, order.Items.Single().QuantityReceived);

        order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 6 });
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
    }

    [Fact]
    public void Receive_MoreThanOrdered_ThrowsInvalidOperationException()
    {
        var order = CreateOrder(quantity: 5);
        order.Submit();
        order.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 6 }));
    }

    [Fact]
    public void Cancel_AfterReceivingStarted_ThrowsInvalidOperationException()
    {
        var order = CreateOrder(quantity: 10);
        order.Submit();
        order.Approve(Guid.NewGuid());
        order.ReceiveItems(new Dictionary<Guid, int> { [ProductId] = 1 });

        Assert.Throws<InvalidOperationException>(order.Cancel);
    }

    [Fact]
    public void Cancel_WhileDraft_Succeeds()
    {
        var order = CreateOrder();
        order.Cancel();
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
    }
}
