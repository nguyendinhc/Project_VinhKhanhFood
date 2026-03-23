using Newtonsoft.Json;
using VinhKhanhFood.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace VinhKhanhFood.Services
{
    public class ApiService
    {
        // QUAN TRỌNG: Thay IP của máy tính bạn vào đây (Ví dụ: 192.168.1.5)
        // Số 7044 là Port của Web API (xem lại trên trình duyệt khi chạy API)
        private const string BaseUrl = "http://10.122.176.26:5100/api/";
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
    }
}