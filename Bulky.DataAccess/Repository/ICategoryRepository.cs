using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
    public interface ICategoryRepository : IRepository<Category>
    {
        void update(Category obj);

    }
}
