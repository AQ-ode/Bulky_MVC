using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
	public interface IOrderDetailRepository : IRepository<OrderDetail>
	{
		void update(OrderDetail obj);

	}
}
