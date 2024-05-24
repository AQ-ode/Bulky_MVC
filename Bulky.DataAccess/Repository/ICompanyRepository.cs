using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
    public interface ICompanyRepository : IRepository<Company>
    {
        void update(Company obj);
    }
}
