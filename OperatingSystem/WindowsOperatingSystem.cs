﻿/*
 * Little Software Stats - .NET Library
 * Copyright (C) 2008-2012 Little Apps (http://www.little-apps.org)
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Runtime.InteropServices;

using SystemInfoLibrary.Hardware;

using Microsoft.Win32;

namespace SystemInfoLibrary.OperatingSystem
{
    internal class WindowsOperatingSystemInfo : OperatingSystemInfo
    {
        #region P/Invoke Signatures
        private const byte VER_NT_WORKSTATION = 1;

        private const ushort VER_SUITE_WH_SERVER = 32768;

        private const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
        private const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
        private const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;

        private const int SM_SERVERR2 = 89;

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public uint wProcessorArchitecture;
            public uint wReserved;
            public uint dwPageSize;
            public uint lpMinimumApplicationAddress;
            public uint lpMaximumApplicationAddress;
            public uint dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public uint dwProcessorLevel;
            public uint dwProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetVersionEx(ref OSVERSIONINFOEX osVersionInfo);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO pSI);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        #endregion

        private HardwareInfo _hardware;
        public override HardwareInfo Hardware { get { return _hardware ?? (_hardware = new WindowsHardwareInfo()); } }

        private Version _frameworkVersion;
        public override Version FrameworkVersion { get { return _frameworkVersion; } }

        private int _frameworkSP;
        public override int FrameworkSP { get { return _frameworkSP; } }

        private Version _javaVersion;
        public override Version JavaVersion { get { return _javaVersion; } }

        public sealed override int Architecture
        {
            get
            {
                var arch = (string) Utils.GetRegistryValue(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "PROCESSOR_ARCHITECTURE");

                switch (arch.ToLower())
                {
                    case "x86":
                        return 32;
                    case "amd64":
                    case "ia64":
                        return 64;
                }

                // Just use IntPtr size
                // (note: will always return 32 bit if process is not 64 bit)
                return IntPtr.Size == 8 ? 64 : 32;
            }
        }

        private string _version;

        public override string Version { get { return _version; } }
        private int _servicePack;
        public override int ServicePack { get { return _servicePack; } }

        public WindowsOperatingSystemInfo()
        {
            // Get OS Info
            GetOsInfo();

            // Get .NET Framework version + SP
            try
            {
                var regNet = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\NET Framework Setup\NDP");

                if (regNet != null)
                {
                    if (regNet.OpenSubKey("v4") != null)
                        _frameworkVersion = new Version(4, 0);
                    else if (regNet.OpenSubKey("v3.5") != null)
                    {
                        _frameworkVersion = new Version(3, 5);
                        _frameworkSP = (int) regNet.GetValue("SP", 0);
                    }
                    else if (regNet.OpenSubKey("v3.0") != null)
                    {
                        _frameworkVersion = new Version(3, 0);
                        _frameworkSP = (int) regNet.GetValue("SP", 0);
                    }
                    else if (regNet.OpenSubKey("v2.0.50727") != null)
                    {
                        _frameworkVersion = new Version(2, 0, 50727);
                        _frameworkSP = (int) regNet.GetValue("SP", 0);
                    }
                    else if (regNet.OpenSubKey("v1.1.4322") != null)
                    {
                        _frameworkVersion = new Version(1, 1, 4322);
                        _frameworkSP = (int) regNet.GetValue("SP", 0);
                    }
                    else if (regNet.OpenSubKey("v1.0") != null)
                    {
                        _frameworkVersion = new Version(1, 0);
                        _frameworkSP = (int) regNet.GetValue("SP", 0);
                    }

                    regNet.Close();
                }
            }
            catch { /* ignored */ }

            // Get Java version
            _javaVersion = new Version();

            try
            {
                var javaVersion = Architecture == 32
                    ? (string) Utils.GetRegistryValue(Registry.LocalMachine, @"Software\JavaSoft\Java Runtime Environment", "CurrentVersion", "")
                    : (string) Utils.GetRegistryValue(Registry.LocalMachine, @"Software\Wow6432Node\JavaSoft\Java Runtime Environment", "CurrentVersion", "");

                _javaVersion = new Version(javaVersion);
            }
            catch { /* ignored */ }
        }

        private void GetOsInfo()
        {
            var osVersionInfo = new OSVERSIONINFOEX { dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OSVERSIONINFOEX)) };

            if (!GetVersionEx(ref osVersionInfo))
            {
                _version = "Unknown";
                _servicePack = 0;
                return;
            }

            var osName = "";

            SYSTEM_INFO systemInfo = new SYSTEM_INFO();
            GetSystemInfo(ref systemInfo);

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32Windows:
                    {
                        switch (osVersionInfo.dwMajorVersion)
                        {
                            case 4:
                                {
                                    switch (osVersionInfo.dwMinorVersion)
                                    {
                                        case 0:
                                            if (osVersionInfo.szCSDVersion == "B" || osVersionInfo.szCSDVersion == "C")
                                                osName += "Windows 95 R2";
                                            else
                                                osName += "Windows 95";
                                            break;
                                        case 10:
                                            if (osVersionInfo.szCSDVersion == "A")
                                                osName += "Windows 98 SE";
                                            else
                                                osName += "Windows 98";
                                            break;
                                        case 90:
                                            osName += "Windows ME";
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                    break;

                case PlatformID.Win32NT:
                    {
                        switch (osVersionInfo.dwMajorVersion)
                        {
                            case 3:
                                osName += "Windows NT 3.5.1";
                                break;

                            case 4:
                                osName += "Windows NT 4.0";
                                break;

                            case 5:
                                {
                                    switch (osVersionInfo.dwMinorVersion)
                                    {
                                        case 0:
                                            osName += "Windows 2000";
                                            break;
                                        case 1:
                                            osName += "Windows XP";
                                            break;
                                        case 2:
                                            {
                                                if (osVersionInfo.wSuiteMask == VER_SUITE_WH_SERVER)
                                                    osName += "Windows Home Server";
                                                else if (osVersionInfo.wProductType == VER_NT_WORKSTATION && systemInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_AMD64)
                                                    osName += "Windows XP";
                                                else
                                                    osName += GetSystemMetrics(SM_SERVERR2) == 0 ? "Windows Server 2003" : "Windows Server 2003 R2";
                                            }
                                            break;
                                    }

                                }
                                break;

                            case 6:
                                {
                                    switch (osVersionInfo.dwMinorVersion)
                                    {
                                        case 0:
                                            osName += osVersionInfo.wProductType == VER_NT_WORKSTATION ? "Windows Vista" : "Windows Server 2008";
                                            break;

                                        case 1:
                                            osName += osVersionInfo.wProductType == VER_NT_WORKSTATION ? "Windows 7" : "Windows Server 2008 R2";
                                            break;
                                        case 2:
                                            osName += osVersionInfo.wProductType == VER_NT_WORKSTATION ? "Windows 8" : "Windows Server 2012";
                                            break;
										case 3:
                                            osName += osVersionInfo.wProductType == VER_NT_WORKSTATION ? "Windows 8.1" : "Windows Server 2012 R2";
                                            break;
                                    }
                                }
                                break;

                            case 10:
                                {
                                    switch (osVersionInfo.dwMinorVersion)
                                    {
                                        case 0:
                                            osName += osVersionInfo.wProductType == VER_NT_WORKSTATION ? "Windows 10" : "Windows Server 2016 Technical Preview";
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                    break;
            }

            _version = osName;
            _servicePack = osVersionInfo.wServicePackMajor;
        }
    } 
}
