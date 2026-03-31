using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace HomeWorkJudge.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _model = "gemini-2.0-flash";
    [ObservableProperty] private double _temperature = 0.2;
    [ObservableProperty] private int _timeoutSeconds = 60;
    [ObservableProperty] private string? _statusMessage;

    public SettingsViewModel(IConfiguration configuration)
    {
        _configuration = configuration;
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        ApiKey = _configuration["Infrastructure:AI:Gemini:ApiKey"] ?? "";
        Model = _configuration["Infrastructure:AI:Gemini:Model"] ?? "gemini-2.0-flash";
        if (double.TryParse(_configuration["Infrastructure:AI:Gemini:Temperature"], out var temp))
            Temperature = temp;
        if (int.TryParse(_configuration["Infrastructure:AI:TimeoutSeconds"], out var timeout))
            TimeoutSeconds = timeout;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var settingsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                Infrastructure = new
                {
                    AI = new
                    {
                        TimeoutSeconds,
                        Gemini = new
                        {
                            ApiKey,
                            Model,
                            Temperature
                        }
                    }
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            await System.IO.File.WriteAllTextAsync(settingsPath, json);
            StatusMessage = "Đã lưu cấu hình. Khởi động lại app để áp dụng.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi lưu: {ex.Message}";
        }
    }
}
