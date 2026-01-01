using System;
using System.Collections.Generic;
using System.IO;
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

    public class ConfigManager
    {
        private readonly string _configFilePath;

        public ConfigManager(string configFilePath)
        {
            _configFilePath = configFilePath;
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
    }
}
