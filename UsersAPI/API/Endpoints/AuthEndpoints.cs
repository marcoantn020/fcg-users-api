using Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using UsersAPI.Domain.Entity;
using UsersAPI.Infrastructure.Auth;
using UsersAPI.Infrastructure.Persistence;

namespace UsersAPI.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        AuthDtos.RegisterRequest request,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IPublishEndpoint publishEndpoint,
        IJwtTokenService jwtTokenService,
        UsersDbContext dbContext // injeta o contexto para controle transacional
    )
    {
        var exists = await userManager.FindByEmailAsync(request.Email);
        if (exists is not null)
            return Results.Conflict(new { message = "Email already exists" });

        const string defaultRole = "User";
        if (!await roleManager.RoleExistsAsync(defaultRole))
            await roleManager.CreateAsync(new IdentityRole<Guid>(defaultRole));

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        // inicia transação para garantir flush do outbox no commit
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
            return Results.BadRequest(new { errors = create.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, defaultRole);

        // força persistir alterações do Identity no mesmo contexto

        var evt = new UserCreatedEventV1(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow,
            UserId: user.Id,
            Email: user.Email ?? "email_not_set",
            DisplayName: user.DisplayName ?? "display_name_not_set"
        );

        await publishEndpoint.Publish(evt, context =>
        {
            context.SetRoutingKey("v1.user-created");
            context.ConversationId = evt.EventId;
        });

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        var token = await jwtTokenService.CreateTokenAsync(user);

        return Results.Created($"/users/{user.Id}",
            new AuthDtos.AuthResponse(
                UserId: user.Id,
                Email: user.Email ?? "email_not_set",
                DisplayName: user.DisplayName ?? "display_name_not_set",
                AccessToken: token
            ));
    }

    private static async Task<IResult> LoginAsync(
        AuthDtos.LoginRequest request,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IJwtTokenService jwtTokenService)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Results.Unauthorized();

        var ok = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!ok.Succeeded)
            return Results.Unauthorized();

        var token = await jwtTokenService.CreateTokenAsync(user);
        return Results.Ok(new AuthDtos.AuthResponse(
            user.Id, user.Email ?? "Email_not_found", user.DisplayName ?? "DisplayName_not_found", token));
    }
}