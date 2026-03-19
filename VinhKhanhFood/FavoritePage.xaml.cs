using VinhKhanhFood.Models;
using VinhKhanhFood.Services; // Thêm dòng này để xài ApiService

namespace VinhKhanhFood;

public partial class FavoritePage : ContentPage
{
    // Khai báo lại ApiService giống hệt bên MainPage
    ApiService _apiService = new ApiService();

    public FavoritePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Đọc bộ nhớ xem khách đã thả tim những quán nào
        string savedFavorites = Preferences.Default.Get("FavoritePois", "");
        List<string> favNames = savedFavorites.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (favNames.Count == 0)
        {
            lstFavorites.ItemsSource = null;
            lblEmpty.IsVisible = true;
            return;
        }

        lblEmpty.IsVisible = false;

        try
        {
            // ==========================================
            // 2. GỌI LẠI API ĐỂ LẤY TẤT CẢ DANH SÁCH QUÁN
            // ==========================================
            var allPois = await _apiService.GetPoisAsync();

            if (allPois != null && allPois.Any())
            {
                // 3. Lọc ra những quán có Tên nằm trong danh sách yêu thích
                var favoritePois = allPois.Where(p => favNames.Contains(p.Name)).ToList();

                // 4. Đổ lên màn hình
                lstFavorites.ItemsSource = favoritePois;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể tải danh sách yêu thích: " + ex.Message, "OK");
        }
    }

    private async void OnFavoriteSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Poi selectedPoi)
        {
            await Navigation.PushAsync(new DetailPage(selectedPoi));
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}