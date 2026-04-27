using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DrawingRectangle = System.Drawing.Rectangle;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;

namespace ScreenShift
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        #endregion

        #region Constants
        
        private const uint QDC_ALL_PATHS = 0x00000001;
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        private const uint SDC_ALLOW_CHANGES = 0x00000400;
        private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
        private const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        private const int EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
        private const uint GW_OWNER = 4;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_MAXIMIZE = 3;
        private const int SW_RESTORE = 9;
        private const uint HWND_BROADCAST = 0xFFFF;
        private const uint WM_DISPLAYCHANGE = 0x007E;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint SPI_GETWORKAREA = 0x0030;

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
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public DrawingRectangle ToRectangle()
            {
                return DrawingRectangle.FromLTRB(Left, Top, Right, Bottom);
            }
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

        private sealed class DisplayConfigurationSnapshot
        {
            public DisplayConfigurationSnapshot(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)
            {
                Paths = paths;
                Modes = modes;
            }

            public DISPLAYCONFIG_PATH_INFO[] Paths { get; }
            public DISPLAYCONFIG_MODE_INFO[] Modes { get; }

            public DisplayConfigurationSnapshot Clone()
            {
                return new DisplayConfigurationSnapshot(
                    (DISPLAYCONFIG_PATH_INFO[])Paths.Clone(),
                    (DISPLAYCONFIG_MODE_INFO[])Modes.Clone());
            }
        }

        private sealed class PersistedDisplayConfigurationSnapshot
        {
            public int PathSize { get; set; }
            public int PathCount { get; set; }
            public string Paths { get; set; } = "";
            public int ModeSize { get; set; }
            public int ModeCount { get; set; }
            public string Modes { get; set; } = "";
        }

        #endregion

        private static readonly object _appDisabledConfigurationLock = new();
        private static DisplayConfigurationSnapshot? _appDisabledConfiguration;
        private static readonly string AppDisabledConfigurationFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenShift",
            "app-disabled-displays.json");

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
                if (!TryQueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, out var paths, out var modes))
                    return false;

                // Find paths for the new primary and current primary
                int newPrimaryPathIdx = -1;
                int currentPrimaryPathIdx = FindPrimaryPathIndex(paths, modes);
                
                for (int i = 0; i < paths.Length; i++)
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
                    }
                }

                if (newPrimaryPathIdx < 0 || newPrimaryPathIdx == currentPrimaryPathIdx)
                    return newPrimaryPathIdx == currentPrimaryPathIdx;

                // Get positions
                var newPrimaryModeIdx = paths[newPrimaryPathIdx].sourceInfo.modeInfoIdx;

                if (newPrimaryModeIdx >= modes.Length)
                    return false;

                int newPrimaryX = modes[newPrimaryModeIdx].info.sourceMode.position.x;
                int newPrimaryY = modes[newPrimaryModeIdx].info.sourceMode.position.y;

                // Shift all displays so new primary is at (0,0)
                for (int i = 0; i < modes.Length; i++)
                {
                    if (modes[i].infoType == 1) // Source mode
                    {
                        modes[i].info.sourceMode.position.x -= newPrimaryX;
                        modes[i].info.sourceMode.position.y -= newPrimaryY;
                    }
                }

                // Apply configuration
                uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_SAVE_TO_DATABASE | SDC_ALLOW_CHANGES;
                int result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags);
                
                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Disables every active display except the current primary display.
        /// </summary>
        public static bool DisableNonPrimaryDisplays()
        {
            try
            {
                if (!TryQueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, out var paths, out var modes))
                    return false;

                if (paths.Length <= 1)
                    return true;

                int primaryPathIndex = FindPrimaryPathIndex(paths, modes);
                if (primaryPathIndex < 0)
                    return false;

                var primaryPath = paths[primaryPathIndex];
                primaryPath.flags |= DISPLAYCONFIG_PATH_ACTIVE;

                if (!TryCreateSinglePathConfiguration(primaryPath, modes, out var primaryOnlyPaths, out var primaryOnlyModes))
                    return false;

                uint primaryModeIndex = primaryOnlyPaths[0].sourceInfo.modeInfoIdx;
                if (primaryModeIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID &&
                    primaryModeIndex < primaryOnlyModes.Length &&
                    primaryOnlyModes[primaryModeIndex].infoType == 1)
                {
                    primaryOnlyModes[primaryModeIndex].info.sourceMode.position.x = 0;
                    primaryOnlyModes[primaryModeIndex].info.sourceMode.position.y = 0;
                }
                else
                {
                    return false;
                }

                uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
                int result = SetDisplayConfig((uint)primaryOnlyPaths.Length, primaryOnlyPaths, (uint)primaryOnlyModes.Length, primaryOnlyModes, flags);

                if (result != 0)
                    return false;

                RefreshShellAfterDisplayChange();
                MoveOffscreenWindowsToPrimaryWorkArea();

                lock (_appDisabledConfigurationLock)
                {
                    _appDisabledConfiguration = new DisplayConfigurationSnapshot(
                        (DISPLAYCONFIG_PATH_INFO[])paths.Clone(),
                        (DISPLAYCONFIG_MODE_INFO[])modes.Clone());
                    SaveAppDisabledConfiguration(_appDisabledConfiguration);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enables only the displays disabled by the last primary-only action.
        /// </summary>
        public static bool EnableAllDisplays()
        {
            try
            {
                DisplayConfigurationSnapshot? snapshot;
                lock (_appDisabledConfigurationLock)
                {
                    snapshot = GetAppDisabledConfigurationLocked()?.Clone();
                }

                if (snapshot == null || snapshot.Paths.Length <= 1)
                    return false;

                var paths = snapshot.Paths
                    .Select(path =>
                    {
                        path.flags |= DISPLAYCONFIG_PATH_ACTIVE;
                        return path;
                    })
                    .ToArray();

                uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
                int result = SetDisplayConfig((uint)paths.Length, paths, (uint)snapshot.Modes.Length, snapshot.Modes, flags);
                if (result != 0)
                    return false;

                ClearAppDisabledConfiguration();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasDisplaysDisabledByApp()
        {
            lock (_appDisabledConfigurationLock)
            {
                return GetAppDisabledConfigurationLocked()?.Paths.Length > 1;
            }
        }
         
        #endregion

        #region Private Methods

        private static bool TryQueryDisplayConfig(uint queryFlags, out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
        {
            paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
            modes = Array.Empty<DISPLAYCONFIG_MODE_INFO>();

            uint numPaths = 0, numModes = 0;
            if (GetDisplayConfigBufferSizes(queryFlags, out numPaths, out numModes) != 0)
                return false;

            if (numPaths == 0)
                return false;

            var pathArray = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modeArray = new DISPLAYCONFIG_MODE_INFO[numModes];

            uint pathCount = numPaths, modeCount = numModes;
            if (QueryDisplayConfig(queryFlags, ref pathCount, pathArray, ref modeCount, modeArray, IntPtr.Zero) != 0)
                return false;

            paths = pathArray.Take((int)pathCount).ToArray();
            modes = modeArray.Take((int)modeCount).ToArray();
            return true;
        }

        private static int FindPrimaryPathIndex(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                uint sourceModeIndex = paths[i].sourceInfo.modeInfoIdx;
                if (sourceModeIndex >= modes.Length)
                    continue;

                var modeInfo = modes[sourceModeIndex];
                if (modeInfo.infoType != 1)
                    continue;

                if (modeInfo.info.sourceMode.position.x == 0 && modeInfo.info.sourceMode.position.y == 0)
                    return i;
            }

            return -1;
        }

        private static bool TryCreateSinglePathConfiguration(
            DISPLAYCONFIG_PATH_INFO path,
            DISPLAYCONFIG_MODE_INFO[] modes,
            out DISPLAYCONFIG_PATH_INFO[] paths,
            out DISPLAYCONFIG_MODE_INFO[] compactModes)
        {
            paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
            compactModes = Array.Empty<DISPLAYCONFIG_MODE_INFO>();

            var remappedModes = new List<DISPLAYCONFIG_MODE_INFO>();
            var modeIndexMap = new Dictionary<uint, uint>();

            if (!TryAddRemappedMode(path.sourceInfo.modeInfoIdx, modes, remappedModes, modeIndexMap, out uint sourceModeIndex))
                return false;

            if (!TryAddRemappedMode(path.targetInfo.modeInfoIdx, modes, remappedModes, modeIndexMap, out uint targetModeIndex))
                return false;

            path.sourceInfo.modeInfoIdx = sourceModeIndex;
            path.targetInfo.modeInfoIdx = targetModeIndex;

            paths = new[] { path };
            compactModes = remappedModes.ToArray();
            return true;
        }

        private static bool TryAddRemappedMode(
            uint modeInfoIndex,
            DISPLAYCONFIG_MODE_INFO[] sourceModes,
            List<DISPLAYCONFIG_MODE_INFO> remappedModes,
            Dictionary<uint, uint> modeIndexMap,
            out uint remappedIndex)
        {
            remappedIndex = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;

            if (modeInfoIndex == DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
                return true;

            if (modeInfoIndex >= sourceModes.Length)
                return false;

            if (modeIndexMap.TryGetValue(modeInfoIndex, out remappedIndex))
                return true;

            remappedIndex = (uint)remappedModes.Count;
            remappedModes.Add(sourceModes[modeInfoIndex]);
            modeIndexMap[modeInfoIndex] = remappedIndex;
            return true;
        }

        private static void ClearAppDisabledConfiguration()
        {
            lock (_appDisabledConfigurationLock)
            {
                _appDisabledConfiguration = null;
                if (File.Exists(AppDisabledConfigurationFilePath))
                    File.Delete(AppDisabledConfigurationFilePath);
            }
        }

        private static DisplayConfigurationSnapshot? GetAppDisabledConfigurationLocked()
        {
            if (_appDisabledConfiguration != null)
                return _appDisabledConfiguration;

            _appDisabledConfiguration = LoadAppDisabledConfiguration();
            return _appDisabledConfiguration;
        }

        private static void SaveAppDisabledConfiguration(DisplayConfigurationSnapshot snapshot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppDisabledConfigurationFilePath)!);

            var persisted = new PersistedDisplayConfigurationSnapshot
            {
                PathSize = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>(),
                PathCount = snapshot.Paths.Length,
                Paths = Convert.ToBase64String(StructArrayToBytes(snapshot.Paths)),
                ModeSize = Marshal.SizeOf<DISPLAYCONFIG_MODE_INFO>(),
                ModeCount = snapshot.Modes.Length,
                Modes = Convert.ToBase64String(StructArrayToBytes(snapshot.Modes))
            };

            File.WriteAllText(AppDisabledConfigurationFilePath, JsonSerializer.Serialize(persisted));
        }

        private static DisplayConfigurationSnapshot? LoadAppDisabledConfiguration()
        {
            if (!File.Exists(AppDisabledConfigurationFilePath))
                return null;

            try
            {
                var persisted = JsonSerializer.Deserialize<PersistedDisplayConfigurationSnapshot>(
                    File.ReadAllText(AppDisabledConfigurationFilePath));
                if (persisted == null)
                    return null;

                int pathSize = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>();
                int modeSize = Marshal.SizeOf<DISPLAYCONFIG_MODE_INFO>();
                if (persisted.PathSize != pathSize || persisted.ModeSize != modeSize)
                    return null;

                var pathBytes = Convert.FromBase64String(persisted.Paths);
                var modeBytes = Convert.FromBase64String(persisted.Modes);
                if (pathBytes.Length != persisted.PathCount * pathSize ||
                    modeBytes.Length != persisted.ModeCount * modeSize)
                {
                    return null;
                }

                return new DisplayConfigurationSnapshot(
                    BytesToStructArray<DISPLAYCONFIG_PATH_INFO>(pathBytes, persisted.PathCount),
                    BytesToStructArray<DISPLAYCONFIG_MODE_INFO>(modeBytes, persisted.ModeCount));
            }
            catch
            {
                return null;
            }
        }

        private static byte[] StructArrayToBytes<T>(T[] values) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            var bytes = new byte[values.Length * size];

            for (int i = 0; i < values.Length; i++)
            {
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(values[i], buffer, false);
                    Marshal.Copy(buffer, bytes, i * size, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return bytes;
        }

        private static T[] BytesToStructArray<T>(byte[] bytes, int count) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            var values = new T[count];
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Marshal.Copy(bytes, i * size, buffer, size);
                    values[i] = Marshal.PtrToStructure<T>(buffer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return values;
        }

        private static void RefreshShellAfterDisplayChange()
        {
            Thread.Sleep(500);
            SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_DISPLAYCHANGE,
                UIntPtr.Zero,
                IntPtr.Zero,
                SMTO_ABORTIFHUNG,
                1000,
                out _);
            SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                IntPtr.Zero,
                SMTO_ABORTIFHUNG,
                1000,
                out _);
        }

        private static void MoveOffscreenWindowsToPrimaryWorkArea()
        {
            if (!TryGetPrimaryWorkArea(out DrawingRectangle primaryWorkArea))
                return;

            if (primaryWorkArea.Width <= 0 || primaryWorkArea.Height <= 0)
                return;

            IntPtr shellWindow = GetShellWindow();

            EnumWindows((hWnd, _) =>
            {
                if (ShouldSkipWindowForPostDisplayMove(hWnd, shellWindow))
                    return true;

                if (!GetWindowRect(hWnd, out RECT windowRect))
                    return true;

                DrawingRectangle windowBounds = windowRect.ToRectangle();
                if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
                    return true;

                int centerX = windowBounds.Left + (windowBounds.Width / 2);
                int centerY = windowBounds.Top + (windowBounds.Height / 2);
                if (primaryWorkArea.Contains(centerX, centerY))
                    return true;

                MoveWindowIntoWorkArea(hWnd, windowBounds, primaryWorkArea);
                return true;
            }, IntPtr.Zero);
        }

        private static bool TryGetPrimaryWorkArea(out DrawingRectangle workArea)
        {
            var rect = new RECT();
            if (!SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0))
            {
                workArea = DrawingRectangle.Empty;
                return false;
            }

            workArea = rect.ToRectangle();
            return true;
        }

        private static bool ShouldSkipWindowForPostDisplayMove(IntPtr hWnd, IntPtr shellWindow)
        {
            if (hWnd == shellWindow || !IsWindowVisible(hWnd) || IsIconic(hWnd))
                return true;

            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                return true;

            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == Environment.ProcessId)
                return true;

            string className = GetWindowClassName(hWnd);
            return className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW";
        }

        private static void MoveWindowIntoWorkArea(IntPtr hWnd, DrawingRectangle windowBounds, DrawingRectangle workArea)
        {
            bool wasMaximized = IsZoomed(hWnd);
            if (wasMaximized)
            {
                ShowWindow(hWnd, SW_RESTORE);
                if (!GetWindowRect(hWnd, out RECT restoredRect))
                    return;

                windowBounds = restoredRect.ToRectangle();
            }

            bool needsResize = windowBounds.Width > workArea.Width || windowBounds.Height > workArea.Height;
            int width = needsResize ? Math.Min(windowBounds.Width, workArea.Width) : windowBounds.Width;
            int height = needsResize ? Math.Min(windowBounds.Height, workArea.Height) : windowBounds.Height;
            int x = Clamp(windowBounds.Left, workArea.Left, workArea.Right - width);
            int y = Clamp(windowBounds.Top, workArea.Top, workArea.Bottom - height);

            uint flags = SWP_NOZORDER | SWP_NOACTIVATE;
            if (needsResize)
                SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
            else
                SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, flags | SWP_NOSIZE);

            if (wasMaximized)
                ShowWindow(hWnd, SW_MAXIMIZE);
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            int length = GetClassName(hWnd, className, className.Capacity);
            return length > 0 ? className.ToString() : string.Empty;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
                return min;

            return Math.Min(Math.Max(value, min), max);
        }

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
