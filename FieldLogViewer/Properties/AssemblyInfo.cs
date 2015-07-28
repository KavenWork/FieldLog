﻿using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyProduct("FieldLogViewer")]
[assembly: AssemblyTitle("FieldLogViewer")]
[assembly: AssemblyDescription("FieldLog Viewer")]
[assembly: AssemblyCopyright("© Yves Goergen, GNU GPL v3")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("1.0")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("1.0")]

// Informational version string, used for the About dialog, error reports and the setup script.
// Can be any freely formatted string containing punctuation, letters and revision codes.
[assembly: AssemblyInformationalVersion("1.{dmin:2015}_{chash:6}{!:+}")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

// Other attributes
[assembly: ComVisible(false)]
[assembly: ThemeInfo(
	// Where theme specific resource dictionaries are located
	// (used if a resource is not found in the page, or application resource dictionaries)
	ResourceDictionaryLocation.SourceAssembly,
	// Where the generic resource dictionary is located
	// (used if a resource is not found in the page, app, or any theme specific resource dictionaries)
	ResourceDictionaryLocation.SourceAssembly
)]
