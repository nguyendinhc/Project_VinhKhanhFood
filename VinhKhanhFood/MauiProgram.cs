using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using ZXing.Net.Maui.Controls;
namespace VinhKhanhFood
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            SearchBarHandler.Mapper.AppendToMapping("SearchIconColor", (handler, view) =>
            {
#if ANDROID
                var searchIconId = handler.PlatformView.Context?.Resources?.GetIdentifier("search_mag_icon", "id", "android") ?? 0;
                if (searchIconId != 0)
                {
                    var searchIcon = handler.PlatformView.FindViewById<Android.Widget.ImageView>(searchIconId);
                    searchIcon?.SetColorFilter(Android.Graphics.Color.ParseColor("#2C2C2C"), Android.Graphics.PorterDuff.Mode.SrcIn);
                }
#endif
            });

            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .UseMauiMaps()
                .UseMauiCommunityToolkit() // Kích hoạt bộ công cụ
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}                                                                                               