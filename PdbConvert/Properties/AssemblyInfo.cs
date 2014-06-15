﻿using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("PdbConvert")]
[assembly: AssemblyTitle("PdbConvert")]
[assembly: AssemblyDescription("Converts .pdb debug symbols to a portable XML file.")]

[assembly: AssemblyCopyright("© Yves Goergen, GNU GPL v3")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly version, also used for Win32 file version resource.
// Must be a plain numeric version definition:
// 1. Major version number, should be increased with major new versions or rewrites of the application
// 2. Minor version number, should ne increased with minor feature changes or new features
// 3. Bugfix number, should be set or increased for bugfix releases of a previous version
// 4. Unused
[assembly: AssemblyVersion("1.0.0")]
// Informational version string, used for the About dialog, error reports and the setup script.
// Can be any freely formatted string containing punctuation, letters and revision codes.
// Should be set to the same value as AssemblyVersion if only the basic numbering scheme is applied.
[assembly: AssemblyInformationalVersion("{bmin:2014:4}.{commit:6}{!:+}")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif