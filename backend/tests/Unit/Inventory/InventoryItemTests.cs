using Domain.Inventory;
using Xunit;

namespace Unit.Inventory;

public class InventoryItemTests
{
    [Fact]
    public void Decrease_WithSufficientStock_Succeeds()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 5);

        item.Decrease(3);

        Assert.Equal(2, item.QuantityOnHand);
    }

    [Fact]
    public void Decrease_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 1);

        Assert.Throws<InvalidOperationException>(() => item.Decrease(2));
        // The domain invariant — a rejected decrease must not partially apply.
        Assert.Equal(1, item.QuantityOnHand);
    }

    [Fact]
    public void Decrease_TheLastUnit_LeavesExactlyZero_NeverNegative()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), initialQuantity: 1);

        item.Decrease(1);

        Assert.Equal(0, item.QuantityOnHand);
        Assert.Throws<InvalidOperationException>(() => item.Decrease(1));
    }

    [Fact]
    public void Increase_WithNonPositiveQuantity_ThrowsArgumentException()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => item.Increase(0));
    }
}
