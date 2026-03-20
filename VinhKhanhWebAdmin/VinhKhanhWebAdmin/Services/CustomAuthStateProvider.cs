using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop; // Thư viện để gọi Javascript (đọc SessionStorage)

namespace VinhKhanhWebAdmin.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _jsRuntime;

        // Cấp cho máy quét cả 2 công cụ: LocalStorage và JSRuntime
        public CustomAuthStateProvider(ILocalStorageService localStorage, IJSRuntime jsRuntime)
        {
            _localStorage = localStorage;
            _jsRuntime = jsRuntime;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // 1. Ưu tiên thò tay vào "túi quần" (LocalStorage) tìm thẻ trước
                var token = await _localStorage.GetItemAsStringAsync("authToken");

                // 2. Nếu túi quần trống trơn, mò lên "túi áo" (SessionStorage)
                if (string.IsNullOrWhiteSpace(token))
                {
                    try
                    {
                        // Gọi Javascript để moi thẻ từ SessionStorage ra
                        token = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");
                    }
                    catch
                    {
                        // (Bỏ qua lỗi rác khi Blazor vừa khởi động chưa kịp nạp xong Javascript)
                    }
                }

                // 3. Cả 2 túi đều không có gì -> Khách vãng lai, đuổi ra ngoài!
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                // 4. Tìm thấy thẻ ở 1 trong 2 túi -> Giải mã lấy chức vụ báo cáo lên hệ thống
                var claims = ParseClaimsFromJwt(token);
                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                return new AuthenticationState(user);
            }
            catch
            {
                // Có lỗi bất ngờ xảy ra -> Cứ coi như chưa đăng nhập cho an toàn
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public void MarkUserAsAuthenticated(string token)
        {
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void MarkUserAsLoggedOut()
        {
            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        }

        // --- CÔNG CỤ GIẢI MÃ TOKEN GIỮ NGUYÊN ---
        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!));
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}