using Newtonsoft.Json;
using System.Text;
using VinhKhanhFood.Models;

namespace VinhKhanhFood.Services
{
    public class ApiService
    {
        // QUAN TRỌNG: Thay IP của máy tính bạn vào đây (Ví dụ: 192.168.1.5)
        // Số 7044 là Port của Web API (xem lại trên trình duyệt khi chạy API)
        private const string BaseUrl = "http://192.168.100.106:5100/api/";
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<Poi>> GetPoisAsync()
        {
            // Gọi API lấy danh sách Quán ăn
            var response = await _httpClient.GetAsync(BaseUrl + "Pois");
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Poi>>(json) ?? new List<Poi>();
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            var response = await _httpClient.GetAsync(BaseUrl + $"Pois/{poiId}");
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API lỗi {(int)response.StatusCode}: {content}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Poi>(json);
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
            var response = await _httpClient.PostAsync(BaseUrl + "Auth/login-app", content);
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
    }
}