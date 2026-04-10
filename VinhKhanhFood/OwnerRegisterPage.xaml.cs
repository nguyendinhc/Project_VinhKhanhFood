using VinhKhanhFood.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace VinhKhanhFood;

public partial class OwnerRegisterPage : ContentPage
{
    private readonly ApiService _apiService = new();
    private bool _isBusy;

    public OwnerRegisterPage()
    {
        InitializeComponent();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        lblError.IsVisible = false;

        var userName = entryUserName.Text?.Trim();
        var password = entryPassword.Text;
        var confirmPassword = entryConfirmPassword.Text;
        var fullName = entryFullName.Text?.Trim();
        var email = entryEmail.Text?.Trim();

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Vui lòng nhập tên đăng nhập và mật khẩu.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Mật khẩu cần tối thiểu 6 ký tự.");
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            ShowError("Mật khẩu nhập lại không khớp.");
            return;
        }

        await SetBusyAsync(true);
        try
        {
            await _apiService.RegisterOwnerRequestAsync(userName, password, fullName, email);
            await DisplayAlert("Thành công", "Đã gửi đăng ký. Vui lòng chờ Admin duyệt và gán quyền Chủ quán.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Không thể gửi đăng ký." : ex.Message);
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
        btnSubmit.IsEnabled = !value;
        loadingIndicator.IsVisible = value;
        loadingIndicator.IsRunning = value;
        return Task.CompletedTask;
    }
}
