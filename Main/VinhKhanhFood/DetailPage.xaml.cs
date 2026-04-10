using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Networking;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;

namespace VinhKhanhFood;

public partial class DetailPage : ContentPage
{
    private Poi _poi;
    private readonly ApiService _apiService = new ApiService();
    private readonly OfflineSyncService _offlineSyncService;
    private bool _isLoadingDetail;
    // private bool _hasTrackedVisit; // Removed duplicate declaration

    // Sửa constructor để nhận đối tượng Poi
    public DetailPage(Poi poi, bool autoSpeakOnAppear = false)
    {
        InitializeComponent();
        _poi = poi;
        _offlineSyncService = new OfflineSyncService(_apiService);
        BindingContext = _poi;

        //  Vừa mở trang lên là kiểm tra xem quán này đã thả tim chưa
        CheckFavoriteStatus();

        if (autoSpeakOnAppear)
        {
            Dispatcher.Dispatch(async () => await SpeakPoiAsync());
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoadingDetail || _poi == null)
        {
            return;
        }

        if (_poi.Menus != null && _poi.Menus.Any())
        {
            await TryTrackVisitAsync();
            return;
        }

        _isLoadingDetail = true;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                var cachedPois = await _offlineSyncService.LoadPoisAsync();
                var cachedPoi = cachedPois.FirstOrDefault(p => p.Poiid == _poi.Poiid);
                if (cachedPoi != null)
                {
                    cachedPoi.Introduction = cachedPoi.Poilocalizations?.FirstOrDefault()?.Description
                                             ?? _poi.Introduction
                                             ?? "Chào mừng bạn đến với " + cachedPoi.Name;
                    cachedPoi.Description = _poi.Description ?? "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";

                    if (cachedPoi.Menus == null || !cachedPoi.Menus.Any())
                    {
                        cachedPoi.Menus = _poi.Menus ?? new List<Menu>();
                    }

                    _poi = cachedPoi;
                    BindingContext = _poi;
                }

                return;
            }

            var detailPoi = await _apiService.GetPoiByIdAsync(_poi.Poiid);
            if (detailPoi == null)
            {
                return;
            }

            detailPoi.Introduction = detailPoi.Poilocalizations?.FirstOrDefault()?.Description
                                     ?? _poi.Introduction
                                     ?? "Chào mừng bạn đến với " + detailPoi.Name;
            detailPoi.Description = _poi.Description ?? "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";

            if (detailPoi.Menus == null || !detailPoi.Menus.Any())
            {
                detailPoi.Menus = _poi.Menus ?? new List<Menu>();
            }

            _poi = detailPoi;
            BindingContext = _poi;
            await TryTrackVisitAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể tải chi tiết địa điểm: " + ex.Message, "OK");
        }
        finally
        {
            _isLoadingDetail = false;
        }
    }

    private async Task TryTrackVisitAsync()
    {
        if (_hasTrackedVisit || _poi == null || _poi.Poiid <= 0)
        {
            return;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        try
        {
            await _apiService.TrackPoiVisitAsync(_poi.Poiid);
            _hasTrackedVisit = true;
        }
        catch
        {
        }
    }

}
