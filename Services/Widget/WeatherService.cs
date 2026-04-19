using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Models.Widget;
using Nexus.Services.Http;

namespace Nexus.Services.Widget;

public class WeatherService : ICardProvider<WeatherCardModel>
{
    private const string ApiBaseUrl = "https://uapis.cn/api/v1/misc/weather";
    private const int RefreshIntervalMinutes = 5;
    
    private readonly ConfigService _configService;
    private readonly HttpService _httpService;
    private readonly Timer _refreshTimer;
    private WeatherCardModel? _currentData;
    
    public event EventHandler<WeatherCardModel>? DataUpdated;
    
    public CardType CardType => CardType.Weather;

    public WeatherService(ConfigService configService, ToastService? toastService = null)
    {
        _configService = configService;
        _httpService = new HttpService(configService, toastService);
        
        _refreshTimer = new Timer(async _ => await RefreshDataAsync(), null, 
            TimeSpan.FromMinutes(RefreshIntervalMinutes), 
            TimeSpan.FromMinutes(RefreshIntervalMinutes));
    }

    public async Task<WeatherCardModel> GetInitialDataAsync()
    {
        var widgetConfig = _configService.GetWidgetConfig();
        
        if (widgetConfig.LocationMode == LocationMode.Auto)
        {
            return await FetchWeatherByAutoLocationAsync();
        }
        
        if (widgetConfig.WeatherLocation == null || !widgetConfig.WeatherLocation.IsConfigured)
        {
            return new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "未设置地理位置，请在设置中配置"
            };
        }

        return await FetchWeatherAsync(widgetConfig.WeatherLocation.CityName);
    }

    public async Task<WeatherCardModel> RefreshDataAsync()
    {
        var widgetConfig = _configService.GetWidgetConfig();
        
        if (widgetConfig.LocationMode == LocationMode.Auto)
        {
            try
            {
                _currentData = await FetchWeatherByAutoLocationAsync();
                DataUpdated?.Invoke(this, _currentData);
                return _currentData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Weather auto refresh error: {ex.Message}");
                return _currentData ?? new WeatherCardModel
                {
                    IsNotConfigured = true,
                    Message = "自动定位获取天气失败"
                };
            }
        }
        
        if (widgetConfig.WeatherLocation == null || !widgetConfig.WeatherLocation.IsConfigured)
        {
            _currentData = new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "未设置地理位置，请在设置中配置"
            };
            return _currentData;
        }

        try
        {
            _currentData = await FetchWeatherAsync(widgetConfig.WeatherLocation.CityName);
            DataUpdated?.Invoke(this, _currentData);
            return _currentData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Weather refresh error: {ex.Message}");
            return _currentData ?? new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "获取天气数据失败"
            };
        }
    }

    public void HandleUpdate(object payload)
    {
    }

    private async Task<WeatherCardModel> FetchWeatherByAutoLocationAsync()
    {
        try
        {
            var location = await GetAutoLocationAsync();
            if (!string.IsNullOrEmpty(location))
            {
                return await FetchWeatherAsync(location);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto location error: {ex.Message}");
        }
        
        return new WeatherCardModel
        {
            IsNotConfigured = true,
            Message = "自动定位失败，请手动设置城市"
        };
    }

    public async Task<string?> GetAutoLocationAsync()
    {
        var content = await _httpService.GetRawAsync(
            "https://uapis.cn/api/v1/ip.location",
            new RequestOptions { RequireAuth = false, ShowErrorToast = false });
        
        if (string.IsNullOrEmpty(content)) return null;
        
        try
        {
            var data = JsonSerializer.Deserialize<IpLocationResponse>(content);
            return data?.City;
        }
        catch
        {
            return null;
        }
    }

    public async Task<WeatherCardModel> FetchWeatherAsync(string city)
    {
        var content = await _httpService.GetRawAsync(
            $"{ApiBaseUrl}?city={Uri.EscapeDataString(city)}&extended=true&forecast=true",
            new RequestOptions { RequireAuth = false, ShowErrorToast = false });
        
        if (string.IsNullOrEmpty(content))
        {
            return new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "获取天气数据失败"
            };
        }
        
        var data = JsonSerializer.Deserialize<UapiWeatherResponse>(content);
        return MapToCardModel(data);
    }

    private WeatherCardModel MapToCardModel(UapiWeatherResponse? data)
    {
        if (data == null)
        {
            return new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "获取天气数据失败"
            };
        }

        var model = new WeatherCardModel
        {
            IsNotConfigured = false,
            City = data.City ?? "",
            Weather = data.Weather ?? "",
            Temperature = data.Temperature,
            TempMin = data.TempMin,
            TempMax = data.TempMax,
            WindDirection = data.WindDirection ?? "",
            WindSpeed = ParseWindScale(data.WindPower),
            Humidity = data.Humidity,
            AirQuality = data.AqiCategory ?? "",
            FeelsLike = data.FeelsLike,
            Visibility = data.Visibility,
            Pressure = data.Pressure,
            UvIndex = data.Uv,
            Aqi = data.Aqi,
            AqiLevel = data.AqiLevel,
            AqiPrimary = data.AqiPrimary ?? "",
            Icon = GetWeatherIcon(data.Weather),
            LastUpdated = DateTime.Now
        };

        return model;
    }

    private int? ParseWindScale(string? windPower)
    {
        if (string.IsNullOrEmpty(windPower)) return null;
        if (windPower.Contains("级"))
        {
            var numStr = windPower.Replace("级", "").Trim();
            if (int.TryParse(numStr, out var result)) return result;
        }
        return null;
    }

    private string GetWeatherIcon(string? weather)
    {
        return weather switch
        {
            "晴" => "sunny",
            "多云" => "cloudy",
            "阴" => "overcast",
            "小雨" or "中雨" or "大雨" or "暴雨" => "rain",
            "小雪" or "中雪" or "大雪" or "暴雪" => "snow",
            "雾" or "霾" => "fog",
            _ => "cloudy"
        };
    }

    public void Stop()
    {
        _refreshTimer?.Dispose();
        _httpService?.Dispose();
    }
}

#region UAPI Weather Models

public class UapiWeatherResponse
{
    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("adcode")]
    public string? Adcode { get; set; }

    [JsonPropertyName("weather")]
    public string? Weather { get; set; }

    [JsonPropertyName("weather_icon")]
    public string? WeatherIcon { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("wind_direction")]
    public string? WindDirection { get; set; }

    [JsonPropertyName("wind_power")]
    public string? WindPower { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("report_time")]
    public string? ReportTime { get; set; }

    [JsonPropertyName("feels_like")]
    public double? FeelsLike { get; set; }

    [JsonPropertyName("visibility")]
    public double? Visibility { get; set; }

    [JsonPropertyName("pressure")]
    public double? Pressure { get; set; }

    [JsonPropertyName("uv")]
    public double? Uv { get; set; }

    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; set; }

    [JsonPropertyName("cloud")]
    public double? Cloud { get; set; }

    [JsonPropertyName("aqi")]
    public int? Aqi { get; set; }

    [JsonPropertyName("aqi_level")]
    public int? AqiLevel { get; set; }

    [JsonPropertyName("aqi_category")]
    public string? AqiCategory { get; set; }

    [JsonPropertyName("aqi_primary")]
    public string? AqiPrimary { get; set; }

    [JsonPropertyName("temp_max")]
    public double? TempMax { get; set; }

    [JsonPropertyName("temp_min")]
    public double? TempMin { get; set; }

    [JsonPropertyName("forecast")]
    public UapiForecast[]? Forecast { get; set; }

    [JsonPropertyName("hourly_forecast")]
    public UapiHourly[]? HourlyForecast { get; set; }

    [JsonPropertyName("life_indices")]
    public UapiLifeIndices? LifeIndices { get; set; }
}

public class UapiForecast
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("week")]
    public string? Week { get; set; }

    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }

    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }

    [JsonPropertyName("weather_day")]
    public string? WeatherDay { get; set; }

    [JsonPropertyName("weather_night")]
    public string? WeatherNight { get; set; }

    [JsonPropertyName("wind_dir_day")]
    public string? WindDirDay { get; set; }

    [JsonPropertyName("wind_dir_night")]
    public string? WindDirNight { get; set; }

    [JsonPropertyName("wind_scale_day")]
    public string? WindScaleDay { get; set; }

    [JsonPropertyName("wind_scale_night")]
    public string? WindScaleNight { get; set; }

    [JsonPropertyName("sunrise")]
    public string? Sunrise { get; set; }

    [JsonPropertyName("sunset")]
    public string? Sunset { get; set; }
}

public class UapiHourly
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("weather")]
    public string? Weather { get; set; }

    [JsonPropertyName("wind_direction")]
    public string? WindDirection { get; set; }

    [JsonPropertyName("wind_speed")]
    public double? WindSpeed { get; set; }

    [JsonPropertyName("wind_scale")]
    public string? WindScale { get; set; }

    [JsonPropertyName("humidity")]
    public int? Humidity { get; set; }

    [JsonPropertyName("precip")]
    public double? Precip { get; set; }

    [JsonPropertyName("pressure")]
    public double? Pressure { get; set; }

    [JsonPropertyName("cloud")]
    public double? Cloud { get; set; }

    [JsonPropertyName("feels_like")]
    public double? FeelsLike { get; set; }

    [JsonPropertyName("pop")]
    public int? Pop { get; set; }
}

public class UapiLifeIndices
{
    [JsonPropertyName("clothing")]
    public UapiLifeIndex? Clothing { get; set; }

    [JsonPropertyName("uv")]
    public UapiLifeIndex? Uv { get; set; }

    [JsonPropertyName("car_wash")]
    public UapiLifeIndex? CarWash { get; set; }

    [JsonPropertyName("exercise")]
    public UapiLifeIndex? Exercise { get; set; }

    [JsonPropertyName("travel")]
    public UapiLifeIndex? Travel { get; set; }

    [JsonPropertyName("umbrella")]
    public UapiLifeIndex? Umbrella { get; set; }
}

public class UapiLifeIndex
{
    [JsonPropertyName("level")]
    public string? Level { get; set; }

    [JsonPropertyName("brief")]
    public string? Brief { get; set; }

    [JsonPropertyName("advice")]
    public string? Advice { get; set; }
}

public class IpLocationResponse
{
    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("adcode")]
    public string? Adcode { get; set; }
}

#endregion
