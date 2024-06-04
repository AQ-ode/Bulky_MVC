using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
	public interface IOrderHeaderRepository : IRepository<OrderHeader>
	{
		void update(OrderHeader obj);

		void UpdateStatus(int id, string orderStatus, string? paymentStatus = null);
		void UpdateStripePaymentId(int id, string sessionid, string paymentIntentId);


	}
}
