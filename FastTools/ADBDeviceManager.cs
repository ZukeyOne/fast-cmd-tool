using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace FastTools
{
    /// <summary>
    /// ADB设备管理器，负责检测和管理ADB设备
    /// </summary>
    public class ADBDeviceManager
    {
        // 设备信息类
        public class DeviceInfo
        {
            public string DeviceId { get; set; }
            public string Status { get; set; }
            public bool IsRooted { get; set; }
            public bool IsRemounted { get; set; }

            public DeviceInfo(string deviceId, string status)
            {
                DeviceId = deviceId;
                Status = status;
                IsRooted = false;
                IsRemounted = false;
            }
        }

        // USB设备监听
        private ManagementEventWatcher? _usbInsertWatcher;
        private ManagementEventWatcher? _usbRemoveWatcher;
        private System.Threading.Timer? _usbDelayTimer;
        private bool _isCheckingDevices = false;

        // 事件定义
        public event EventHandler<List<DeviceInfo>>? DevicesUpdated;

        /// <summary>
        /// 初始化USB设备监听
        /// </summary>
        public void InitializeUsbDeviceMonitoring()
        {
            try
            {
                // 监听USB设备插入事件
                var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 0.5 WHERE TargetInstance ISA 'Win32_USBHub'");
                _usbInsertWatcher = new ManagementEventWatcher(insertQuery);
                _usbInsertWatcher.EventArrived += UsbDeviceChanged;
                _usbInsertWatcher.Start();

                // 监听USB设备拔出事件
                var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 0.5 WHERE TargetInstance ISA 'Win32_USBHub'");
                _usbRemoveWatcher = new ManagementEventWatcher(removeQuery);
                _usbRemoveWatcher.EventArrived += UsbDeviceChanged;
                _usbRemoveWatcher.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"USB设备监听初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// USB设备变化事件处理
        /// </summary>
        private void UsbDeviceChanged(object sender, EventArrivedEventArgs e)
        {
            try
            {
                // 取消之前的延迟定时器
                _usbDelayTimer?.Dispose();
                
                // 创建新的延迟定时器，1秒后触发设备检查
                _usbDelayTimer = new System.Threading.Timer(async _ =>
                {
                    await UpdateDeviceListAsync();
                }, null, TimeSpan.FromSeconds(1), TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"USB设备变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新设备列表
        /// </summary>
        public async Task UpdateDeviceListAsync()
        {
            if (_isCheckingDevices)
                return;

            try
            {
                _isCheckingDevices = true;
                // 执行adb devices命令检测设备
                var result = await ExecuteAdbCommandAsync("devices");
                
                // 解析设备列表
                var devices = ParseAdbDevicesOutput(result);
                
                // 检测每个设备的root和remount状态
                foreach (var device in devices)
                {
                    device.IsRooted = await CheckRootStatusAsync(device.DeviceId);
                    device.IsRemounted = await CheckRemountStatusAsync(device.DeviceId);
                }
                
                // 触发设备更新事件
                DevicesUpdated?.Invoke(this, devices);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检测设备失败: {ex.Message}");
            }
            finally
            {
                _isCheckingDevices = false;
            }
        }

        /// <summary>
        /// 检查设备是否已root
        /// </summary>
        public async Task<bool> CheckRootStatusAsync(string deviceId)
        {
            try
            {
                var result = await ExecuteAdbCommandAsync($"-s {deviceId} shell id");
                return result.Contains("uid=0(root)");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查设备是否已remount
        /// </summary>
        public async Task<bool> CheckRemountStatusAsync(string deviceId)
        {
            try
            {
                // 使用 df -h 获取挂载信息，检查 /vendor 和 /system 是否挂载在包含 "overlay" 的设备上
                var result = await ExecuteAdbCommandAsync($"-s {deviceId} shell df -h");
                var lines = result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                return lines.Any(line =>
                    (line.Contains("/vendor") || line.Contains("/system")) &&
                    line.Contains("overlay"));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 执行ADB命令
        /// </summary>
        public async Task<string> ExecuteAdbCommandAsync(string command)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }

        /// <summary>
        /// 解析ADB设备输出
        /// </summary>
        private List<DeviceInfo> ParseAdbDevicesOutput(string output)
        {
            var devices = new List<DeviceInfo>();
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            
            // 跳过第一行 "List of devices attached"
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                // 分割设备ID和状态
                var parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    var deviceId = parts[0];
                    var status = parts.Length > 1 ? parts[1] : "未知状态";
                    devices.Add(new DeviceInfo(deviceId, status));
                }
            }
            return devices;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放USB监听资源
            _usbInsertWatcher?.Stop();
            _usbInsertWatcher?.Dispose();
            _usbRemoveWatcher?.Stop();
            _usbRemoveWatcher?.Dispose();
            _usbDelayTimer?.Dispose();
        }
    }
}