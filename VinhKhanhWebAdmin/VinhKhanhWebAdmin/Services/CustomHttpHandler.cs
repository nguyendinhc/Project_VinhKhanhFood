using System.Net.Http.Headers;
using Blazored.LocalStorage;
using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace VinhKhanhWebAdmin.Services
{
    // Kế thừa DelegatingHandler để làm "Trạm kiểm soát" mọi thư từ gửi đi API
    public class CustomHttpHandler : DelegatingHandler
    {
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _jsRuntime;

        public CustomHttpHandler(ILocalStorageService localStorage, IJSRuntime jsRuntime)
        {
            _localStorage = localStorage;
            _jsRuntime = jsRuntime;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.Contains("/api/Auth/login", StringComparison.OrdinalIgnoreCase) == true)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // 1. Tự động tìm thẻ trong túi quần
            string? token = null;
            try
            {
                token = await _localStorage.GetItemAsStringAsync("authToken");
            }
            catch
            {
                // Bỏ qua lỗi JS interop khi mạch Blazor chưa sẵn sàng
            }

            // 2. Không có thì mò túi áo
            if (string.IsNullOrEmpty(token))
            {
                try
                {
                    token = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");
                }
                catch
                {
                    // Bỏ qua lỗi nếu JS chưa load xong
                }
            }

            // 3. Nếu có thẻ -> Tự động dán mộc Bearer lên thư
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // 4. Cho phép bức thư được gửi đi
            return await base.SendAsync(request, cancellationToken);
        }
    }
}