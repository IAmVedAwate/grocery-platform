using Domain.Sales;
using Xunit;

namespace Unit.Sales;

public class SalesOrderTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesOrder CreateOrder(int quantity = 3, decimal unitPrice = 100m) =>
        new(StoreId, null, UserId, Guid.NewGuid().ToString(), [(ProductId, quantity, unitPrice, 5m, 0m)]);

    [Fact]
    public void Constructor_ComputesTotals_FromLines()
    {
        var order = CreateOrder(quantity: 2, unitPrice: 50m);

        Assert.Equal(100m, order.SubtotalAmount);
        Assert.Equal(5m, order.TaxAmount);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(105m, order.TotalAmount);
    }

    [Fact]
    public void CapturePayment_Twice_ThrowsInvalidOperationException()
    {
        var order = CreateOrder();
        order.CapturePayment(PaymentMethod.Cash);

        Assert.Throws<InvalidOperationException>(() => order.CapturePayment(PaymentMethod.Card));
    }

    [Fact]
    public void RefundLines_MoreThanSoldQuantity_ThrowsInvalidOperationException()
    {
        var order = CreateOrder(quantity: 2);

        Assert.Throws<InvalidOperationException>(() =>
            order.RefundLines(new Dictionary<Guid, int> { [ProductId] = 3 }));
    }

    [Fact]
    public void RefundLines_ForAnUnknownProduct_ThrowsInvalidOperationException()
    {
        var order = CreateOrder();

        Assert.Throws<InvalidOperationException>(() =>
            order.RefundLines(new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 }));
    }

    [Fact]
    public void RefundLines_FullQuantity_TransitionsToRefunded()
    {
        var order = CreateOrder(quantity: 3);

        order.RefundLines(new Dictionary<Guid, int> { [ProductId] = 3 });

        Assert.Equal(SalesOrderStatus.Refunded, order.Status);
        Assert.Equal(0, order.Items.Single().RefundableQuantity);
    }

    [Fact]
    public void RefundLines_PartialQuantity_TransitionsToPartiallyRefunded()
    {
        var order = CreateOrder(quantity: 3);

        order.RefundLines(new Dictionary<Guid, int> { [ProductId] = 1 });

        Assert.Equal(SalesOrderStatus.PartiallyRefunded, order.Status);
        Assert.Equal(2, order.Items.Single().RefundableQuantity);
    }

    [Fact]
    public void Constructor_WithNoLines_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SalesOrder(StoreId, null, UserId, "key", []));
    }
}
