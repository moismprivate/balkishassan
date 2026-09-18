using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BalkisHassan.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var email = configuration["AdminBootstrap:Email"];
        var password = configuration["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (!await userManager.CheckPasswordAsync(existingUser, password))
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resetResult = await userManager.ResetPasswordAsync(existingUser, resetToken, password);
                if (!resetResult.Succeeded)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
                    logger.LogError("Het beheerwachtwoord uit User Secrets kon niet worden toegepast: {Errors}",
                        string.Join(", ", resetResult.Errors.Select(x => x.Description)));
                    return;
                }
            }

            if (await userManager.IsLockedOutAsync(existingUser))
            {
                await userManager.SetLockoutEndDateAsync(existingUser, null);
            }
            await userManager.ResetAccessFailedCountAsync(existingUser);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "بلقيس حميد حسن"
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
            logger.LogError("Het initiële beheeraccount kon niet worden aangemaakt: {Errors}",
                string.Join(", ", result.Errors.Select(x => x.Description)));
        }
    }
}
