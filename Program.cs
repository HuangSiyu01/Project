using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;
using Project.Repositories;
using Project.Services;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Register MVC views and attribute-routed REST API controllers.
builder.Services.AddControllersWithViews();

// Allow the attraction upload endpoint to receive up to 250 MB when hosted by IIS.
// The controller applies the same endpoint-specific limit for Kestrel requests.
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 250L * 1024 * 1024;
});

// Allow AJAX clients to send the anti-forgery token in a request header.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Register distributed memory cache for Session.
builder.Services.AddDistributedMemoryCache();

// Register HttpContextAccessor.
builder.Services.AddHttpContextAccessor();

// Configure secure Session.
builder.Services.AddSession(options =>
{
    // Session expires after 30 minutes of inactivity.
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    // Give the Session cookie a specific name.
    options.Cookie.Name = ".TravelShare.Session";

    // Prevent client-side JavaScript from reading the cookie.
    options.Cookie.HttpOnly = true;

    // Session cookie is required for application functionality.
    options.Cookie.IsEssential = true;

    // Only send the cookie over HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Reduce cross-site cookie transmission.
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Register the database context.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register ASP.NET Core Identity.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure the authentication cookie.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".TravelShare.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.LoginPath = "/Customer/Account/Login";
    options.LogoutPath = "/Customer/Account/Logout";
    options.AccessDeniedPath = "/Customer/Account/Login";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
});

// Register Repository and Unit of Work services.
builder.Services.AddScoped(
    typeof(IRepository<>),
    typeof(Repository<>));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register the shopping cart service.
builder.Services.AddScoped<CartService>();

// Register the e-ticket service used by TicketsController and refund processing.
builder.Services.AddScoped<TicketService>();

// Register Stripe settings.
builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection("Stripe"));

// Configure the Stripe API secret key.
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

if (string.IsNullOrWhiteSpace(stripeSecretKey) ||
    stripeSecretKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Stripe Secret Key is not configured. Replace the placeholder in appsettings.json with your new Stripe test Secret Key.");
}

StripeConfiguration.ApiKey = stripeSecretKey;

var app = builder.Build();

// Configure error handling.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Session must run before authentication and MVC endpoints.
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

// Register attribute-routed API endpoints such as /api/payments/checkout-session.
app.MapControllers();

// Configure Area routes.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Configure the default MVC route.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
