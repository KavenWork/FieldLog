﻿// FieldLog – .NET logging solution
// © Yves Goergen, Made in Germany
// Website: http://unclassified.software/source/fieldlog
//
// This library is free software: you can redistribute it and/or modify it under the terms of
// the GNU Lesser General Public License as published by the Free Software Foundation, version 3.
//
// This library is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License along with this
// library. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace Unclassified.FieldLog
{
	#region OSInfo class

	/// <summary>
	/// Provides information about the version, edition and other aspects of running operating system.
	/// </summary>
	public static class OSInfo
	{
		#region Native interop

		[StructLayout(LayoutKind.Sequential)]
		private struct OSVERSIONINFOEX
		{
			public int dwOSVersionInfoSize;
			public int dwMajorVersion;
			public int dwMinorVersion;
			public int dwBuildNumber;
			public int dwPlatformId;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string szCSDVersion;
			public short wServicePackMajor;
			public short wServicePackMinor;
			public ushort wSuiteMask;
			public byte wProductType;
			public byte wReserved;
		}

		/// <summary>
		/// The value that specifies how the system is started:
		/// 0 Normal boot
		/// 1 Fail-safe boot
		/// 2 Fail-safe with network boot
		/// A fail-safe boot (also called SafeBoot, Safe Mode, or Clean Boot) bypasses the user startup files.
		/// </summary>
		private const int SM_CLEANBOOT = 67;
		/// <summary>
		/// The number of display monitors on a desktop.
		/// </summary>
		private const int SM_CMONITORS = 80;
		/// <summary>
		/// The number of buttons on a mouse, or zero if no mouse is installed.
		/// </summary>
		private const int SM_CMOUSEBUTTONS = 43;
		/// <summary>
		/// Nonzero if the current operating system is the Windows XP, Media Center Edition, 0 if not.
		/// </summary>
		private const int SM_MEDIACENTER = 87;
		/// <summary>
		/// The build number if the system is Windows Server 2003 R2; otherwise, 0.
		/// </summary>
		private const int SM_SERVERR2 = 89;
		/// <summary>
		/// Nonzero if the current operating system is Windows 7 Starter Edition, Windows Vista
		/// Starter, or Windows XP Starter Edition; otherwise, 0.
		/// </summary>
		private const int SM_STARTER = 88;
		/// <summary>
		/// Nonzero if the current operating system is the Windows XP Tablet PC edition or if the
		/// current operating system is Windows Vista or Windows 7 and the Tablet PC Input service
		/// is started; otherwise, 0.
		/// </summary>
		private const int SM_TABLETPC = 86;
		/// <summary>
		/// Nonzero if the current operating system is Windows 7 or Windows Server 2008 R2 and the
		/// Tablet PC Input service is started; otherwise, 0. The return value is a bitmask that
		/// specifies the type of digitizer input supported by the device. (Only supported in
		/// Windows 7 an newer.)
		/// </summary>
		private const int SM_DIGITIZER = 94;
		/// <summary>
		/// Nonzero if there are digitizers in the system; otherwise, 0. SM_MAXIMUMTOUCHES returns
		/// the aggregate maximum of the maximum number of contacts supported by every digitizer in
		/// the system. If the system has only single-touch digitizers, the return value is 1. If
		/// the system has multi-touch digitizers, the return value is the number of simultaneous
		/// contacts the hardware can provide. (Only supported in Windows 7 an newer.)
		/// </summary>
		private const int SM_MAXIMUMTOUCHES = 95;

		/// <summary>
		/// Microsoft BackOffice components are installed.
		/// </summary>
		private const ushort VER_SUITE_BACKOFFICE = 0x00000004;
		/// <summary>
		/// Windows Server 2003, Web Edition is installed.
		/// </summary>
		private const ushort VER_SUITE_BLADE = 0x00000400;
		/// <summary>
		/// Windows Server 2003, Compute Cluster Edition is installed.
		/// </summary>
		private const ushort VER_SUITE_COMPUTE_SERVER = 0x00004000;
		/// <summary>
		/// Windows Server 2008 Datacenter, Windows Server 2003, Datacenter Edition, or Windows 2000
		/// Datacenter Server is installed.
		/// </summary>
		private const ushort VER_SUITE_DATACENTER = 0x00000080;
		/// <summary>
		/// Windows Server 2008 Enterprise, Windows Server 2003, Enterprise Edition, or Windows 2000
		/// Advanced Server is installed. Refer to the Remarks section for more information about
		/// this bit flag.
		/// </summary>
		private const ushort VER_SUITE_ENTERPRISE = 0x00000002;
		/// <summary>
		/// Windows XP Embedded is installed.
		/// </summary>
		private const ushort VER_SUITE_EMBEDDEDNT = 0x00000040;
		/// <summary>
		/// Windows Vista Home Premium, Windows Vista Home Basic, or Windows XP Home Edition is installed.
		/// </summary>
		private const ushort VER_SUITE_PERSONAL = 0x00000200;
		/// <summary>
		/// Remote Desktop is supported, but only one interactive session is supported. This value
		/// is set unless the system is running in application server mode.
		/// </summary>
		private const ushort VER_SUITE_SINGLEUSERTS = 0x00000100;
		/// <summary>
		/// Microsoft Small Business Server was once installed on the system, but may have been
		/// upgraded to another version of Windows.
		/// </summary>
		private const ushort VER_SUITE_SMALLBUSINESS = 0x00000001;
		/// <summary>
		/// Microsoft Small Business Server is installed with the restrictive client license in force.
		/// </summary>
		private const ushort VER_SUITE_SMALLBUSINESS_RESTRICTED = 0x00000020;
		/// <summary>
		/// Windows Storage Server 2003 R2 or Windows Storage Server 2003 is installed.
		/// </summary>
		private const ushort VER_SUITE_STORAGE_SERVER = 0x00002000;
		/// <summary>
		/// Terminal Services is installed. This value is always set.
		/// If VER_SUITE_TERMINAL is set but VER_SUITE_SINGLEUSERTS is not set, the system is running
		/// in application server mode.
		/// </summary>
		private const ushort VER_SUITE_TERMINAL = 0x00000010;
		/// <summary>
		/// Windows Home Server is installed.
		/// </summary>
		private const ushort VER_SUITE_WH_SERVER = 0x8000;

		private const ushort VER_NT_WORKSTATION = 1;

		/// <summary>
		/// An integrated (built-in) touch digitizer is used for input.
		/// </summary>
		private const int NID_INTEGRATED_TOUCH = 0x1;
		/// <summary>
		/// An external (connected) touch digitizer is used for input.
		/// </summary>
		private const int NID_EXTERNAL_TOUCH = 0x2;
		/// <summary>
		/// An integrated (built-in) pen digitizer is used for input.
		/// </summary>
		private const int NID_INTEGRATED_PEN = 0x4;
		/// <summary>
		/// An external (connected) pen digitizer is used for input.
		/// </summary>
		private const int NID_EXTERNAL_PEN = 0x8;
		/// <summary>
		/// An input digitizer with support for multiple inputs is used for input.
		/// </summary>
		private const int NID_MULTI_INPUT = 0x40;
		/// <summary>
		/// The input digitizer is ready for input. If this value is unset, it may mean that the
		/// tablet service is stopped, the digitizer is not supported, or digitizer drivers have
		/// not been installed.
		/// </summary>
		private const int NID_READY = 0x80;

		/// <summary>
		/// Logical pixels inch in X.
		/// </summary>
		private const int LOGPIXELSX = 88;
		/// <summary>
		/// Logical pixels inch in Y.
		/// </summary>
		private const int LOGPIXELSY = 90;

		[DllImport("kernel32.dll")]
		private static extern bool GetVersionEx(ref OSVERSIONINFOEX osvi);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int smIndex);

		[DllImport("gdi32.dll")]
		private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

		#endregion Native interop

		#region Public static properties

		/// <summary>
		/// Gets the operating system type (client, server, core server).
		/// </summary>
		public static OSType Type { get; private set; }
		/// <summary>
		/// Gets the Windows version.
		/// </summary>
		public static OSVersion Version { get; private set; }
		/// <summary>
		/// Gets the Windows edition.
		/// </summary>
		public static OSEdition Edition { get; private set; }
		/// <summary>
		/// Gets the installed service pack name.
		/// </summary>
		public static string ServicePack { get; private set; }
		/// <summary>
		/// Gets a value indicating whether a 64 bit system is running.
		/// </summary>
		public static bool Is64Bit { get; private set; }
		/// <summary>
		/// Gets the Windows version build number.
		/// </summary>
		public static int Build { get; private set; }
		/// <summary>
		/// Gets the service pack build number.
		/// </summary>
		public static int ServicePackBuild { get; private set; }
		/// <summary>
		/// Gets the complete operating system product name from the registry, including Windows
		/// version and edition name. This can be used if correctness is required and the value
		/// does not need to be evaluated.
		/// </summary>
		public static string ProductName { get; private set; }
		/// <summary>
		/// Gets a value indicating whether the system is set up as application terminal server.
		/// </summary>
		public static bool IsAppServer { get; private set; }
		/// <summary>
		/// Gets the ISO 639-1/ISO 3166 language/country code of the system language.
		/// </summary>
		public static string Language { get; private set; }
		/// <summary>
		/// Gets the time when the system was last booted, in UTC.
		/// </summary>
		public static DateTime LastBootTime { get; private set; }
		/// <summary>
		/// Gets a value indicating whether the system is started in fail-safe mode.
		/// </summary>
		public static bool IsFailSafeBoot { get; private set; }
		/// <summary>
		/// Gets the application compatibility layers that are in effect for the current process.
		/// </summary>
		public static string AppCompatLayer { get; private set; }
		/// <summary>
		/// Gets the CLR type running the current process. This is either "Microsoft .NET" or
		/// "Mono".
		/// </summary>
		public static string ClrType { get; private set; }
		/// <summary>
		/// Gets the number of buttons on a mouse, or zero if no mouse is installed.
		/// </summary>
		public static int MouseButtons { get; private set; }
		/// <summary>
		/// Gets the number of supported touch points.
		/// </summary>
		public static int MaxTouchPoints { get; private set; }
		/// <summary>
		/// Gets the logical resolution of the screen. 100 % is 96 dpi.
		/// </summary>
		public static int ScreenDpi { get; private set; }

		#endregion Public static properties

		#region Static constructor

		/// <summary>
		/// Initialises the static environment information and stores it in the static properties
		/// for later access.
		/// </summary>
		static OSInfo()
		{
			// Get the uptime of the computer. This will restart from zero every 49.8 days.
			// If WMI is available further down, it will be used to determine a more reliable value.
			int tickCount = Environment.TickCount;
			LastBootTime = DateTime.UtcNow;
			if (tickCount < 0)
				LastBootTime = LastBootTime.AddMilliseconds(-tickCount - uint.MaxValue);
			else
				LastBootTime = LastBootTime.AddMilliseconds(-tickCount);

			IsFailSafeBoot = GetSystemMetrics(SM_CLEANBOOT) != 0;

			if (System.Type.GetType("Mono.Runtime") != null)
				ClrType = "Mono";
			else
				ClrType = "Microsoft .NET";

			MouseButtons = GetSystemMetrics(SM_CMOUSEBUTTONS);

			// Look for integrated or external touch capability, then count touch points
			int smDigitizer = GetSystemMetrics(SM_DIGITIZER);
			if ((smDigitizer & (NID_INTEGRATED_TOUCH | NID_EXTERNAL_TOUCH)) != 0)
			{
				if ((smDigitizer & NID_MULTI_INPUT) != 0)
				{
					MaxTouchPoints = GetSystemMetrics(SM_MAXIMUMTOUCHES);
				}
				else
				{
					MaxTouchPoints = 1;
				}
			}

			IntPtr hdc = GetDC(IntPtr.Zero);
			if (hdc != IntPtr.Zero)
			{
				ScreenDpi = GetDeviceCaps(hdc, LOGPIXELSY);
				ReleaseDC(IntPtr.Zero, hdc);
			}

			if (Environment.OSVersion.Platform == PlatformID.Win32Windows)
			{
				// Non-NT Windows
				// Source: http://support.microsoft.com/kb/158238
				// Windows 95 (all 4.0) is not supported by .NET so it's not regarded here.
				if (Environment.OSVersion.Version.Major >= 4 && Environment.OSVersion.Version.Minor == 10 && Environment.OSVersion.Version.Build == 1998)
					Version = OSVersion.Windows98;
				else if (Environment.OSVersion.Version.Major >= 4 && Environment.OSVersion.Version.Minor == 10 && Environment.OSVersion.Version.Build == 2222)
					Version = OSVersion.Windows98SE;
				else if (Environment.OSVersion.Version.Major >= 4 && Environment.OSVersion.Version.Minor == 90)
					Version = OSVersion.WindowsME;
			}
			else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				// Windows NT-based

				// Windows NT version detection based on new observations:
				// * Stay with Environment.OSVersion for 4.x (NT4)
				// * Always use WMI for the Windows version
				// * Get the rest from other sources (but first search in WMI for an equivalent)
				//   - Registry may be affected by app compatibility mode from Windows XP through Windows 7 (not later)
				//   - x86 build on 64 bit Windows can't access some registry keys
				//     See http://stackoverflow.com/questions/12136372/unable-to-query-value-of-csdversion
				//   - GetVersionEx may be affected by Windows 8.1 manifest
				//   - GetSystemMetrics is of unknown reliability
				//   - WMI/OperatingSystemSKU is available from version 6.0 on, use existing checks until then
				//
				// Sources:
				// * OSVERSIONINFOEX structure
				//   http://msdn.microsoft.com/en-us/library/windows/desktop/ms724833.aspx
				// * GetVersionEx function
				//   http://msdn.microsoft.com/en-us/library/windows/desktop/ms724451.aspx
				// * WMI info
				//   http://techontip.wordpress.com/tag/operatingsystemsku/
				//     refernecing http://msdn.microsoft.com/en-us/library/ms724358.aspx (GetProductInfo function) for SKU numbers
				//   http://msdn.microsoft.com/en-us/library/windows/desktop/aa394239%28v=vs.85%29.aspx

				if (Environment.OSVersion.Version.Major < 5)
				{
					// NT4 doesn't support WMI so that's all we know (?)
					Version = OSVersion.WindowsNT4;
					Build = Environment.OSVersion.Version.Build;
					ServicePack = ParseSPNumber(Environment.OSVersion.ServicePack);
					ProductName = ReadRegistryVersionValue("ProductName");
				}
				else
				{
					// Windows 2000 or newer

					// Collect equivalent values from different possible sources
					OSVERSIONINFOEX osvi = new OSVERSIONINFOEX();
					osvi.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
					GetVersionEx(ref osvi);

					string version = GetWmiIdentifier("Win32_OperatingSystem", "Version");
					if (string.IsNullOrEmpty(version))
					{
						// Oops, try something else
						version = ReadRegistryVersionValue("CurrentVersion");
					}

					int build;
					if (!int.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "BuildNumber"), out build))
					{
						// Oops, try something else
						build = ReadRegistryVersionIntValue("CurrentBuildNumber");
					}

					int productType;
					if (!int.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "ProductType"), out productType))
					{
						// Oops, try something else
						productType = osvi.wProductType;
					}

					int productSuite;
					if (!int.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "OSProductSuite"), out productSuite))
					{
						// Oops, try something else
						productSuite = osvi.wSuiteMask;
					}

					int sku;
					int.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "OperatingSystemSKU"), out sku);

					string csdVersion = GetWmiIdentifier("Win32_OperatingSystem", "CSDVersion");
					if (string.IsNullOrEmpty(version))
					{
						csdVersion = GetWmiIdentifier("Win32_OperatingSystem", "ServicePackMajorVersion");
						if (csdVersion != null)
						{
							string spMinorVersion = GetWmiIdentifier("Win32_OperatingSystem", "ServicePackMajorVersion");
							if (spMinorVersion != null && spMinorVersion != "0")
								csdVersion += "." + spMinorVersion;
							if (csdVersion == "0")
								csdVersion = "";
						}
						else
						{
							// Oops, try something else
							// NOTE: x86 build on 64 bit Windows cannot access this registry key.
							// Source: http://stackoverflow.com/questions/12136372/unable-to-query-value-of-csdversion
							csdVersion = ReadRegistryVersionValue("CSDVersion");
						}
					}

					// Detect common features
					if (csdVersion != null && csdVersion.Length > 13 && csdVersion.StartsWith("Service Pack ", StringComparison.Ordinal))
					{
						ServicePack = csdVersion.Substring(13);
					}
					else
					{
						ServicePack = csdVersion;
					}

					Build = build;
					ServicePackBuild = ReadRegistryVersionIntValue("CSDBuildNumber");
					ProductName = ReadRegistryVersionValue("ProductName");

					//Is64Bit = Environment.Is64BitOperatingSystem;   // .NET 4+ only
					string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
					Is64Bit = arch == "AMD64";

					IsAppServer = (productSuite & VER_SUITE_TERMINAL) != 0 && (productSuite & VER_SUITE_SINGLEUSERTS) == 0;

					// Detect app compat by searching for own executable path in the registry at
					// HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers\
					// Just make the entire string of it available for logging.
					// Flags description:
					// http://www.verboon.info/2011/03/running-an-application-as-administrator-or-in-compatibility-mode/
					try
					{
						if (FL.EntryAssemblyLocation != null)
						{
							using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
							{
								if (key != null)
								{
									AppCompatLayer = key.GetValue(FL.EntryAssemblyLocation) as string;
								}
							}
						}
					}
					catch
					{
					}

					// Detect OS installation language
					int languageId;
					string languageStr = GetWmiIdentifier("Win32_OperatingSystem", "OSLanguage");
					if (languageStr != null)
					{
						if (int.TryParse(languageStr, out languageId))
						{
							CultureInfo ci = new CultureInfo(languageId);
							Language = ci.Name;
						}
						else
						{
							Language = languageStr;
						}
					}

					// Read the last boot time
					// Environment.TickCount can only represent 49.8 days, WMI is more stable here
					try
					{
						LastBootTime = System.Management.ManagementDateTimeConverter.ToDateTime(GetWmiIdentifier("Win32_OperatingSystem", "LastBootUpTime")).ToUniversalTime();
					}
					catch
					{
					}

					// Detect version and edition
					if (version.StartsWith("5.0", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.Windows2000;
							Is64Bit = false;
							Edition = OSEdition.Windows2000Professional;
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.Windows2000Server;
							Is64Bit = false;
							if ((productSuite & VER_SUITE_DATACENTER) != 0)
							{
								Edition = OSEdition.Windows2000DatacenterServer;
							}
							else if ((productSuite & VER_SUITE_ENTERPRISE) != 0)
							{
								Edition = OSEdition.Windows2000AdvancedServer;
							}
							else
							{
								Edition = OSEdition.Windows2000Server;
							}
						}
					}
					else if (version.StartsWith("5.1", StringComparison.Ordinal))
					{
						Version = OSVersion.WindowsXP;
						Is64Bit = false;
						if ((productSuite & VER_SUITE_PERSONAL) != 0)
						{
							Edition = OSEdition.WindowsXPHome;
						}
						else if (GetSystemMetrics(SM_MEDIACENTER) != 0)
						{
							// Alternative registry key: SOFTWARE\Microsoft\Windows\CurrentVersion\Media Center\Ident
							Edition = OSEdition.WindowsXPMediaCenter;
						}
						else if (GetSystemMetrics(SM_TABLETPC) != 0)
						{
							// Alternative registry key: SOFTWARE\Microsoft\Windows\CurrentVersion\Tablet PC\Ident
							Edition = OSEdition.WindowsXPTabletPC;
						}
						else if (GetSystemMetrics(SM_STARTER) != 0)
						{
							Edition = OSEdition.WindowsXPStarter;
						}
						else
						{
							Edition = OSEdition.WindowsXPProfessional;
						}
					}
					else if (version.StartsWith("5.2", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.WindowsXP;
							Is64Bit = true;
							Edition = OSEdition.WindowsXPProfessionalX64;
							// SP1: Build 3790
						}
						else
						{
							Type = OSType.Server;
							if (GetSystemMetrics(SM_SERVERR2) == 0)
							{
								Version = OSVersion.WindowsServer2003;
							}
							else
							{
								Version = OSVersion.WindowsServer2003R2;
							}

							if ((productSuite & VER_SUITE_COMPUTE_SERVER) != 0)
							{
								Edition = OSEdition.WindowsServer2003Cluster;
							}
							else if ((productSuite & VER_SUITE_DATACENTER) != 0)
							{
								Edition = OSEdition.WindowsServer2003Datacenter;
							}
							else if ((productSuite & VER_SUITE_ENTERPRISE) != 0)
							{
								Edition = OSEdition.WindowsServer2003Enterprise;
							}
							else if ((productSuite & VER_SUITE_BLADE) != 0)
							{
								Edition = OSEdition.WindowsServer2003Web;
							}
							else if ((productSuite & VER_SUITE_STORAGE_SERVER) != 0)
							{
								Edition = OSEdition.WindowsServer2003Storage;
							}
							else if ((productSuite & VER_SUITE_SMALLBUSINESS_RESTRICTED) != 0)
							{
								Edition = OSEdition.WindowsServer2003SmallBusiness;
							}
							else if ((productSuite & VER_SUITE_WH_SERVER) != 0)
							{
								Edition = OSEdition.WindowsServer2003Home;
							}
							else
							{
								Edition = OSEdition.WindowsServer2003Standard;
							}
						}
					}
					else if (version.StartsWith("6.0", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.WindowsVista;

							switch (sku)
							{
								case 11:
								case 47:
								case 66:
									Edition = OSEdition.WindowsVistaStarter;
									break;
								case 2:
								case 5:
								case 67:
									Edition = OSEdition.WindowsVistaHomeBasic;
									break;
								case 3:
								case 26:
								case 68:
									Edition = OSEdition.WindowsVistaHomePremium;
									break;
								case 6:
								case 16:
								case 69:
									Edition = OSEdition.WindowsVistaBusiness;
									break;
								case 4:
								case 27:
								case 70:
								case 84:
									Edition = OSEdition.WindowsVistaEnterprise;
									break;
								case 1:
								case 28:
								case 71:
									Edition = OSEdition.WindowsVistaUltimate;
									break;
								default:
									// Oops, try something else
									if (GetSystemMetrics(SM_STARTER) != 0)
									{
										Edition = OSEdition.WindowsVistaStarter;
									}
									else if ((productSuite & VER_SUITE_PERSONAL) != 0)
									{
										if (ReadRegistryVersionValue("EditionId") == "Home Premium")
										{
											Edition = OSEdition.WindowsVistaHomePremium;
										}
										else
										{
											Edition = OSEdition.WindowsVistaHomeBasic;
										}
									}
									else
									{
										if (ReadRegistryVersionValue("EditionId") == "Ultimate")
										{
											Edition = OSEdition.WindowsVistaUltimate;
										}
										else if (ReadRegistryVersionValue("EditionId") == "Enterprise")
										{
											Edition = OSEdition.WindowsVistaEnterprise;
										}
										else
										{
											Edition = OSEdition.WindowsVistaBusiness;
										}
									}
									break;
							}
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.WindowsServer2008;

							switch (sku)
							{
								case 33:
									Edition = OSEdition.WindowsServer2008Foundation;
									break;
								case 7:
								case 13:
								case 36:
								case 40:
								case 79:
									Edition = OSEdition.WindowsServer2008Standard;
									break;
								case 10:
								case 14:
								case 38:
								case 41:
								case 72:
									Edition = OSEdition.WindowsServer2008Enterprise;
									break;
								case 8:
								case 12:
								case 37:
								case 39:
								case 80:
									Edition = OSEdition.WindowsServer2008Datacenter;
									break;
								case 18:
									Edition = OSEdition.WindowsServer2008Hpc;
									break;
								case 17:
								case 29:
									Edition = OSEdition.WindowsServer2008Web;
									break;
								case 20:
								case 21:
								case 22:
								case 23:
								case 43:
								case 44:
								case 45:
								case 46:
								case 95:
								case 96:
									Edition = OSEdition.WindowsServer2008Storage;
									break;
								case 9:
								case 63:
									Edition = OSEdition.WindowsServer2008SmallBusiness;
									break;
								case 30:
								case 31:
								case 32:
									Edition = OSEdition.WindowsServer2008EssentialBusiness;
									break;
								default:
									// Oops, try something else
									// NOTE: Editions not supported in this branch.
									// Registry value InstallationType is not yet supported in Server 2008 (not R2)
									if (!File.Exists(Path.Combine(Environment.GetEnvironmentVariable("windir"), "explorer.exe")))
									{
										Type = OSType.ServerCore;
									}
									break;
							}
							switch (sku)
							{
								case 12:
								case 13:
								case 14:
								case 29:
								case 39:
								case 40:
								case 41:
								case 43:
								case 44:
								case 45:
								case 46:
								case 63:
									Type = OSType.ServerCore;
									break;
							}
						}
					}
					else if (version.StartsWith("6.1", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.Windows7;

							switch (sku)
							{
								case 11:
								case 47:
								case 66:
									Edition = OSEdition.Windows7Starter;
									break;
								case 2:
								case 5:
								case 67:
									Edition = OSEdition.Windows7HomeBasic;
									break;
								case 3:
								case 26:
								case 68:
									Edition = OSEdition.Windows7HomePremium;
									break;
								case 48:
								case 49:
								case 69:
								case 103:
									Edition = OSEdition.Windows7Professional;
									break;
								case 4:
								case 27:
								case 70:
								case 84:
									Edition = OSEdition.Windows7Enterprise;
									break;
								case 1:
								case 28:
								case 71:
									Edition = OSEdition.Windows7Ultimate;
									break;
								default:
									// Oops, try something else
									if (GetSystemMetrics(SM_STARTER) != 0)
									{
										Edition = OSEdition.WindowsVistaStarter;
									}
									else if ((productSuite & VER_SUITE_PERSONAL) != 0)
									{
										if (ReadRegistryVersionValue("EditionId") == "Home Premium")
										{
											Edition = OSEdition.Windows7HomePremium;
										}
										else
										{
											Edition = OSEdition.Windows7HomeBasic;
										}
									}
									else
									{
										if (ReadRegistryVersionValue("EditionId") == "Ultimate")
										{
											Edition = OSEdition.Windows7Ultimate;
										}
										else if (ReadRegistryVersionValue("EditionId") == "Enterprise")
										{
											Edition = OSEdition.Windows7Enterprise;
										}
										else
										{
											Edition = OSEdition.Windows7Professional;
										}
									}
									break;
							}
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.WindowsServer2008R2;

							switch (sku)
							{
								case 33:
									Edition = OSEdition.WindowsServer2008Foundation;
									break;
								case 7:
								case 13:
								case 36:
								case 40:
								case 79:
									Edition = OSEdition.WindowsServer2008Standard;
									break;
								case 10:
								case 14:
								case 38:
								case 41:
								case 72:
									Edition = OSEdition.WindowsServer2008Enterprise;
									break;
								case 8:
								case 12:
								case 37:
								case 39:
								case 80:
									Edition = OSEdition.WindowsServer2008Datacenter;
									break;
								case 18:
									Edition = OSEdition.WindowsServer2008Hpc;
									break;
								case 17:
								case 29:
									Edition = OSEdition.WindowsServer2008Web;
									break;
								case 19:
								case 20:
								case 21:
								case 22:
								case 23:
								case 43:
								case 44:
								case 45:
								case 46:
								case 95:
								case 96:
									Edition = OSEdition.WindowsServer2008Storage;
									break;
								case 9:
								case 63:
									Edition = OSEdition.WindowsServer2008SmallBusiness;
									break;
								case 30:
								case 31:
								case 32:
									Edition = OSEdition.WindowsServer2008EssentialBusiness;
									break;
								default:
									// Oops, try something else
									// NOTE: Editions not supported in this branch.
									if (ReadRegistryVersionValue("InstallationType") == "Server Core")
									{
										Type = OSType.ServerCore;
									}
									break;
							}
							switch (sku)
							{
								case 12:
								case 13:
								case 14:
								case 29:
								case 39:
								case 40:
								case 41:
								case 43:
								case 44:
								case 45:
								case 46:
								case 63:
									Type = OSType.ServerCore;
									break;
							}
						}
					}
					else if (version.StartsWith("6.2", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.Windows8;

							switch (sku)
							{
								case 98:
								case 99:
								case 100:
								case 101:
									Edition = OSEdition.Windows8Core;
									break;
								case 48:
								case 49:
								case 69:
								case 103:
									Edition = OSEdition.Windows8Pro;
									break;
								case 4:
								case 27:
								case 70:
								case 84:
									Edition = OSEdition.Windows8Enterprise;
									break;
								default:
									// Oops, try something else
									if ((productSuite & VER_SUITE_PERSONAL) != 0)
									{
										Edition = OSEdition.Windows8Core;
									}
									else
									{
										if (ReadRegistryVersionValue("EditionId") == "Enterprise")
										{
											Edition = OSEdition.Windows8Enterprise;
										}
										else
										{
											Edition = OSEdition.Windows8Pro;
										}
									}
									break;
							}
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.WindowsServer2012;

							switch (sku)
							{
								case 33:
									Edition = OSEdition.WindowsServer2012Foundation;
									break;
								case 59:
								case 60:
								case 61:
								case 62:
									Edition = OSEdition.WindowsServer2012Essentials;
									break;
								case 7:
								case 13:
								case 36:
								case 40:
								case 79:
									Edition = OSEdition.WindowsServer2012Standard;
									break;
								case 8:
								case 12:
								case 37:
								case 39:
								case 80:
									Edition = OSEdition.WindowsServer2012Datacenter;
									break;
								default:
									// Oops, try something else
									// NOTE: Editions not supported in this branch.
									if (ReadRegistryVersionValue("InstallationType") == "Server Core")
									{
										Type = OSType.ServerCore;
									}
									break;
							}
							switch (sku)
							{
								case 12:
								case 13:
								case 14:
								case 29:
								case 39:
								case 40:
								case 41:
								case 43:
								case 44:
								case 45:
								case 46:
								case 63:
									Type = OSType.ServerCore;
									break;
							}
						}
					}
					else if (version.StartsWith("6.3", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.Windows81;

							switch (sku)
							{
								case 98:
								case 99:
								case 100:
								case 101:
									Edition = OSEdition.Windows8Core;
									break;
								case 48:
								case 49:
								case 69:
								case 103:
									Edition = OSEdition.Windows8Pro;
									break;
								case 4:
								case 27:
								case 70:
								case 84:
									Edition = OSEdition.Windows8Enterprise;
									break;
								default:
									// Oops, try something else
									if ((productSuite & VER_SUITE_PERSONAL) != 0)
									{
										Edition = OSEdition.Windows8Core;
									}
									else
									{
										if (ReadRegistryVersionValue("EditionId") == "Enterprise")
										{
											Edition = OSEdition.Windows8Enterprise;
										}
										else
										{
											Edition = OSEdition.Windows8Pro;
										}
									}
									break;
							}
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.WindowsServer2012R2;

							switch (sku)
							{
								case 33:
									Edition = OSEdition.WindowsServer2012Foundation;
									break;
								case 59:
								case 60:
								case 61:
								case 62:
									Edition = OSEdition.WindowsServer2012Essentials;
									break;
								case 7:
								case 13:
								case 36:
								case 40:
								case 79:
									Edition = OSEdition.WindowsServer2012Standard;
									break;
								case 8:
								case 12:
								case 37:
								case 39:
								case 80:
									Edition = OSEdition.WindowsServer2012Datacenter;
									break;
								default:
									// Oops, try something else
									// NOTE: Editions not supported in this branch.
									if (ReadRegistryVersionValue("InstallationType") == "Server Core")
									{
										Type = OSType.ServerCore;
									}
									break;
							}
							switch (sku)
							{
								case 12:
								case 13:
								case 14:
								case 29:
								case 39:
								case 40:
								case 41:
								case 43:
								case 44:
								case 45:
								case 46:
								case 63:
									Type = OSType.ServerCore;
									break;
							}
						}
					}
					else if (version.StartsWith("10.0", StringComparison.Ordinal))
					{
						if (productType == VER_NT_WORKSTATION)
						{
							Version = OSVersion.Windows10;

							switch (sku)
							{
								case 98:
								case 99:
								case 100:
								case 101:
									Edition = OSEdition.Windows10Home;
									break;
								case 48:
								case 49:
								case 69:
								case 103:
									Edition = OSEdition.Windows10Pro;
									break;
								case 4:
								case 27:
								case 70:
								case 84:
									Edition = OSEdition.Windows10Enterprise;
									break;
								default:
									// Oops, try something else
									if ((productSuite & VER_SUITE_PERSONAL) != 0)
									{
										Edition = OSEdition.Windows10Home;
									}
									else
									{
										if (ReadRegistryVersionValue("EditionId") == "Enterprise")
										{
											Edition = OSEdition.Windows10Enterprise;
										}
										else
										{
											Edition = OSEdition.Windows10Pro;
										}
									}
									break;
							}
						}
						else
						{
							Type = OSType.Server;
							Version = OSVersion.WindowsServer2016;
							// TODO: Server editions to be determined
						}
					}
					else if (FL.CompareVersions(version, "10") > 0)
					{
						Version = OSVersion.WindowsFuture;
					}
				}
			}
			else
			{
				// Probably Mono
				Version = OSVersion.NonWindows;
			}
		}

		#endregion Static constructor

		#region Windows account methods

		/// <summary>
		/// Determines whether the logged on Windows user is member of the specified Windows group.
		/// </summary>
		/// <param name="groupName">Group name in the format "Domain\Group".</param>
		/// <returns>true, if the user is member of the group, false otherwise.</returns>
		public static bool IsCurrentUserInWindowsGroup(string groupName)
		{
			// Based on: http://www.mycsharp.de/wbb2/thread.php?threadid=36895
			WindowsIdentity identity = WindowsIdentity.GetCurrent();
			if (!identity.IsAuthenticated)
			{
				throw new SecurityException("The current Windows user is not authenticated.");
			}
			try
			{
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole(groupName);
			}
			catch (SystemException)
			{
				// The group is not found. If the group is not created then the user cannot be
				// member of the group so just return false.
				return false;
			}
		}

		/// <summary>
		/// Determines whether the logged on Windows user is member of the specified Windows group.
		/// </summary>
		/// <param name="wellKnownSidType">A value of the set of commonly used security identifiers (SIDs).</param>
		/// <returns>true, if the user is member of the group, false otherwise.</returns>
		public static bool IsCurrentUserInWindowsGroup(WellKnownSidType wellKnownSidType)
		{
			WindowsIdentity identity = WindowsIdentity.GetCurrent();
			if (!identity.IsAuthenticated)
			{
				throw new SecurityException("The current Windows user is not authenticated.");
			}
			try
			{
				SecurityIdentifier sid = new SecurityIdentifier(wellKnownSidType, null);
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole(sid);
			}
			catch (SystemException)
			{
				// The group is not found. If the group is not created then the user cannot be
				// member of the group so just return false.
				return false;
			}
		}

		/// <summary>
		/// Determines whether the logged on Windows user is a local administrator.
		/// </summary>
		/// <returns>true if the user is a local administrator; otherwise, false.</returns>
		public static bool IsCurrentUserLocalAdministrator()
		{
			return IsCurrentUserInWindowsGroup(WellKnownSidType.BuiltinAdministratorsSid);
		}

		/// <summary>
		/// Determines whether the logged on Windows user is a domain administrator.
		/// </summary>
		/// <returns>true if the user is a domain administrator; otherwise, false.</returns>
		public static bool IsCurrentUserDomainAdministrator()
		{
			return IsCurrentUserInWindowsGroup(WellKnownSidType.AccountDomainAdminsSid);
		}

		/// <summary>
		/// Determines whether the logged on Windows user is the local system account.
		/// </summary>
		/// <returns>true if the user is the local system account; otherwise, false.</returns>
		public static bool IsCurrentUserLocalSystem()
		{
			return IsCurrentUserInWindowsGroup(WellKnownSidType.LocalSystemSid);
		}

		/// <summary>
		/// Determines whether the logged on Windows user is the local service account.
		/// </summary>
		/// <returns>true if the user is the local service account; otherwise, false.</returns>
		public static bool IsCurrentUserLocalService()
		{
			return IsCurrentUserInWindowsGroup(WellKnownSidType.LocalServiceSid);
		}

		/// <summary>
		/// Determines whether the logged on Windows user is the network service account.
		/// </summary>
		/// <returns>true if the user is the network service account; otherwise, false.</returns>
		public static bool IsCurrentUserNetworkService()
		{
			return IsCurrentUserInWindowsGroup(WellKnownSidType.NetworkServiceSid);
		}

		#endregion Windows account methods

		#region Memory usage methods

		/// <summary>
		/// Gets the private memory currently used by this process.
		/// </summary>
		/// <returns>The private memory currently used by this process, in bytes.</returns>
		public static long GetProcessPrivateMemory()
		{
			return System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
		}

		/// <summary>
		/// Gets the peak working set memory used by this process.
		/// </summary>
		/// <returns>The peak working set memory used by this process, in bytes.</returns>
		public static long GetProcessPeakMemory()
		{
			return System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64;
		}

		/// <summary>
		/// Gets the amount of total visible memory on the computer. This is the installed physical
		/// memory, excluding the memory reserved for hardware or that cannot be addressed.
		/// </summary>
		/// <returns>The amount of total visible memory on the computer, in bytes.</returns>
		public static long GetTotalMemorySize()
		{
			long mem;
			long.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "TotalVisibleMemorySize"), out mem);
			mem *= 1024;
			return mem;
		}

		/// <summary>
		/// Gets the amount of available memory on the computer. This is the unused physical
		/// memory, plus the memory currently used for cache.
		/// </summary>
		/// <returns>The amount of available memory on the computer, in bytes.</returns>
		public static long GetAvailableMemorySize()
		{
			long mem;
			long.TryParse(GetWmiIdentifier("Win32_OperatingSystem", "FreePhysicalMemory"), out mem);
			mem *= 1024;
			return mem;
		}

		#endregion Memory usage methods

		#region Helper methods

		private static string ParseSPNumber(string spString)
		{
			if (spString != null && spString.Length > 13 && spString.StartsWith("Service Pack ", StringComparison.Ordinal))
			{
				return spString.Substring(13);
			}
			return spString;
		}

		private static string ReadRegistryVersionValue(string name)
		{
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
				{
					return key.GetValue(name) as string;
				}
			}
			catch
			{
				return null;
			}
		}

		private static int ReadRegistryVersionIntValue(string name)
		{
			string str = ReadRegistryVersionValue(name);
			int i;
			int.TryParse(str, out i);
			return i;
		}

		/// <summary>
		/// Reads a value from the WMI interface.
		/// </summary>
		/// <param name="wmiClass">The WMI management class name, e. g. "Win32_LogicalDisk" or "Win32_Process".</param>
		/// <param name="wmiProperty">The name of the property of interest.</param>
		/// <returns>The value of the requested property.</returns>
		public static string GetWmiIdentifier(string wmiClass, string wmiProperty)
		{
			return GetWmiIdentifier(wmiClass, wmiProperty, null);
		}

		/// <summary>
		/// Reads a value from the WMI interface.
		/// </summary>
		/// <param name="wmiClass">The WMI management class name, e. g. "Win32_LogicalDisk" or "Win32_Process".</param>
		/// <param name="wmiProperty">The name of the property of interest.</param>
		/// <param name="wmiCondition">If not null, the name of the property whose value must be true (boolean) to select the object instance.</param>
		/// <returns>The value of the requested property.</returns>
		public static string GetWmiIdentifier(string wmiClass, string wmiProperty, string wmiCondition)
		{
			System.Management.ManagementClass mc = new System.Management.ManagementClass(wmiClass);
			foreach (System.Management.ManagementObject mo in mc.GetInstances())
			{
				if (wmiCondition == null || (mo[wmiCondition] is bool && (bool)mo[wmiCondition]))
				{
					try
					{
						object value = mo[wmiProperty];
						if (value != null)
						{
							return value.ToString().Trim();
						}
					}
					catch
					{
					}
				}
			}
			return null;
		}

		#endregion Helper methods

		#region OS version helper properties

		/// <summary>
		/// Gets a value indicating whether the OS version is Windows XP/Server 2003 or newer.
		/// </summary>
		public static bool IsWindowsXPOrNewer
		{
			get
			{
				return OSInfo.Type == OSType.Client && OSInfo.Version >= OSVersion.WindowsXP ||
					(OSInfo.Type == OSType.Server || OSInfo.Type == OSType.ServerCore) && OSInfo.Version >= OSVersion.WindowsServer2003;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the OS version is Windows Vista/Server 2008 or newer.
		/// </summary>
		public static bool IsWindowsVistaOrNewer
		{
			get
			{
				return OSInfo.Type == OSType.Client && OSInfo.Version >= OSVersion.WindowsVista ||
					(OSInfo.Type == OSType.Server || OSInfo.Type == OSType.ServerCore) && OSInfo.Version >= OSVersion.WindowsServer2008;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the OS version is Windows 7/Server 2008 R2 or newer.
		/// </summary>
		public static bool IsWindows7OrNewer
		{
			get
			{
				return OSInfo.Type == OSType.Client && OSInfo.Version >= OSVersion.Windows7 ||
					(OSInfo.Type == OSType.Server || OSInfo.Type == OSType.ServerCore) && OSInfo.Version >= OSVersion.WindowsServer2008R2;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the OS version is Windows 8/Server 2012 or newer.
		/// </summary>
		public static bool IsWindows8OrNewer
		{
			get
			{
				return OSInfo.Type == OSType.Client && OSInfo.Version >= OSVersion.Windows8 ||
					(OSInfo.Type == OSType.Server || OSInfo.Type == OSType.ServerCore) && OSInfo.Version >= OSVersion.WindowsServer2012;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the OS version is Windows 10 (Server TBD) or newer.
		/// </summary>
		public static bool IsWindows10OrNewer
		{
			get
			{
				return OSInfo.Type == OSType.Client && OSInfo.Version >= OSVersion.Windows10 ||
					(OSInfo.Type == OSType.Server || OSInfo.Type == OSType.ServerCore) && OSInfo.Version >= OSVersion.WindowsServer2016;
			}
		}

		#endregion OS version helper properties
	}

	#endregion OSInfo class

	#region Type, Version and Edition enumerations

	/// <summary>
	/// Defines operating system type values.
	/// </summary>
	public enum OSType
	{
		// NOTE: The numeric values are stored in log files. Do not change existing values.

		/// <summary>A client system.</summary>
		[Description("Client")]
		Client,
		/// <summary>A server system.</summary>
		[Description("Server")]
		Server,
		/// <summary>A server core system.</summary>
		[Description("Server Core")]
		ServerCore
	}

	/// <summary>
	/// Defines operating system version values.
	/// </summary>
	public enum OSVersion
	{
		// NOTE: The numeric values are stored in log files. Do not change existing values. The
		// order of the items is laid out so that new Windows versions can be added at the end of
		// each the client and server section (just before a field with a specified number) without
		// pushing the following group behind.

		/// <summary>Unknown version.</summary>
		[Description("Unknown")]
		Unknown = 0,

		/// <summary>Not Windows.</summary>
		[Description("Not Windows")]
		NonWindows = 1,

		/// <summary>Windows 98.</summary>
		[Description("Windows 98")]
		Windows98 = 10,
		/// <summary>Windows 98 SE.</summary>
		[Description("Windows 98 SE")]
		Windows98SE,
		/// <summary>Windows ME.</summary>
		[Description("Windows ME")]
		WindowsME,
		/// <summary>Windows NT 4.</summary>
		[Description("Windows NT 4")]
		WindowsNT4,
		/// <summary>Windows 2000.</summary>
		[Description("Windows 2000")]
		Windows2000,
		/// <summary>Windows XP.</summary>
		[Description("Windows XP")]
		WindowsXP,
		/// <summary>Windows Vista.</summary>
		[Description("Windows Vista")]
		WindowsVista,
		/// <summary>Windows 7.</summary>
		[Description("Windows 7")]
		Windows7,
		/// <summary>Windows 8.</summary>
		[Description("Windows 8")]
		Windows8,
		/// <summary>Windows 8.1.</summary>
		[Description("Windows 8.1")]
		Windows81,
		/// <summary>Windows 10.</summary>
		[Description("Windows 10")]
		Windows10,

		/// <summary>Windows 2000 Server.</summary>
		[Description("Windows 2000 Server")]
		Windows2000Server = 100,
		/// <summary>Windows Home Server.</summary>
		[Description("Windows Home Server")]
		WindowsHomeServer,
		/// <summary>Windows Server 2003.</summary>
		[Description("Windows Server 2003")]
		WindowsServer2003,
		/// <summary>Windows Server 2003 R2.</summary>
		[Description("Windows Server 2003 R2")]
		WindowsServer2003R2,
		/// <summary>Windows Server 2008.</summary>
		[Description("Windows Server 2008")]
		WindowsServer2008,
		/// <summary>Windows Server 2008 R2.</summary>
		[Description("Windows Server 2008 R2")]
		WindowsServer2008R2,
		/// <summary>Windows Server 2012.</summary>
		[Description("Windows Server 2012")]
		WindowsServer2012,
		/// <summary>Windows Server 2012 R2.</summary>
		[Description("Windows Server 2012 R2")]
		WindowsServer2012R2,
		/// <summary>Windows Server 2016.</summary>
		[Description("Windows Server 2016")]
		WindowsServer2016,

		/// <summary>A future version of Windows not yet known by this implementation.</summary>
		[Description("Future Windows version")]
		WindowsFuture = 200
	}

	/// <summary>
	/// Defines operating system edition values.
	/// </summary>
	public enum OSEdition
	{
		// NOTE: The numeric values are stored in log files. Do not change existing values. The
		// order of the items is laid out so that new Windows versions can be added at the end of
		// each the client and server section (just before a field with a specified number) without
		// pushing the following group behind.

		/// <summary>No special edition.</summary>
		[Description("No special edition")]
		None = 0,

		/// <summary>Windows 2000 Professional.</summary>
		[Description("Windows 2000 Professional")]
		Windows2000Professional,
		/// <summary>Windows 2000 Server.</summary>
		[Description("Windows 2000 Server")]
		Windows2000Server,
		/// <summary>Windows 2000 Advanced Server.</summary>
		[Description("Windows 2000 Advanced Server")]
		Windows2000AdvancedServer,
		/// <summary>Windows 2000 Datacenter Server.</summary>
		[Description("Windows 2000 Datacenter Server")]
		Windows2000DatacenterServer,

		/// <summary>Windows XP Starter.</summary>
		[Description("Windows XP Starter")]
		WindowsXPStarter,
		/// <summary>Windows XP Home.</summary>
		[Description("Windows XP Home")]
		WindowsXPHome,
		/// <summary>Windows XP Professional.</summary>
		[Description("Windows XP Professional")]
		WindowsXPProfessional,
		/// <summary>Windows XP Media Center.</summary>
		[Description("Windows XP Media Center")]
		WindowsXPMediaCenter,
		/// <summary>Windows XP Tablet PC.</summary>
		[Description("Windows XP Tablet PC")]
		WindowsXPTabletPC,
		/// <summary>Windows XP Professional x64.</summary>
		[Description("Windows XP Professional x64")]
		WindowsXPProfessionalX64,

		/// <summary>Windows Vista Starter.</summary>
		[Description("Windows Vista Starter")]
		WindowsVistaStarter,
		/// <summary>Windows Vista Home Basic.</summary>
		[Description("Windows Vista Home Basic")]
		WindowsVistaHomeBasic,
		/// <summary>Windows Vista Home Premium.</summary>
		[Description("Windows Vista Home Premium")]
		WindowsVistaHomePremium,
		/// <summary>Windows Vista Business.</summary>
		[Description("Windows Vista Business")]
		WindowsVistaBusiness,
		/// <summary>Windows Vista Enterprise.</summary>
		[Description("Windows Vista Enterprise")]
		WindowsVistaEnterprise,
		/// <summary>Windows Vista Ultimate.</summary>
		[Description("Windows Vista Ultimate")]
		WindowsVistaUltimate,

		/// <summary>Windows 7 Starter.</summary>
		[Description("Windows 7 Starter")]
		Windows7Starter,
		/// <summary>Windows 7 Home Basic.</summary>
		[Description("Windows 7 Home Basic")]
		Windows7HomeBasic,
		/// <summary>Windows 7 Home Premium.</summary>
		[Description("Windows 7 Home Premium")]
		Windows7HomePremium,
		/// <summary>Windows 7 Professional.</summary>
		[Description("Windows 7 Professional")]
		Windows7Professional,
		/// <summary>Windows 7 Enterprise.</summary>
		[Description("Windows 7 Enterprise")]
		Windows7Enterprise,
		/// <summary>Windows 7 Ultimate.</summary>
		[Description("Windows 7 Ultimate")]
		Windows7Ultimate,

		/// <summary>Windows 8 ("Core").</summary>
		[Description("Windows 8 (\"Core\")")]
		Windows8Core,
		/// <summary>Windows 8 Pro.</summary>
		[Description("Windows 8 Pro")]
		Windows8Pro,
		/// <summary>Windows 8 Enterprise.</summary>
		[Description("Windows 8 Enterprise")]
		Windows8Enterprise,

		/// <summary>Windows 10 Home.</summary>
		[Description("Windows 10 Home")]
		Windows10Home,
		/// <summary>Windows 10 Pro.</summary>
		[Description("Windows 10 Pro")]
		Windows10Pro,
		/// <summary>Windows 10 Enterprise.</summary>
		[Description("Windows 10 Enterprise")]
		Windows10Enterprise,

		/// <summary>Windows Server 2003/2003 R2 Web.</summary>
		[Description("Windows Server 2003/2003 R2 Web")]
		WindowsServer2003Web = 100,
		/// <summary>Windows Server 2003/2003 R2 Standard.</summary>
		[Description("Windows Server 2003/2003 R2 Standard")]
		WindowsServer2003Standard,
		/// <summary>Windows Server 2003/2003 R2 Enterprise.</summary>
		[Description("Windows Server 2003/2003 R2 Enterprise")]
		WindowsServer2003Enterprise,
		/// <summary>Windows Server 2003/2003 R2 Datacenter.</summary>
		[Description("Windows Server 2003/2003 R2 Datacenter")]
		WindowsServer2003Datacenter,
		/// <summary>Windows Server 2003/2003 R2 Cluster.</summary>
		[Description("Windows Server 2003/2003 R2 Cluster")]
		WindowsServer2003Cluster,
		/// <summary>Windows Server 2003/2003 R2 Storage.</summary>
		[Description("Windows Server 2003/2003 R2 Storage")]
		WindowsServer2003Storage,
		/// <summary>Windows Server 2003/2003 R2 Small Business.</summary>
		[Description("Windows Server 2003/2003 R2 Small Business")]
		WindowsServer2003SmallBusiness,
		/// <summary>Windows Server 2003/2003 R2 Home.</summary>
		[Description("Windows Server 2003/2003 R2 Home")]
		WindowsServer2003Home,

		/// <summary>Windows Server 2008/2008 R2 Foundation.</summary>
		[Description("Windows Server 2008/2008 R2 Foundation")]
		WindowsServer2008Foundation,
		/// <summary>Windows Server 2008/2008 R2 Standard.</summary>
		[Description("Windows Server 2008/2008 R2 Standard")]
		WindowsServer2008Standard,
		/// <summary>Windows Server 2008/2008 R2 Enterprise.</summary>
		[Description("Windows Server 2008/2008 R2 Enterprise")]
		WindowsServer2008Enterprise,
		/// <summary>Windows Server 2008/2008 R2 Datacenter.</summary>
		[Description("Windows Server 2008/2008 R2 Datacenter")]
		WindowsServer2008Datacenter,
		/// <summary>Windows Server 2008/2008 R2 HPC.</summary>
		[Description("Windows Server 2008/2008 R2 HPC")]
		WindowsServer2008Hpc,
		/// <summary>Windows Server 2008/2008 R2 Web.</summary>
		[Description("Windows Server 2008/2008 R2 Web")]
		WindowsServer2008Web,
		/// <summary>Windows Server 2008/2008 R2 Storage.</summary>
		[Description("Windows Server 2008/2008 R2 Storage")]
		WindowsServer2008Storage,
		/// <summary>Windows Server 2008/2008 R2 Small Business.</summary>
		[Description("Windows Server 2008/2008 R2 Small Business")]
		WindowsServer2008SmallBusiness,
		/// <summary>Windows Server 2008/2008 R2 Essential Business.</summary>
		[Description("Windows Server 2008/2008 R2 Essential Business")]
		WindowsServer2008EssentialBusiness,

		/// <summary>Windows Server 2012/2012 R2 Foundation.</summary>
		[Description("Windows Server 2012/2012 R2 Foundation")]
		WindowsServer2012Foundation,
		/// <summary>Windows Server 2012/2012 R2 Essentials.</summary>
		[Description("Windows Server 2012/2012 R2 Essentials")]
		WindowsServer2012Essentials,
		/// <summary>Windows Server 2012/2012 R2 Standard.</summary>
		[Description("Windows Server 2012/2012 R2 Standard")]
		WindowsServer2012Standard,
		/// <summary>Windows Server 2012/2012 R2 Datacenter.</summary>
		[Description("Windows Server 2012/2012 R2 Datacenter")]
		WindowsServer2012Datacenter,
	}

	#endregion Type, Version and Edition enumerations
}
