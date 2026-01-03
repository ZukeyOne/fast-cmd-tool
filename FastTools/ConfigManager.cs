using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FastTools
{
    public class RequestItem
    {
        public string Alias { get; set; } = string.Empty;
        public List<StepItem> Steps { get; set; } = new();
    }

    public class StepItem
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool? LocalDir { get; set; }
    }

    public class CommandsConfig
    {
        public string WorkDir { get; set; } = string.Empty;
        public List<RequestItem> Requests { get; set; } = new();
    }

    public class HistoryItem
    {
        public string Alias { get; set; } = string.Empty;
        public List<StepItem> Steps { get; set; } = new();
        public DateTime ExecuteTime { get; set; }
    }

    public class ConfigManager
    {
        private readonly string _configFilePath;
        private readonly string _historyFilePath;

        public ConfigManager(string configFilePath)
        {
            _configFilePath = configFilePath;
            var directory = Path.GetDirectoryName(configFilePath);
            _historyFilePath = Path.Combine(directory ?? "", "history.json");
        }

        public async Task<CommandsConfig> LoadConfigAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    return new CommandsConfig();
                }

                var txt = await File.ReadAllTextAsync(_configFilePath, Encoding.UTF8);
                var jsonDoc = JsonDocument.Parse(txt);

                var config = new CommandsConfig();

                if (jsonDoc.RootElement.TryGetProperty("work_dir", out var workDirElement))
                {
                    config.WorkDir = workDirElement.GetString() ?? string.Empty;
                }

                if (jsonDoc.RootElement.TryGetProperty("requests", out var requestsElement))
                {
                    config.Requests = JsonSerializer.Deserialize<List<RequestItem>>(requestsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<RequestItem>();
                }

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置文件失败: {ex.Message}");
                return new CommandsConfig();
            }
        }

        public async Task SaveConfigAsync(CommandsConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = new
                {
                    work_dir = config.WorkDir,
                    requests = config.Requests
                };
                var txt = JsonSerializer.Serialize(json, options);
                await File.WriteAllTextAsync(_configFilePath, txt, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("保存配置失败: " + ex.Message);
            }
        }

        public async Task AddRequestAsync(RequestItem request)
        {
            var config = await LoadConfigAsync();
            config.Requests.Add(request);
            await SaveConfigAsync(config);
        }

        public async Task RemoveRequestAsync(RequestItem request)
        {
            var config = await LoadConfigAsync();
            config.Requests.Remove(request);
            await SaveConfigAsync(config);
        }

        public async Task AddHistoryAsync(HistoryItem history)
        {
            var historyList = await LoadHistoryAsync();
            historyList.Insert(0, history);
            if (historyList.Count > 50)
            {
                historyList = historyList.Take(50).ToList();
            }
            await SaveHistoryAsync(historyList);
        }

        public async Task ClearHistoryAsync()
        {
            await SaveHistoryAsync(new List<HistoryItem>());
        }

        public async Task<List<HistoryItem>> LoadHistoryAsync()
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    return new List<HistoryItem>();
                }

                var txt = await File.ReadAllTextAsync(_historyFilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<HistoryItem>>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<HistoryItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载历史记录文件失败: {ex.Message}");
                return new List<HistoryItem>();
            }
        }

        public async Task SaveHistoryAsync(List<HistoryItem> history)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var txt = JsonSerializer.Serialize(history, options);
                await File.WriteAllTextAsync(_historyFilePath, txt, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("保存历史记录失败: " + ex.Message);
            }
        }
    }
}
