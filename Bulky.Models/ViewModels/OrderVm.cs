namespace Bulky.Models.ViewModels
{
	public class OrderVm
	{
		public OrderHeader OrderHeader { get; set; }
		public IEnumerable<OrderDetail> OrderDetail { get; set; }
	}
}
