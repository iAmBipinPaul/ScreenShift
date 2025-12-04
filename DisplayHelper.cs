using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace MonitorSwitcher
{
    public static class DisplayHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        // SetDisplayConfig API - modern Windows display configuration
        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, out DISPLAYCONFIG_TOPOLOGY_ID currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(uint numPathArrayElements, DISPLAYCONFIG_PATH_INFO[]? pathArray,
            uint numModeInfoArrayElements, DISPLAYCONFIG_MODE_INFO[]? modeInfoArray, uint flags);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
        
        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);
        
        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const uint CDS_UPDATEREGISTRY = 0x01;
        private const uint CDS_SET_PRIMARY = 0x10;
        private const uint CDS_NORESET = 0x10000000;
        private const uint CDS_RESET = 0x40000000;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        
        // SetDisplayConfig flags
        private const uint QDC_ALL_PATHS = 0x00000001;
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_TOPOLOGY_SUPPLIED = 0x00000001;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        private const uint SDC_ALLOW_CHANGES = 0x00000400;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
        private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;
        private const uint DISPLAY_DEVICE_ACTIVE = 0x1;
        private const uint DM_POSITION = 0x20;
        private const uint DM_ORIENTATION = 0x80;
        private const uint DM_PELSWIDTH = 0x80000;
        private const uint DM_PELSHEIGHT = 0x100000;
        private const uint DM_DISPLAYFREQUENCY = 0x400000;
        private const uint DM_BITSPERPEL = 0x40000;

        // Structures for SetDisplayConfig API
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public uint targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] modeInfo;
        }

        private enum DISPLAYCONFIG_TOPOLOGY_ID : uint
        {
            DISPLAYCONFIG_TOPOLOGY_INTERNAL = 0x00000001,
            DISPLAYCONFIG_TOPOLOGY_CLONE = 0x00000002,
            DISPLAYCONFIG_TOPOLOGY_EXTEND = 0x00000004,
            DISPLAYCONFIG_TOPOLOGY_EXTERNAL = 0x00000008
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }
        
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

        public static List<MonitorInfo> GetAllDisplays()
        {
            var monitors = new List<MonitorInfo>();
            
            // Get monitor names and display numbers using DisplayConfig API (same as Windows Settings)
            var displayConfigInfo = GetDisplayConfigInfo();
            
            // Get all WMI monitors for disabled monitor detection
            var allWmiMonitors = GetAllWmiMonitors();
            var laptopMonitor = allWmiMonitors.FirstOrDefault(m => string.IsNullOrWhiteSpace(m.FriendlyName));
            
            // Track which display numbers are used by enabled displays
            var usedDisplayNumbers = new HashSet<int>();

            // Get active displays using DisplayConfig info
            foreach (var (gdiName, monitorName, displayNum) in displayConfigInfo)
            {
                usedDisplayNumbers.Add(displayNum);
                
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                if (EnumDisplaySettings(gdiName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    // Check if primary
                    var device = new DISPLAY_DEVICE();
                    device.cb = Marshal.SizeOf(device);
                    bool isPrimary = false;
                    uint deviceNum = 0;
                    while (EnumDisplayDevices(null, deviceNum, ref device, 0))
                    {
                        if (device.DeviceName == gdiName)
                        {
                            isPrimary = (device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                            break;
                        }
                        deviceNum++;
                        device.cb = Marshal.SizeOf(device);
                    }
                    
                    string orientation = devMode.dmDisplayOrientation switch
                    {
                        0 => "Landscape",
                        1 => "Portrait",
                        2 => "Landscape (flipped)",
                        3 => "Portrait (flipped)",
                        _ => "Unknown"
                    };

                    monitors.Add(new MonitorInfo
                    {
                        DeviceName = monitorName,
                        AdapterName = "Intel(R) UHD Graphics 630",
                        DeviceKey = gdiName,
                        DisplayNumber = displayNum.ToString(),
                        Width = (int)devMode.dmPelsWidth,
                        Height = (int)devMode.dmPelsHeight,
                        RefreshRate = (int)devMode.dmDisplayFrequency,
                        Orientation = orientation,
                        IsPrimary = isPrimary,
                        IsEnabled = true,
                        PositionX = devMode.dmPositionX,
                        PositionY = devMode.dmPositionY
                    });
                }
            }

            // Add disabled monitors - find gaps in display numbers or extra WMI monitors
            int totalWmiMonitors = allWmiMonitors.Count;
            int enabledCount = displayConfigInfo.Count;
            
            if (totalWmiMonitors > enabledCount)
            {
                // Find which display numbers are missing (disabled)
                // Check numbers 1 through totalWmiMonitors
                for (int i = 1; i <= totalWmiMonitors; i++)
                {
                    if (!usedDisplayNumbers.Contains(i))
                    {
                        string disabledName = laptopMonitor?.Name ?? "Unknown Monitor";
                        
                        monitors.Add(new MonitorInfo
                        {
                            DeviceName = disabledName,
                            AdapterName = "Disabled",
                            DeviceKey = "",
                            DisplayNumber = i.ToString(),
                            Width = 0,
                            Height = 0,
                            RefreshRate = 0,
                            Orientation = "N/A",
                            IsPrimary = false,
                            IsEnabled = false
                        });
                    }
                }
            }

            // Sort by display number to match Windows Settings order
            return monitors.OrderBy(m => int.TryParse(m.DisplayNumber, out int n) ? n : 999).ToList();
        }
        
        /// <summary>
        /// Gets monitor friendly names using DisplayConfig API (same method Windows Settings uses)
        /// </summary>
        private static Dictionary<string, string> GetDisplayConfigMonitorNames()
        {
            var result = new Dictionary<string, string>();
            foreach (var (gdiName, monitorName, _) in GetDisplayConfigInfo())
            {
                result[gdiName] = monitorName;
            }
            return result;
        }
        
        /// <summary>
        /// Gets full display config info: GDI name, monitor name, and display number
        /// </summary>
        private static List<(string GdiName, string MonitorName, int DisplayNumber)> GetDisplayConfigInfo()
        {
            var result = new List<(string, string, int)>();
            
            try
            {
                uint numPaths = 0, numModes = 0;
                GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPaths, out numModes);
                
                if (numPaths == 0) return result;
                
                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
                
                uint pathCount = numPaths, modeCount = numModes;
                int queryResult = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                
                if (queryResult != 0) return result;
                
                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    
                    // Get source name (\\.\DISPLAY#)
                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = path.sourceInfo.adapterId;
                    sourceName.header.id = path.sourceInfo.id;
                    
                    if (DisplayConfigGetDeviceInfo(ref sourceName) != 0) continue;
                    
                    // Get target name (monitor friendly name)
                    var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    targetName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                    targetName.header.adapterId = path.targetInfo.adapterId;
                    targetName.header.id = path.targetInfo.id;
                    
                    if (DisplayConfigGetDeviceInfo(ref targetName) != 0) continue;
                    
                    if (!string.IsNullOrEmpty(sourceName.viewGdiDeviceName) && 
                        !string.IsNullOrEmpty(targetName.monitorFriendlyDeviceName))
                    {
                        // Extract display number from GDI name (\\.\DISPLAY1 -> 1)
                        string numStr = new string(sourceName.viewGdiDeviceName.Where(char.IsDigit).ToArray());
                        int displayNum = int.TryParse(numStr, out int n) ? n : 0;
                        
                        result.Add((sourceName.viewGdiDeviceName, targetName.monitorFriendlyDeviceName, displayNum));
                    }
                }
            }
            catch { }
            
            return result;
        }
        
        private static List<WmiMonitorInfo> GetAllWmiMonitors()
        {
            var monitors = new List<WmiMonitorInfo>();
            try
            {
                // Get ALL monitors (both active and inactive)
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorID");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        string? instanceName = obj["InstanceName"]?.ToString();
                        bool isActive = obj["Active"] as bool? ?? false;
                        string friendlyName = DecodeWmiString(obj["UserFriendlyName"]);
                        string mfgName = DecodeWmiString(obj["ManufacturerName"]);
                        string productCode = DecodeWmiString(obj["ProductCodeID"]);

                        // Build display name - prefer UserFriendlyName
                        string displayName;
                        if (!string.IsNullOrWhiteSpace(friendlyName))
                        {
                            displayName = friendlyName;
                        }
                        else
                        {
                            string mfgFullName = mfgName switch
                            {
                                "DEL" => "DELL",
                                "SHP" => "Sharp",
                                "SAM" => "Samsung",
                                "LGD" or "GSM" => "LG",
                                "ACI" => "ASUS",
                                "ACR" => "Acer",
                                "BNQ" => "BenQ",
                                "HWP" => "HP",
                                "VSC" => "ViewSonic",
                                "AOC" => "AOC",
                                "PHL" => "Philips",
                                _ => mfgName
                            };
                            displayName = !string.IsNullOrEmpty(productCode) 
                                ? $"{mfgFullName} {productCode}" 
                                : mfgFullName;
                        }
                        
                        // Extract monitor ID from instance name
                        string monitorId = "";
                        string uid = "";
                        if (!string.IsNullOrEmpty(instanceName))
                        {
                            var parts = instanceName.Split('\\');
                            if (parts.Length >= 2) monitorId = parts[1];
                            var uidMatch = System.Text.RegularExpressions.Regex.Match(instanceName, @"UID(\d+)");
                            if (uidMatch.Success) uid = uidMatch.Groups[1].Value;
                        }

                        monitors.Add(new WmiMonitorInfo
                        {
                            MonitorId = monitorId,
                            Name = displayName,
                            FriendlyName = friendlyName,
                            Uid = uid,
                            IsActive = isActive
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return monitors;
        }



        public static string GetDebugInfo()
        {
            var sb = new StringBuilder();
            var wmiMonitors = GetAllWmiMonitors();
            sb.AppendLine("=== WMI Monitors ===");
            foreach (var m in wmiMonitors)
            {
                sb.AppendLine($"  {m.MonitorId} = {m.Name} (Active: {m.IsActive})");
            }
            
            sb.AppendLine("\n=== Display Devices ===");
            var device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);
            uint deviceNum = 0;
            while (EnumDisplayDevices(null, deviceNum, ref device, 0))
            {
                if ((device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    var monitorDevice = new DISPLAY_DEVICE();
                    monitorDevice.cb = Marshal.SizeOf(monitorDevice);
                    if (EnumDisplayDevices(device.DeviceName, 0, ref monitorDevice, 0))
                    {
                        sb.AppendLine($"  Adapter: {device.DeviceName}");
                        sb.AppendLine($"    Monitor DeviceID: {monitorDevice.DeviceID}");
                    }
                }
                deviceNum++;
                device.cb = Marshal.SizeOf(device);
            }
            return sb.ToString();
        }



        private class WmiMonitorInfo
        {
            public string MonitorId { get; set; } = "";
            public string Name { get; set; } = "";
            public string FriendlyName { get; set; } = ""; // Raw UserFriendlyName from WMI
            public string Uid { get; set; } = "";
            public bool IsActive { get; set; }
        }



        private static string DecodeWmiString(object? value)
        {
            if (value == null) return "";
            
            // Try ushort[] first (most common)
            if (value is ushort[] ushortArray)
            {
                return new string(ushortArray.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
            }
            
            // Try byte[] 
            if (value is byte[] byteArray)
            {
                return Encoding.ASCII.GetString(byteArray).TrimEnd('\0').Trim();
            }
            
            // Try int[] or uint[]
            if (value is int[] intArray)
            {
                return new string(intArray.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
            }
            
            // Try as generic array
            if (value is Array arr)
            {
                var chars = new List<char>();
                foreach (var item in arr)
                {
                    int val = Convert.ToInt32(item);
                    if (val == 0) break;
                    chars.Add((char)val);
                }
                return new string(chars.ToArray()).Trim();
            }
            
            return value.ToString()?.Trim() ?? "";
        }




        private const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;

        public static bool SetPrimaryDisplay(string deviceName)
        {
            try
            {
                uint numPaths = 0, numModes = 0;
                int result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPaths, out numModes);
                if (result != 0) return false;

                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];

                result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPaths, paths, ref numModes, modes, IntPtr.Zero);
                if (result != 0) return false;

                // Find the target path
                int offsetX = 0;
                int offsetY = 0;
                bool found = false;

                for (int i = 0; i < numPaths; i++)
                {
                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = paths[i].sourceInfo.adapterId;
                    sourceName.header.id = paths[i].sourceInfo.id;

                    if (DisplayConfigGetDeviceInfo(ref sourceName) == 0)
                    {
                        if (sourceName.viewGdiDeviceName == deviceName)
                        {
                            // Found the new primary
                            uint modeIdx = paths[i].sourceInfo.modeInfoIdx;
                            if (modeIdx < numModes && modes[modeIdx].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                            {
                                // Read position from byte array (offset 12 for x, 16 for y)
                                // DISPLAYCONFIG_SOURCE_MODE: width(4), height(4), pixelFormat(4), position(8)
                                offsetX = BitConverter.ToInt32(modes[modeIdx].modeInfo, 12);
                                offsetY = BitConverter.ToInt32(modes[modeIdx].modeInfo, 16);
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found) return false;

                // Update all source positions
                for (int i = 0; i < numModes; i++)
                {
                    if (modes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                    {
                        int x = BitConverter.ToInt32(modes[i].modeInfo, 12);
                        int y = BitConverter.ToInt32(modes[i].modeInfo, 16);

                        x -= offsetX;
                        y -= offsetY;

                        byte[] xBytes = BitConverter.GetBytes(x);
                        byte[] yBytes = BitConverter.GetBytes(y);

                        Array.Copy(xBytes, 0, modes[i].modeInfo, 12, 4);
                        Array.Copy(yBytes, 0, modes[i].modeInfo, 16, 4);
                    }
                }

                // Apply
                result = SetDisplayConfig(numPaths, paths, numModes, modes, 
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);



                return result == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
