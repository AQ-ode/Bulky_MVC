using Bulky.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bulky.DataAccess.Data
{

	public class ApplicationDbContext : IdentityDbContext<IdentityUser>
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{


		}

		public DbSet<Category> Categories { get; set; } // categorie dbset

		public DbSet<Product> Product { get; set; }
		public DbSet<ApplicationUser> ApplicationUsers { get; set; }
		public DbSet<Company> Companies { get; set; }
		public DbSet<ShoppingCart> ShoppingCarts { get; set; }
		public DbSet<OrderHeader> OrderHeaders { get; set; }
		public DbSet<OrderDetail> OrderDetails { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<Category>().HasData(
				new Category { Id = 1, Name = "Action", DisplayOrder = 1 },
				new Category { Id = 2, Name = "SciFi", DisplayOrder = 2 },
				new Category { Id = 3, Name = "History", DisplayOrder = 3 }

				);
		}
	}
}

