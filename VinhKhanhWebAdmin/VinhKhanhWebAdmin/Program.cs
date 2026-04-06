using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using VinhKhanhWebAdmin.Components;
using VinhKhanhWebAdmin.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Nhớ thay số 5100 thành cái Port (hoặc IP) API của bạn đang chạy nhé
// Đăng ký ông Giao liên vào hệ thống
builder.Services.AddTransient<CustomHttpHandler>();

// Bố trí Giao liên chặn đầu mọi yêu cầu của HttpClient
builder.Services.AddScoped(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var apiBaseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5100/";

    var handler = sp.GetRequiredService<CustomHttpHandler>();
    handler.InnerHandler = new HttpClientHandler(); // Lõi của HttpClient

    var client = new HttpClient(handler)
    {
        // QUAN TRỌNG: Sửa cấu hình Api:BaseUrl trong appsettings nếu API đổi cổng/domain
        BaseAddress = new Uri(apiBaseUrl)
    };
    return client;
});
builder.Services.AddBlazoredLocalStorage();
// Kích hoạt Hệ thống Phân quyền Cốt lõi
builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/");
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/");
            return Task.CompletedTask;
        };
    });
builder.Services.AddCascadingAuthenticationState();

// Đăng ký Máy quét thẻ của bạn làm Máy quét chính thức của cả hệ thống
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
