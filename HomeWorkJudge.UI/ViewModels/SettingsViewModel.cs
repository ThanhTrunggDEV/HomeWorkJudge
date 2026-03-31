using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Configuration;

namespace HomeWorkJudge.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly PaletteHelper _paletteHelper = new();

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _model = "gemini-2.0-flash";
    [ObservableProperty] private double _temperature = 0.2;
    [ObservableProperty] private int _timeoutSeconds = 60;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isDarkMode = true;

    public SettingsViewModel(IConfiguration configuration)
    {
        _configuration = configuration;
        LoadFromConfig();

        // Đọc theme hiện tại
        var theme = _paletteHelper.GetTheme();
        _isDarkMode = theme.GetBaseTheme() == BaseTheme.Dark;
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(value ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
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

            // Đọc file hiện tại để merge
            System.Text.Json.Nodes.JsonObject root;
            if (System.IO.File.Exists(settingsPath))
            {
                var existing = await System.IO.File.ReadAllTextAsync(settingsPath);
                root = System.Text.Json.Nodes.JsonNode.Parse(existing)?.AsObject()
                       ?? new System.Text.Json.Nodes.JsonObject();
            }
            else
            {
                root = new System.Text.Json.Nodes.JsonObject();
            }

            // Infrastructure.AI
            var infra = root["Infrastructure"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();
            var ai = infra["AI"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();
            var gemini = ai["Gemini"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();

            ai["TimeoutSeconds"] = TimeoutSeconds;
            gemini["ApiKey"] = ApiKey;
            gemini["Model"] = Model;
            gemini["Temperature"] = Temperature;
            ai["Gemini"] = gemini;
            infra["AI"] = ai;
            root["Infrastructure"] = infra;

            // UI preferences
            var ui = root["UI"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();
            ui["DarkMode"] = IsDarkMode;
            root["UI"] = ui;

            var json = root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(settingsPath, json);
            StatusMessage = "Đã lưu cấu hình.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi lưu: {ex.Message}";
        }
    }
}
