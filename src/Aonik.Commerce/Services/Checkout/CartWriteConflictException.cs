using Aonik.Commerce.Entities.Cart;

namespace Aonik.Commerce.Services.Checkout;

public sealed class CartWriteConflictException : Exception
{
    public CartWriteConflictException(Cart cart, string code, string message) : base(message)
    {
        CartId = cart.Id;
        CartVersion = Convert.ToBase64String(cart.RowVersion);
        Status = cart.Status;
        OrderId = cart.OrderId;
        Code = code;
    }

    public Guid CartId { get; }
    public string CartVersion { get; }
    public string Status { get; }
    public Guid? OrderId { get; }
    public string Code { get; }
}
