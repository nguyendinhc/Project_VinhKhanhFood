using VinhKhanhFood.Services;

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
            ShowError("Vui lòng nh?p tên ??ng nh?p và m?t kh?u.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("M?t kh?u c?n t?i thi?u 6 ký t?.");
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            ShowError("M?t kh?u nh?p l?i không kh?p.");
            return;
        }

        await SetBusyAsync(true);
        try
        {
            await _apiService.RegisterOwnerRequestAsync(userName, password, fullName, email);
            await DisplayAlert("Thành công", "?ã g?i ??ng ký. Vui lòng ch? Admin duy?t và gán quy?n Ch? quán.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Không th? g?i ??ng ký." : ex.Message);
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
