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
builder.Services.AddHttpClient();
var app = builder.Build();

// Tạo bảng AppEventLog nếu DB chưa có (không cần migrations)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VinhKhanhAudioGuideContext>();
    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[AppEventLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppEventLog](
        [EventID] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DeviceID] VARCHAR(255) NOT NULL,
        [EventType] VARCHAR(50) NOT NULL,
        [QrCode] VARCHAR(100) NULL,
        [POIID] INT NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_AppEventLog_CreatedAt] DEFAULT (GETDATE())
    );

    CREATE INDEX [IX_AppEventLog_DeviceID_CreatedAt] ON [dbo].[AppEventLog]([DeviceID], [CreatedAt]);
    CREATE INDEX [IX_AppEventLog_EventType_CreatedAt] ON [dbo].[AppEventLog]([EventType], [CreatedAt]);
END
");
}

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