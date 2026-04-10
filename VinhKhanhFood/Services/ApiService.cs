using Microsoft.Maui.Storage;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using VinhKhanhFood.Models;

namespace VinhKhanhFood.Services
{
    public class ApiService
    {
        // QUAN TRỌNG: Thay IP của máy tính bạn vào đây (Ví dụ: 192.168.1.5)
        // Số 7044 là Port của Web API (xem lại trên trình duyệt khi chạy API)
        private const string BaseApiUrl = "http://192.168.1.46:5100/api/";
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
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response = await _httpClient.GetAsync(BaseApiUrl + $"Pois/{poiId}");
            }

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

        public async Task TrackPoiVisitAsync(int poiId)
        {
            if (poiId <= 0)
            {
                return;
            }

            var payload = new
            {
                Poiid = poiId,
                DeviceId = GetOrCreateTrackingDeviceId()
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(BaseApiUrl + "VisitLogs", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {error}");
            }
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

        public async Task<List<int>> GetFavoritePoiIdsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseApiUrl + "user-favorites");
            ApplyAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
        }

        public async Task SetFavoriteAsync(int poiId, bool isFavorite)
        {
            var method = isFavorite ? HttpMethod.Put : HttpMethod.Delete;
            using var request = new HttpRequestMessage(method, BaseApiUrl + $"user-favorites/{poiId}");
            ApplyAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }
        }

        public async Task<string?> GetPreferredLanguageAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseApiUrl + "user-preferences/language");
            ApplyAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<UserLanguageResponse>(json);
            return result?.LanguageCode;
        }

        public async Task SetPreferredLanguageAsync(string languageCode)
        {
            var payload = new UserLanguageRequest { LanguageCode = languageCode };
            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Put, BaseApiUrl + "user-preferences/language")
            {
                Content = content
            };
            ApplyAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {error}");
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
            var normalizedPath = imagePath.Trim().Replace('\\', '/');

            if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absolute))
            {
                return absolute;
            }

            var trimmed = normalizedPath.TrimStart('~');
            if (!trimmed.StartsWith('/'))
            {
                trimmed = "/" + trimmed;
            }

            return Uri.TryCreate(new Uri(BaseFileUrl), trimmed, out var combined) ? combined : null;
        }

        private static void ApplyAuthorizationHeader(HttpRequestMessage request)
        {
            var token = Preferences.Default.Get("AuthToken", string.Empty);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private static string GetOrCreateTrackingDeviceId()
        {
            const string key = "VisitTrackingDeviceId";
            var existing = Preferences.Default.Get(key, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var created = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(key, created);
            return created;
        }

        private class UserLanguageRequest
        {
            public string LanguageCode { get; set; } = string.Empty;
        }

        private class UserLanguageResponse
        {
            public string? LanguageCode { get; set; }
        }
    }
}