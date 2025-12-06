using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;

namespace MonitorSwitcher
{
    /// Display helper using only modern Windows 11 APIs:
    /// - Windows.Devices.Display.DisplayMonitor (WinRT) for monitor enumeration
    /// - QueryDisplayConfig/SetDisplayConfig (CCD API) for display configuration
    /// 
    /// Note: This helper explicitly treats the Internal Display as #1 to match Windows Settings.
    /// </summary>
    public static class DisplayHelper
    {
        #region Modern CCD API Imports
        
        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, 
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, 
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(uint numPathArrayElements, DISPLAYCONFIG_PATH_INFO[]? pathArray,
            uint numModeInfoArrayElements, DISPLAYCONFIG_MODE_INFO[]? modeInfoArray, uint flags);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
        
        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        #endregion

        #region Constants
        
        private const uint QDC_ALL_PATHS = 0x00000001;
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        private const uint SDC_ALLOW_CHANGES = 0x00000400;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        private const int EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

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
            public bool targetAvailable;
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
        private struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION info;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
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

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets all displays using modern Windows 11 APIs
        /// </summary>
        public static List<MonitorInfo> GetAllDisplays()
        {
            var monitors = new List<MonitorInfo>();
            
            try
            {
                // 1. Get all monitors via WinRT
                var winrtMonitors = GetWinRTMonitorsAsync().GetAwaiter().GetResult();
                
                // 2. Get GDI Device Name mapping (legacy API)
                var deviceIdMap = GetDisplayNumberMapping();

                // 3. Get modern CCD Paths to identify output technology (Internal vs External)
                // We use QDC_ALL_PATHS so we can find the internal panel even if it is disabled/closed
                var techMap = GetTargetTechnologyMapping();
                
                // 4. Get active configuration for positioning/resolution
                var activeDisplays = GetActiveDisplayConfig();
                
                // Sorting Logic:
                // Windows Heuristic: Internal Display is ALWAYS #1. 
                // External displays follow based on connection priority (Target ID).
                
                // Sort by target ID to match typical Windows enumeration (Port Priority)
                winrtMonitors = winrtMonitors.OrderBy(m => m.targetId).ToList();

                var internalMonitors = new List<MonitorInfo>();
                var externalMonitors = new List<MonitorInfo>();

                foreach (var (name, targetId, physicalWidth, physicalHeight, deviceId) in winrtMonitors)
                {
                    // Create basic monitor info
                    var monitor = new MonitorInfo
                    {
                        DeviceName = name,
                        AdapterName = "Disabled", // Default
                        DeviceKey = "",
                        Width = (int)physicalWidth,
                        Height = (int)physicalHeight,
                        RefreshRate = 0,
                        Orientation = "N/A",
                        IsPrimary = false,
                        IsEnabled = false,
                        PositionX = 0,
                        PositionY = 0
                    };

                    // Check output technology to see if this is an Internal Display
                    // 0x80000000 = DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL
                    // 0x0B = DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED (eDP)
                    bool isInternal = false;
                    if (techMap.TryGetValue(targetId, out uint tech))
                    {
                        if (tech == 0x80000000 || tech == 0x0B)
                            isInternal = true;
                    }

                    // Try to match with Active Display Config
                    var activeMatch = activeDisplays.FirstOrDefault(a => a.targetId == targetId);
                    if (activeMatch.gdiName != null)
                    {
                        monitor.DeviceKey = activeMatch.gdiName;
                        monitor.Width = activeMatch.width;
                        monitor.Height = activeMatch.height;
                        monitor.RefreshRate = activeMatch.refreshRate;
                        monitor.Orientation = GetOrientationString(activeMatch.rotation);
                        monitor.IsPrimary = activeMatch.isPrimary;
                        monitor.IsEnabled = true;
                        monitor.PositionX = activeMatch.posX;
                        monitor.PositionY = activeMatch.posY;
                        monitor.AdapterName = "Graphics Adapter";
                    }

                    if (isInternal)
                        internalMonitors.Add(monitor);
                    else
                        externalMonitors.Add(monitor);
                }

                // 5. Assign Display Numbers
                /* 
                   NOTE: We explicitly treat the Internal Display as #1.
                   Windows Settings typically assigns #1 to the internal laptop screen regardless of 
                   its connection ID or port order. We replicate this behavior here to ensure
                   consistency with the OS Settings app.
                */

                int counter = 1;

                // Add Internal Monitors first (usually just one)
                foreach(var m in internalMonitors)
                {
                    m.DisplayNumber = counter.ToString();
                    monitors.Add(m);
                    counter++;
                }

                // Add External Monitors, sorted by Target ID (which proxies for Port Order)
                foreach(var m in externalMonitors)
                {
                    m.DisplayNumber = counter.ToString();
                    monitors.Add(m);
                    counter++;
                }
            }
            catch (Exception)
            {
                // Fallback if WinRT fails - use CCD API only
                return GetDisplaysFromCCD();
            }
            
            return monitors;
        }

        private static Dictionary<uint, uint> GetTargetTechnologyMapping()
        {
            var mapping = new Dictionary<uint, uint>();
            try
            {
                uint numPaths = 0, numModes = 0;
                if (GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out numPaths, out numModes) != 0)
                    return mapping;

                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
                uint pathCount = numPaths, modeCount = numModes;

                if (QueryDisplayConfig(QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) == 0)
                {
                    for (int i = 0; i < pathCount; i++)
                    {
                        var p = paths[i];
                        // Store target ID -> Output Technology
                        if (!mapping.ContainsKey(p.targetInfo.id))
                        {
                            mapping[p.targetInfo.id] = p.targetInfo.outputTechnology;
                        }
                    }
                }
            }
            catch {}
            return mapping;
        }

        /// <summary>
        /// Sets the specified display as primary using modern SetDisplayConfig API
        /// </summary>
        public static bool SetPrimaryDisplay(string gdiDeviceName)
        {
            try
            {
                uint numPaths = 0, numModes = 0;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPaths, out numModes) != 0)
                    return false;

                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];

                uint pathCount = numPaths, modeCount = numModes;
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                    return false;

                // Find paths for the new primary and current primary
                int newPrimaryPathIdx = -1;
                int currentPrimaryPathIdx = -1;
                
                for (int i = 0; i < pathCount; i++)
                {
                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = paths[i].sourceInfo.adapterId;
                    sourceName.header.id = paths[i].sourceInfo.id;

                    if (DisplayConfigGetDeviceInfo(ref sourceName) == 0)
                    {
                        if (sourceName.viewGdiDeviceName == gdiDeviceName)
                            newPrimaryPathIdx = i;
                        
                        // Check if this is current primary (position 0,0)
                        if (paths[i].sourceInfo.modeInfoIdx < modeCount)
                        {
                            var modeInfo = modes[paths[i].sourceInfo.modeInfoIdx];
                            if (modeInfo.infoType == 1) // Source mode
                            {
                                if (modeInfo.info.sourceMode.position.x == 0 && 
                                    modeInfo.info.sourceMode.position.y == 0)
                                {
                                    currentPrimaryPathIdx = i;
                                }
                            }
                        }
                    }
                }

                if (newPrimaryPathIdx < 0 || newPrimaryPathIdx == currentPrimaryPathIdx)
                    return newPrimaryPathIdx == currentPrimaryPathIdx;

                // Get positions
                var newPrimaryModeIdx = paths[newPrimaryPathIdx].sourceInfo.modeInfoIdx;
                var currentPrimaryModeIdx = currentPrimaryPathIdx >= 0 ? paths[currentPrimaryPathIdx].sourceInfo.modeInfoIdx : uint.MaxValue;

                if (newPrimaryModeIdx >= modeCount)
                    return false;

                int newPrimaryX = modes[newPrimaryModeIdx].info.sourceMode.position.x;
                int newPrimaryY = modes[newPrimaryModeIdx].info.sourceMode.position.y;

                // Shift all displays so new primary is at (0,0)
                for (int i = 0; i < modeCount; i++)
                {
                    if (modes[i].infoType == 1) // Source mode
                    {
                        modes[i].info.sourceMode.position.x -= newPrimaryX;
                        modes[i].info.sourceMode.position.y -= newPrimaryY;
                    }
                }

                // Apply configuration
                uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_SAVE_TO_DATABASE | SDC_ALLOW_CHANGES;
                int result = SetDisplayConfig(pathCount, paths, modeCount, modes, flags);
                
                return result == 0;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        #region Private Methods

        private static async Task<List<(string name, uint targetId, uint physicalWidth, uint physicalHeight, string deviceId)>> GetWinRTMonitorsAsync()
        {
            var result = new List<(string, uint, uint, uint, string)>();
            
            var displayMonitors = await DeviceInformation.FindAllAsync(DisplayMonitor.GetDeviceSelector());
            
            foreach (var deviceInfo in displayMonitors)
            {
                var displayMonitor = await DisplayMonitor.FromInterfaceIdAsync(deviceInfo.Id);
                if (displayMonitor != null)
                {
                    string name = displayMonitor.DisplayName;
                    if (string.IsNullOrEmpty(name))
                        name = "Internal Display";
                    
                    uint width = (uint)displayMonitor.NativeResolutionInRawPixels.Width;
                    uint height = (uint)displayMonitor.NativeResolutionInRawPixels.Height;
                    
                    result.Add((name, displayMonitor.DisplayAdapterTargetId, width, height, displayMonitor.DeviceId));
                }
            }
            
            return result;
        }

        private static Dictionary<string, string> GetDisplayNumberMapping()
        {
            var mapping = new Dictionary<string, string>();
            
            try
            {
                DISPLAY_DEVICE d = new DISPLAY_DEVICE();
                d.cb = Marshal.SizeOf(d);
                
                // Enumerate Adapters
                for (uint id = 0; EnumDisplayDevices(null, id, ref d, 0); id++)
                {
                    // Enumerate Monitors for this adapter
                    DISPLAY_DEVICE mon = new DISPLAY_DEVICE();
                    mon.cb = Marshal.SizeOf(mon);
                    
                    if (EnumDisplayDevices(d.DeviceName, 0, ref mon, 0)) // usually index 0 is enough for pnp ID
                    {
                        if (!string.IsNullOrEmpty(mon.DeviceID) && !mapping.ContainsKey(mon.DeviceID))
                        {
                            mapping[mon.DeviceID] = d.DeviceName;
                        }
                    }
                    
                    d.cb = Marshal.SizeOf(d);
                }
            }
            catch { }

            return mapping;
        }

        private static List<(string gdiName, uint targetId, int width, int height, int refreshRate, uint rotation, bool isPrimary, int posX, int posY)> GetActiveDisplayConfig()
        {
            var result = new List<(string, uint, int, int, int, uint, bool, int, int)>();
            
            try
            {
                uint numPaths = 0, numModes = 0;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPaths, out numModes) != 0)
                    return result;

                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];

                uint pathCount = numPaths, modeCount = numModes;
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                    return result;

                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    
                    // Get GDI device name
                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = path.sourceInfo.adapterId;
                    sourceName.header.id = path.sourceInfo.id;
                    
                    if (DisplayConfigGetDeviceInfo(ref sourceName) != 0) continue;
                    
                    string gdiName = sourceName.viewGdiDeviceName;
                    
                    // Get source mode info (resolution and position)
                    int width = 0, height = 0, posX = 0, posY = 0;
                    bool isPrimary = false;
                    
                    if (path.sourceInfo.modeInfoIdx < modeCount)
                    {
                        var sourceMode = modes[path.sourceInfo.modeInfoIdx];
                        if (sourceMode.infoType == 1) // Source mode
                        {
                            width = (int)sourceMode.info.sourceMode.width;
                            height = (int)sourceMode.info.sourceMode.height;
                            posX = sourceMode.info.sourceMode.position.x;
                            posY = sourceMode.info.sourceMode.position.y;
                            isPrimary = (posX == 0 && posY == 0);
                        }
                    }
                    
                    // Get target mode info (refresh rate)
                    int refreshRate = 0;
                    if (path.targetInfo.modeInfoIdx < modeCount)
                    {
                        var targetMode = modes[path.targetInfo.modeInfoIdx];
                        if (targetMode.infoType == 2) // Target mode
                        {
                            var vsync = targetMode.info.targetMode.targetVideoSignalInfo.vSyncFreq;
                            if (vsync.Denominator > 0)
                                refreshRate = (int)(vsync.Numerator / vsync.Denominator);
                        }
                    }
                    
                    // Get rotation from path
                    uint rotation = path.targetInfo.rotation;
                    
                    result.Add((gdiName, path.targetInfo.id, width, height, refreshRate, rotation, isPrimary, posX, posY));
                }
            }
            catch { }
            
            return result;
        }

        private static List<MonitorInfo> GetDisplaysFromCCD()
        {
            var monitors = new List<MonitorInfo>();
            var activeDisplays = GetActiveDisplayConfig();
            
            int displayNumber = 1;
            foreach (var display in activeDisplays.OrderBy(d => d.targetId))
            {
                // Get friendly name from target
                string friendlyName = GetTargetFriendlyName(display.targetId);
                
                monitors.Add(new MonitorInfo
                {
                    DeviceName = string.IsNullOrEmpty(friendlyName) ? $"Display {displayNumber}" : friendlyName,
                    AdapterName = "Graphics Adapter",
                    DeviceKey = display.gdiName,
                    DisplayNumber = displayNumber.ToString(),
                    Width = display.width,
                    Height = display.height,
                    RefreshRate = display.refreshRate,
                    Orientation = GetOrientationString(display.rotation),
                    IsPrimary = display.isPrimary,
                    IsEnabled = true,
                    PositionX = display.posX,
                    PositionY = display.posY
                });
                
                displayNumber++;
            }
            
            return monitors;
        }

        private static string GetTargetFriendlyName(uint targetId)
        {
            try
            {
                uint numPaths = 0, numModes = 0;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPaths, out numModes) != 0)
                    return "";

                var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
                var modes = new DISPLAYCONFIG_MODE_INFO[numModes];

                uint pathCount = numPaths, modeCount = numModes;
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                    return "";

                for (int i = 0; i < pathCount; i++)
                {
                    if (paths[i].targetInfo.id == targetId)
                    {
                        var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                        targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                        targetName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                        targetName.header.adapterId = paths[i].targetInfo.adapterId;
                        targetName.header.id = targetId;
                        
                        if (DisplayConfigGetDeviceInfo(ref targetName) == 0)
                            return targetName.monitorFriendlyDeviceName ?? "";
                    }
                }
            }
            catch { }
            
            return "";
        }

        private static string GetOrientationString(uint rotation)
        {
            return rotation switch
            {
                1 => "Landscape",        // DISPLAYCONFIG_ROTATION_IDENTITY
                2 => "Portrait",         // DISPLAYCONFIG_ROTATION_ROTATE90
                3 => "Landscape (flipped)", // DISPLAYCONFIG_ROTATION_ROTATE180
                4 => "Portrait (flipped)",  // DISPLAYCONFIG_ROTATION_ROTATE270
                _ => "Landscape"
            };
        }
        
        #endregion
    }
}
