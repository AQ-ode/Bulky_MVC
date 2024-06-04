using Bulky.DataAccess.Data;
using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
    public class ShoppingCartRepository : Repository<ShoppingCart>, IShoppingCartRepository
    {
        private ApplicationDbContext _db;
        public ShoppingCartRepository(ApplicationDbContext db) : base(db)
        {

            _db = db;
        }
        public void update(ShoppingCart obj)
        {
            _db.Update(obj);
        }
    }
}
