using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký chuỗi kết nối
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<VinhKhanhAudioGuideContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Cấu hình Controllers và FIX lỗi vòng lặp JSON (Chỗ này quan trọng nè)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 3. Cấu hình Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Đăng ký dịch vụ Authentication dùng JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "VinhKhanhApi",
            ValidAudience = "VinhKhanhApp",
            // Chìa khóa bí mật (Phải giống hệt lúc tạo Token)
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("VinhKhanhFood_Super_Secret_Key_12345!!!"))
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();

// 4. Cấu hình HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();