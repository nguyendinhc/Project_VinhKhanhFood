using Microsoft.Maui.Storage;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using VinhKhanhFood.Models;

namespace VinhKhanhFood.Services
{
    public class ApiService
    {
        // QUAN TRỌNG: Thay IP của máy tính bạn vào đây (Ví dụ: 192.168.1.5)
        // Số 7044 là Port của Web API (xem lại trên trình duyệt khi chạy API)
        private const string BaseApiUrl = "http://10.24.174.26:5100/api/";
        private static readonly string BaseFileUrl = BaseApiUrl.EndsWith("api/", StringComparison.OrdinalIgnoreCase)
            ? BaseApiUrl[..^4]
            : BaseApiUrl;
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<Poi>> GetPoisAsync()
        {
            // Gọi API lấy danh sách Quán ăn
            var response = await _httpClient.GetAsync(BaseApiUrl + "Pois/public");
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var pois = JsonConvert.DeserializeObject<List<Poi>>(json) ?? new List<Poi>();
            foreach (var poi in pois)
            {
                poi.LocalThumbnailPath = await CacheImageAsync(poi.Thumbnail, "poi");
            }

            return pois;
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            var response = await _httpClient.GetAsync(BaseApiUrl + $"Pois/public/{poiId}");
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var poi = JsonConvert.DeserializeObject<Poi>(json);
            if (poi != null)
            {
                poi.LocalThumbnailPath = await CacheImageAsync(poi.Thumbnail, "poi");
                if (poi.Menus != null)
                {
                    foreach (var menu in poi.Menus)
                    {
                        menu.LocalImagePath = await CacheImageAsync(menu.Image, "menu");
                    }
                }
            }

            return poi;
        }

        public async Task<LoginResult?> LoginAsync(string username, string password)
        {
            var payload = new LoginRequest
            {
                Username = username,
                Password = password
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(BaseApiUrl + "Auth/login-app", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? "Sai tài khoản hoặc mật khẩu."
                    : error);
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<LoginResult>(resultJson);
        }

        public async Task RegisterOwnerRequestAsync(string username, string password, string? fullName, string? email)
        {
            var payload = new
            {
                UserName = username,
                Password = password,
                FullName = fullName,
                Email = email
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(BaseApiUrl + "users/register-owner", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? "Không thể gửi đăng ký chủ quán."
                    : error);
            }
        }

        private async Task<string?> CacheImageAsync(string? imagePath, string prefix)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            var absoluteUrl = BuildAbsoluteImageUrl(imagePath);
            if (absoluteUrl == null)
            {
                return null;
            }

            var cacheFolder = Path.Combine(FileSystem.AppDataDirectory, "image_cache");
            Directory.CreateDirectory(cacheFolder);

            var extension = Path.GetExtension(absoluteUrl.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(absoluteUrl.ToString())));
            var filePath = Path.Combine(cacheFolder, $"{prefix}_{hash}{extension}");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(absoluteUrl);
                await File.WriteAllBytesAsync(filePath, bytes);
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        private static Uri? BuildAbsoluteImageUrl(string imagePath)
        {
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var absolute))
            {
                return absolute;
            }

            var trimmed = imagePath.TrimStart('~');
            if (!trimmed.StartsWith('/'))
            {
                trimmed = "/" + trimmed;
            }

            return Uri.TryCreate(new Uri(BaseFileUrl), trimmed, out var combined) ? combined : null;
        }
    }
}