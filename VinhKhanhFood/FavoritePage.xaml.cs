using VinhKhanhFood.Models;
using VinhKhanhFood.Services; // Thêm dòng này để xài ApiService

namespace VinhKhanhFood;

public partial class FavoritePage : ContentPage
{
    // Khai báo lại ApiService giống hệt bên MainPage
    ApiService _apiService = new ApiService();
    private readonly OfflineSyncService _offlineSyncService;
    bool _isNavigating;
    public Command<Poi?> FavoriteTapCommand { get; }

    public FavoritePage()
    {
        InitializeComponent();
        BindingContext = this;
        _offlineSyncService = new OfflineSyncService(_apiService);
        FavoriteTapCommand = new Command<Poi?>(async selectedPoi => await NavigateToDetailAsync(selectedPoi));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _offlineSyncService.ProcessPendingActionsAsync();

        // 1. Đọc bộ nhớ xem khách đã thả tim những quán nào
        string savedFavorites = Preferences.Default.Get("FavoritePois", "");
        string savedFavoriteIds = Preferences.Default.Get("FavoritePoiIds", "");
        List<string> favNames = savedFavorites.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        List<int> favIds = savedFavoriteIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var id) ? id : -1)
            .Where(id => id > 0)
            .ToList();

        if (favNames.Count == 0 && favIds.Count == 0)
        {
            lstFavorites.ItemsSource = null;
            lblEmpty.IsVisible = true;
            return;
        }

        try
        {
            List<Poi> allPois = new();

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    await _offlineSyncService.SyncPoisAsync();
                }
                catch
                {
                }
            }

            allPois = await _offlineSyncService.LoadPoisAsync();

            if (allPois != null && allPois.Any())
            {
                foreach (var item in allPois)
                {
                    item.Introduction = item.Poilocalizations?.FirstOrDefault()?.Description
                                        ?? "Chào mừng bạn đến với " + item.Name;
                    item.Description = "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";
                }

                var favoritePois = allPois
                    .Where(p => favIds.Contains(p.Poiid) || favNames.Contains(p.Name))
                    .ToList();
                lstFavorites.ItemsSource = favoritePois;
                lblEmpty.IsVisible = !favoritePois.Any();
            }
            else
            {
                lstFavorites.ItemsSource = null;
                lblEmpty.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể tải danh sách yêu thích: " + ex.Message, "OK");
        }
    }

    private async Task NavigateToDetailAsync(Poi? selectedPoi)
    {
        if (_isNavigating)
        {
            return;
        }

        if (selectedPoi == null)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            await Navigation.PushAsync(new DetailPage(selectedPoi));
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
