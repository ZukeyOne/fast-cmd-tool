using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FastTools
{
    public partial class MainWindow : Window
    {
        private readonly string _requestsFile;
        private List<RequestItem> _requests = new();
        private static readonly SemaphoreSlim _executionSemaphore = new SemaphoreSlim(1);

        private enum ExecutionStatus { Waiting, Executing, Completed }

        private Expander CreateRequestExpander(RequestItem request)
        {
            var headerBlock = new TextBlock();
            headerBlock.Inlines.Add(new Run("⏳ ") { FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Microsoft YaHei") });
            headerBlock.Inlines.Add(new Run(request.Alias));
            
            var expander = new Expander
            {
                Header = headerBlock,
                IsExpanded = false
            };
            var textBox = new RichTextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei"),
                FontSize = 12
            };
            expander.Content = textBox;
            return expander;
        }

        public MainWindow()
        {
            InitializeComponent();
            _requestsFile = Path.Combine(AppContext.BaseDirectory, "commands.json");
            Loaded += MainWindow_Loaded;
        }

        private ADBDeviceManager.DeviceInfo? _selectedDevice;
        private readonly ADBDeviceManager _adbDeviceManager = new ADBDeviceManager();

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadRequestsAsync();
            RefreshRequestButtons();

            // 检查管理员权限，如果是管理员则隐藏管理员权限说明
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            AdminNote.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

            // 注册设备更新事件
            _adbDeviceManager.DevicesUpdated += OnDevicesUpdated;

            // 初始化设备检测
            await _adbDeviceManager.UpdateDeviceListAsync();
            
            // 初始化USB设备监听
            _adbDeviceManager.InitializeUsbDeviceMonitoring();
        }

        // 设备更新事件处理
        private void OnDevicesUpdated(object? sender, List<ADBDeviceManager.DeviceInfo> devices)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDeviceUI(devices);
            });
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            OutputPanel.Children.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 释放ADB设备管理器资源
            _adbDeviceManager.Dispose();
        }



        private void UpdateDeviceUI(List<ADBDeviceManager.DeviceInfo> devices)
        {
            DevicePanel.Children.Clear();
            
            if (devices.Count == 0)
            {
                var textBlock = new TextBlock
                {
                    Text = "未检测到设备",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                DevicePanel.Children.Add(textBlock);
                _selectedDevice = null;
                RefreshRequestButtons(); // 更新按钮状态
                return;
            }
            
            foreach (var device in devices)
            {
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                var radioButton = new RadioButton
                {
                    Content = device.DeviceId,
                    Foreground = Brushes.Green,
                    GroupName = "Devices",
                    Margin = new Thickness(0, 0, 8, 0),
                    IsChecked = _selectedDevice?.DeviceId == device.DeviceId
                };
                
                radioButton.Checked += (s, e) =>
                {
                    _selectedDevice = device;
                    RefreshRequestButtons(); // 更新按钮状态
                };
                
                stackPanel.Children.Add(radioButton);
                
                // 显示root状态
                var rootIndicator = new TextBlock
                {
                    Text = device.IsRooted ? "[Rooted]" : "[Non-Rooted]",
                    Foreground = device.IsRooted ? Brushes.Orange : Brushes.Gray,
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                stackPanel.Children.Add(rootIndicator);
                
                // 显示remount状态
                var remountIndicator = new TextBlock
                {
                    Text = device.IsRemounted ? "[Remounted]" : "[Non-Remounted]",
                    Foreground = device.IsRemounted ? Brushes.Blue : Brushes.Gray,
                    FontSize = 10
                };
                stackPanel.Children.Add(remountIndicator);
                
                DevicePanel.Children.Add(stackPanel);
            }
            
            // 如果没有选中设备且有设备可用，则默认选择第一个设备
            if (_selectedDevice == null || !devices.Any(d => d.DeviceId == _selectedDevice.DeviceId))
            {
                _selectedDevice = devices.FirstOrDefault();
                RefreshRequestButtons(); // 更新按钮状态
            }
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        private static Encoding GetOemEncoding()
        {
            try
            {
                var cp = (int)GetOEMCP();
                return Encoding.GetEncoding(cp);
            }
            catch
            {
                return Encoding.Default;
            }
        }

        private async Task ExecuteRequestAsync(RequestItem request, Expander expander)
        {
            var textBox = expander.Content as RichTextBox;
            if (textBox == null) return;
            var headerBlock = new TextBlock();
            headerBlock.Inlines.Add(new Run("🔄 ") { FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Microsoft YaHei") });
            headerBlock.Inlines.Add(new Run(request.Alias));
            expander.Header = headerBlock;
            Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"--- 开始执行任务: {request.Alias} ---"))));
            foreach (var step in request.Steps)
            {
                if (step.Type == "command")
                {
                    Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"----- 命令:{step.Value} -----"))));
                    await ExecuteCommandAsync(step.Value, textBox);
                }
                else if (step.Type == "adb_command")
                {
                    // 替换{dev}占位符为选中的设备ID
                    var command = step.Value.Replace("{dev}", _selectedDevice?.DeviceId ?? "");
                    Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"----- ADB命令:{command} -----"))));
                    await ExecuteCommandAsync(command, textBox);
                }
                else if (step.Type == "delay")
                {
                    if (int.TryParse(step.Value, out int delay))
                    {
                        Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"----- 延时:{delay} ms -----"))));
                        await Task.Delay(delay);
                    }
                    else
                    {
                        Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"----- 无效延时值: {step.Value} -----"))));
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"未知步骤类型: {step.Type}"))));
                }
            }
            Dispatcher.Invoke(() => textBox.Document.Blocks.Add(new Paragraph(new Run($"--- 任务完成 ---"))));
            var completedHeaderBlock = new TextBlock();
            completedHeaderBlock.Inlines.Add(new Run("✅ ") { FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Microsoft YaHei") });
            completedHeaderBlock.Inlines.Add(new Run(request.Alias));
            expander.Header = completedHeaderBlock;
        }

        private async Task ExecuteCommandAsync(string command, RichTextBox outputBox)
        {
            if (outputBox == null) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c chcp 65001 >nul && " + command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => outputBox.Document.Blocks.Add(new Paragraph(new Run(e.Data))));
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => outputBox.Document.Blocks.Add(new Paragraph(new Run(e.Data))));
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => outputBox.Document.Blocks.Add(new Paragraph(new Run($"错误: {ex.Message}"))));
            }
        }

        private void RefreshRequestButtons()
        {
            BtnPanel.Children.Clear();
            foreach (var item in _requests)
            {
                var b = new Button { Content = item.Alias, Margin = new Thickness(0,0,0,6), ToolTip = string.Join("; ", item.Steps.Select(s => $"{s.Type}: {s.Value}")) };
                
                // 检查请求是否包含adb_command步骤
                bool hasAdbCommand = item.Steps.Any(step => step.Type == "adb_command");
                
                // 如果包含adb_command但没有选中设备，则禁用按钮
                b.IsEnabled = !hasAdbCommand || (_selectedDevice != null);
                
                b.Click += async (s, e) =>
                {
                    var expander = CreateRequestExpander(item);
                    OutputPanel.Children.Add(expander);
                    var headerBlock = new TextBlock();
                    headerBlock.Inlines.Add(new Run("⏳ ") { FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Microsoft YaHei") });
                    headerBlock.Inlines.Add(new Run(item.Alias));
                    expander.Header = headerBlock;
                    await _executionSemaphore.WaitAsync();
                    try
                    {
                        // 在执行请求前检查设备状态
                        if (_selectedDevice != null)
                        {
                            // 重新检查当前选中设备的root和remount状态
                            _selectedDevice.IsRooted = await _adbDeviceManager.CheckRootStatusAsync(_selectedDevice.DeviceId);
                            _selectedDevice.IsRemounted = await _adbDeviceManager.CheckRemountStatusAsync(_selectedDevice.DeviceId);
                            // 更新设备UI显示
                            await _adbDeviceManager.UpdateDeviceListAsync();
                        }
                        await ExecuteRequestAsync(item, expander);
                    }
                    finally
                    {
                        _executionSemaphore.Release();
                    }
                };

                var menu = new ContextMenu();
                var mi = new MenuItem { Header = "删除" };
                mi.Click += async (s, e) =>
                {
                    if (MessageBox.Show($"删除请求 '{item.Alias}' ?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        _requests.Remove(item);
                        await SaveRequestsAsync();
                        RefreshRequestButtons();
                    }
                };
                menu.Items.Add(mi);
                b.ContextMenu = menu;

                BtnPanel.Children.Add(b);
            }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                if (!File.Exists(_requestsFile))
                {
                    _requests = new List<RequestItem>();
                    return;
                }
                var txt = await File.ReadAllTextAsync(_requestsFile, Encoding.UTF8);
                _requests = JsonSerializer.Deserialize<List<RequestItem>>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<RequestItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置文件失败: {ex.Message}");
                _requests = new List<RequestItem>();
            }
        }

        private async Task SaveRequestsAsync()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_requests, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_requestsFile, txt, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败: " + ex.Message);
            }
        }

        private class RequestItem
        {
            public string Alias { get; set; } = string.Empty;
            public List<StepItem> Steps { get; set; } = new();
        }

        private class StepItem
        {
            public string Type { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
