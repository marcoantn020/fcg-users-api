using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UsersAPI.Domain.Entity;

namespace UsersAPI.Infrastructure.Persistence;

public class UsersDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public UsersDbContext()
    {
    }
    
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // remover prefixos padrão do identity (AspNet *)
        builder.Entity<AppUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        
        // ajuste de camos para o EF Core
        builder.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(120);
        
        // Mapeia as entidades do Outbox do MassTransit (EF Core)
        builder.AddInboxStateEntity();
        builder.AddOutboxMessageEntity();
        builder.AddOutboxStateEntity();
    }
}