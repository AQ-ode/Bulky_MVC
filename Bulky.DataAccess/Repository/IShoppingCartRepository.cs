using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
    public interface IShoppingCartRepository : IRepository<ShoppingCart>
    {
        void update(ShoppingCart obj);

    }
}
