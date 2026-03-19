using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;
namespace VinhKhanhFood
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
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