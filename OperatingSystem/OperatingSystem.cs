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
using System.Threading;

using SystemInfoLibrary.Hardware;

namespace SystemInfoLibrary.OperatingSystem
{
    public enum OperatingSystemType
    {
        Windows,
        Unix,
        MacOSX,
        Unity5
    }

    public abstract class OperatingSystemInfo
    {
        public OperatingSystemType OperatingSystemType
        {
            get
            {
#if UNITY_5
                return OperatingSystemType.Unity5;
#endif

                if (GetType() == typeof(UnixOperatingSystemInfo))
                    return OperatingSystemType.Unix;

                if (GetType() == typeof(MacOSXOperatingSystemInfo))
                    return OperatingSystemType.MacOSX;

                return OperatingSystemType.Windows;
            }
        }

        /// <summary>
        /// Could be 16-bit 32-bit, 64-bit, ARM.
        /// </summary>
        public abstract string Architecture { get; }

        /// <summary>
        /// Operating System name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Locale ID assigned by <a href="https://msdn.microsoft.com/en-us/goglobal/bb964664.aspx">Microsoft</a>, in DEC.
        /// </summary>
        public int LocaleID
        {
            get
            {
                try { return Thread.CurrentThread.CurrentCulture.LCID; }
                catch { return 1033; } // Just return 1033 (English - USA)
            }
        }

        /// <summary>
        /// .NET Framework version.
        /// </summary>
        public abstract Version FrameworkVersion { get; }

        public bool IsMono => Type.GetType("Mono.Runtime") != null;

        /// <summary>
        /// Java version.
        /// </summary>
        public abstract Version JavaVersion { get; }


        public abstract HardwareInfo Hardware { get; }


        public abstract OperatingSystemInfo Update();


        public static OperatingSystemInfo GetOperatingSystemInfo()
        {
#if UNITY_5
            return new UnityOperatingSystemInfo();
#endif

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:

                    // https://github.com/mono/mono/blob/master/mcs/class/System/System/Platform.cs
                    // Calling uname from standard c library is pointless. The struct size varies on platforms.
                    var uname = IntPtr.Zero;
                    try
                    {
                        // 5 char[] with ~256 size = 1024 minimum
                        uname = Marshal.AllocHGlobal(8192);
                        if (Utils.UName(uname) == 0 && Marshal.PtrToStringAnsi(uname) == "Darwin")
                            return new MacOSXOperatingSystemInfo();
                    }
                    catch { /* ignored */ }
                    finally
                    {
                        if (uname != IntPtr.Zero)
                            Marshal.FreeHGlobal(uname);
                    }
                    return new UnixOperatingSystemInfo();

                case PlatformID.MacOSX:
                    return new MacOSXOperatingSystemInfo();

                default:
                    return new WindowsOperatingSystemInfo();

            }
        }
    }
}