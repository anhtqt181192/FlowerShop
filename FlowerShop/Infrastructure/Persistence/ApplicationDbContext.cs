using FlowerShop.Domain.Catalog;
using FlowerShop.Domain.Customers;
using FlowerShop.Domain.Inventory;
using FlowerShop.Domain.Orders;
using FlowerShop.Domain.Payments;
using FlowerShop.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Flower> Flowers => Set<Flower>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => item.Name).IsUnique();
        });

        builder.Entity<Flower>(entity =>
        {
            entity.ToTable("Flowers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.Price).HasPrecision(18, 2);
            entity.Property(item => item.ImageUrl).HasMaxLength(2048);
            entity.Property(item => item.Status).HasConversion<int>();
            entity.HasOne(item => item.Category).WithMany().HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.CategoryId);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FullName).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(254);
            entity.Property(item => item.Address).HasMaxLength(500);
            entity.HasIndex(item => item.Phone);
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrderNumber).HasMaxLength(30).IsRequired();
            entity.HasIndex(item => item.OrderNumber).IsUnique();
            entity.Property(item => item.Status).HasConversion<int>();
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.HasOne<Customer>().WithMany().HasForeignKey(item => item.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany<OrderItem>("_items").WithOne().HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.OwnsOne(item => item.ShippingAddress, address =>
            {
                address.Property(item => item.ReceiverName).HasMaxLength(150).IsRequired();
                address.Property(item => item.Phone).HasMaxLength(30).IsRequired();
                address.Property(item => item.Province).HasMaxLength(100).IsRequired();
                address.Property(item => item.District).HasMaxLength(100).IsRequired();
                address.Property(item => item.Ward).HasMaxLength(100).IsRequired();
                address.Property(item => item.Address).HasMaxLength(500).IsRequired();
            });
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FlowerName).HasMaxLength(150).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Ignore(item => item.SubTotal);
        });

        builder.Entity<Inventory>(entity =>
        {
            entity.ToTable("Inventories");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.FlowerId).IsUnique();
            entity.Property(item => item.Version).IsRowVersion();
            entity.HasOne<Flower>().WithOne().HasForeignKey<Inventory>(item => item.FlowerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InventoryTransaction>(entity =>
        {
            entity.ToTable("InventoryTransactions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasConversion<int>();
            entity.Property(item => item.ReferenceNo).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.FlowerId, item.CreatedAt });
        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Method).HasConversion<int>();
            entity.Property(item => item.Status).HasConversion<int>();
            entity.Property(item => item.TransactionCode).HasMaxLength(100);
            entity.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Category>().HasData(
            new { Id = 1, Name = "Hoa hồng", Description = "Những đóa hồng dịu dàng cho mọi dịp." },
            new { Id = 2, Name = "Hoa bó", Description = "Bó hoa tươi được thiết kế thủ công." },
            new { Id = 3, Name = "Hoa theo mùa", Description = "Sắc hoa đẹp nhất theo mùa." });

        builder.Entity<Flower>().HasData(
            new
            {
                Id = Guid.Parse("a1200000-0000-4000-8000-000000000001"), Name = "Bó hồng nàng thơ",
                Description = "12 đóa hồng pastel kết hợp cùng lá bạc, gói giấy mỹ thuật.",
                Price = 690000m, ImageUrl = "https://images.unsplash.com/photo-1494972308805-463bc619d34e?auto=format&fit=crop&w=900&q=85",
                CategoryId = 1, Status = FlowerStatus.Active
            },
            new
            {
                Id = Guid.Parse("a1200000-0000-4000-8000-000000000002"), Name = "Bó tulip bình minh",
                Description = "Tulip hồng nhập khẩu, dịu dàng như lời chúc ngày mới.",
                Price = 890000m, ImageUrl = "https://images.unsplash.com/photo-1520763185298-1b434c919102?auto=format&fit=crop&w=900&q=85",
                CategoryId = 2, Status = FlowerStatus.Active
            },
            new
            {
                Id = Guid.Parse("a1200000-0000-4000-8000-000000000003"), Name = "Cẩm tú cầu mơ màng",
                Description = "Cẩm tú cầu xanh trắng trong chiếc hộp quà thanh lịch.",
                Price = 750000m, ImageUrl = "https://images.unsplash.com/photo-1561181286-d3fee7d55364?auto=format&fit=crop&w=900&q=85",
                CategoryId = 3, Status = FlowerStatus.Active
            },
            new
            {
                Id = Guid.Parse("a1200000-0000-4000-8000-000000000004"), Name = "Bó hoa ngày thương",
                Description = "Sự kết hợp tinh tế của hồng, cúc tana và hoa baby.",
                Price = 590000m, ImageUrl = "https://images.unsplash.com/photo-1525310072745-f49212b5ac6d?auto=format&fit=crop&w=900&q=85",
                CategoryId = 2, Status = FlowerStatus.Active
            });

        builder.Entity<Inventory>().HasData(
            new { Id = Guid.Parse("b1200000-0000-4000-8000-000000000001"), FlowerId = Guid.Parse("a1200000-0000-4000-8000-000000000001"), AvailableQuantity = 18, ReservedQuantity = 0 },
            new { Id = Guid.Parse("b1200000-0000-4000-8000-000000000002"), FlowerId = Guid.Parse("a1200000-0000-4000-8000-000000000002"), AvailableQuantity = 12, ReservedQuantity = 0 },
            new { Id = Guid.Parse("b1200000-0000-4000-8000-000000000003"), FlowerId = Guid.Parse("a1200000-0000-4000-8000-000000000003"), AvailableQuantity = 10, ReservedQuantity = 0 },
            new { Id = Guid.Parse("b1200000-0000-4000-8000-000000000004"), FlowerId = Guid.Parse("a1200000-0000-4000-8000-000000000004"), AvailableQuantity = 20, ReservedQuantity = 0 });
    }
}
