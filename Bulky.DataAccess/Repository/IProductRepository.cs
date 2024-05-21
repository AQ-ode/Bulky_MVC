using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
    public interface IProductRepository : IRepository<Product>
    {
        void update(Product obj);
    }
}
