using Bulky.DataAccess.Data;
using Bulky.Models;

namespace Bulky.DataAccess.Repository
{
	public class OrderHeaderRepository : Repository<OrderHeader>, IOrderHeaderRepository
	{
		private ApplicationDbContext _db;
		public OrderHeaderRepository(ApplicationDbContext db) : base(db)
		{

			_db = db;
		}
		public void update(OrderHeader obj)
		{
			_db.OrderHeaders.Update(obj);
		}

		public void UpdateStatus(int id, string orderStatus, string? paymentStatus = null)
		{
			var orderFromDb = _db.OrderHeaders.FirstOrDefault(u => u.Id == id);
			if (orderFromDb != null)
			{
				orderFromDb.OrderStatus = orderStatus;
				if (!string.IsNullOrEmpty(paymentStatus))
				{
					orderFromDb.PaymentStatus = paymentStatus;
				}
			}
		}

		public void UpdateStripePaymentId(int id, string sessionid, string paymentIntentId)
		{
			var orderFromDb = _db.OrderHeaders.FirstOrDefault(u => u.Id == id);
			if (!string.IsNullOrEmpty(sessionid))
			{
				orderFromDb.SessionId = sessionid;
				if (!string.IsNullOrEmpty(paymentIntentId))
				{
					orderFromDb.PaymentIntentId = paymentIntentId;
					orderFromDb.PaymentDate = DateTime.Now;
				}
			}
		}
	}
}
