using VinhKhanhFood.Services;

namespace VinhKhanhFood;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _apiService = new();
    private bool _isBusy;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        lblError.IsVisible = false;
        var username = entryUsername.Text?.Trim();
        var password = entryPassword.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Vui lòng nhập tài khoản và mật khẩu.");
            return;
        }

        await SetBusyAsync(true);
        try
        {
            var result = await _apiService.LoginAsync(username, password);
            if (result == null)
            {
                ShowError("Sai tài khoản hoặc mật khẩu.");
                return;
            }

            Preferences.Default.Set("AuthToken", result.Token ?? string.Empty);
            Preferences.Default.Set("UserRole", result.Role ?? string.Empty);
            Preferences.Default.Set("DisplayName", result.FullName ?? username);

            Application.Current!.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Không thể đăng nhập." : ex.Message);
        }
        finally
        {
            await SetBusyAsync(false);
        }
    }

    private void ShowError(string message)
    {
        lblError.Text = message;
        lblError.IsVisible = true;
    }

    private Task SetBusyAsync(bool value)
    {
        _isBusy = value;
        btnLogin.IsEnabled = !value;
        loadingIndicator.IsVisible = value;
        loadingIndicator.IsRunning = value;
        return Task.CompletedTask;
    }
}
