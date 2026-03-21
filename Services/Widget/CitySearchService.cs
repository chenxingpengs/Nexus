using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.Widget
{
    public class CityInfo
    {
        public string CityId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
    }

    public class CitySearchService
    {
        private List<CityInfo> _cities = new();
        private bool _isLoaded;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public CitySearchService()
        {
            LoadDefaultCities();
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadCitiesFromFileAsync();
                }
                catch
                {
                }
            });
        }

        private async Task LoadCitiesFromFileAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                if (_isLoaded) return;

                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Nexus.Data.Cities.json";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var cities = JsonSerializer.Deserialize<List<CityInfo>>(json, options);
                    if (cities != null && cities.Count > 0)
                    {
                        _cities = cities;
                    }
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCities error: {ex.Message}");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private void LoadDefaultCities()
        {
            _cities = new List<CityInfo>
            {
                new CityInfo { CityId = "101010100", Name = "北京", Pinyin = "beijing", Province = "北京" },
                new CityInfo { CityId = "101020100", Name = "上海", Pinyin = "shanghai", Province = "上海" },
                new CityInfo { CityId = "101280101", Name = "广州", Pinyin = "guangzhou", Province = "广东" },
                new CityInfo { CityId = "101280601", Name = "深圳", Pinyin = "shenzhen", Province = "广东" },
                new CityInfo { CityId = "101110101", Name = "西安", Pinyin = "xian", Province = "陕西" },
                new CityInfo { CityId = "101120101", Name = "济南", Pinyin = "jinan", Province = "山东" },
                new CityInfo { CityId = "101130101", Name = "乌鲁木齐", Pinyin = "wulumuqi", Province = "新疆" },
                new CityInfo { CityId = "101140101", Name = "太原", Pinyin = "taiyuan", Province = "山西" },
                new CityInfo { CityId = "101150101", Name = "西宁", Pinyin = "xining", Province = "青海" },
                new CityInfo { CityId = "101160101", Name = "兰州", Pinyin = "lanzhou", Province = "甘肃" },
                new CityInfo { CityId = "101170101", Name = "银川", Pinyin = "yinchuan", Province = "宁夏" },
                new CityInfo { CityId = "101180101", Name = "郑州", Pinyin = "zhengzhou", Province = "河南" },
                new CityInfo { CityId = "101190101", Name = "合肥", Pinyin = "hefei", Province = "安徽" },
                new CityInfo { CityId = "101200101", Name = "武汉", Pinyin = "wuhan", Province = "湖北" },
                new CityInfo { CityId = "101210101", Name = "长沙", Pinyin = "changsha", Province = "湖南" },
                new CityInfo { CityId = "101220101", Name = "南昌", Pinyin = "nanchang", Province = "江西" },
                new CityInfo { CityId = "101230101", Name = "南京", Pinyin = "nanjing", Province = "江苏" },
                new CityInfo { CityId = "101240101", Name = "成都", Pinyin = "chengdu", Province = "四川" },
                new CityInfo { CityId = "101250101", Name = "贵阳", Pinyin = "guiyang", Province = "贵州" },
                new CityInfo { CityId = "101260101", Name = "昆明", Pinyin = "kunming", Province = "云南" },
                new CityInfo { CityId = "101270101", Name = "拉萨", Pinyin = "lasa", Province = "西藏" },
                new CityInfo { CityId = "101280701", Name = "珠海", Pinyin = "zhuhai", Province = "广东" },
                new CityInfo { CityId = "101280702", Name = "斗门", Pinyin = "doumen", Province = "广东" },
                new CityInfo { CityId = "101280703", Name = "金湾", Pinyin = "jinwan", Province = "广东" },
                new CityInfo { CityId = "101280401", Name = "汕头", Pinyin = "shantou", Province = "广东" },
                new CityInfo { CityId = "101280501", Name = "佛山", Pinyin = "foshan", Province = "广东" },
                new CityInfo { CityId = "101280801", Name = "惠州", Pinyin = "huizhou", Province = "广东" },
                new CityInfo { CityId = "101280901", Name = "东莞", Pinyin = "dongguan", Province = "广东" },
                new CityInfo { CityId = "101281601", Name = "中山", Pinyin = "zhongshan", Province = "广东" },
                new CityInfo { CityId = "101290101", Name = "南宁", Pinyin = "nanning", Province = "广西" },
                new CityInfo { CityId = "101300101", Name = "海口", Pinyin = "haikou", Province = "海南" },
                new CityInfo { CityId = "101310101", Name = "石家庄", Pinyin = "shijiazhuang", Province = "河北" },
                new CityInfo { CityId = "101320101", Name = "福州", Pinyin = "fuzhou", Province = "福建" },
                new CityInfo { CityId = "101330101", Name = "杭州", Pinyin = "hangzhou", Province = "浙江" },
                new CityInfo { CityId = "101340101", Name = "沈阳", Pinyin = "shenyang", Province = "辽宁" },
                new CityInfo { CityId = "101350101", Name = "长春", Pinyin = "changchun", Province = "吉林" },
                new CityInfo { CityId = "101360101", Name = "哈尔滨", Pinyin = "haerbin", Province = "黑龙江" },
                new CityInfo { CityId = "101370101", Name = "呼和浩特", Pinyin = "huhehaote", Province = "内蒙古" },
                new CityInfo { CityId = "101380101", Name = "天津", Pinyin = "tianjin", Province = "天津" },
                new CityInfo { CityId = "101390101", Name = "重庆", Pinyin = "chongqing", Province = "重庆" }
            };
        }

        public async Task<List<CityInfo>> SearchCitiesAsync(string keyword)
        {
            await _loadLock.WaitAsync();
            _loadLock.Release();

            System.Diagnostics.Debug.WriteLine($"[CitySearch] 城市列表数量: {_cities.Count}");
            System.Diagnostics.Debug.WriteLine($"[CitySearch] 前5个城市: {string.Join(", ", _cities.Take(5).Select(c => c.Name))}");

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return _cities.Take(20).ToList();
            }

            var lowerKeyword = keyword.ToLower();
            
            var results = _cities
                .Where(c => 
                    c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    c.Pinyin.Contains(lowerKeyword, StringComparison.OrdinalIgnoreCase) ||
                    c.CityId.Contains(keyword))
                .Take(20)
                .ToList();
                
            System.Diagnostics.Debug.WriteLine($"[CitySearch] 搜索 '{keyword}' 结果: {results.Count}");
            
            return results;
        }

        public CityInfo? GetCityById(string cityId)
        {
            return _cities.FirstOrDefault(c => c.CityId == cityId);
        }

        public CityInfo? GetCityByName(string name)
        {
            return _cities.FirstOrDefault(c => 
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
