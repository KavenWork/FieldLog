param($config, $batchMode = "")
$scriptDir = ($MyInvocation.MyCommand.Definition | Split-Path -parent)
$sourcePath = $scriptDir | Split-Path -parent | Split-Path -parent
. (Join-Path $scriptDir "psbuildlib.ps1")

Begin-BuildScript "FieldLog" "$config" ($batchMode -eq "batch")

# Set the application version
$gitRevisionFormat = "{bmin:2014:4}.{commit:6}{!:+}"
$revId = Get-GitRevision

# Disable parallel builds for overlapping projects in a solution
$noParallelBuild = $true

# Debug builds
if (IsSelected("build-debug"))
{
	Build-Solution "FieldLog.sln" "Debug" "Any CPU" 8

	if (IsSelected("sign-lib"))
	{
		. "$sourcePath\.local\sign_config.ps1"
		Sign-File "FieldLog\bin\DebugNET40\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
		Sign-File "FieldLog\bin\DebugNET20\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
		Sign-File "FieldLog\bin\DebugASPNET40\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
	}
	if (IsSelected("sign-app"))
	{
		. "$sourcePath\.local\sign_config.ps1"
		Sign-File "FieldLogViewer\bin\Debug\FieldLogViewer.exe" "$signKeyFile" "$signPassword" 1
		Sign-File "PdbConvert\bin\Debug\PdbConvert.exe" "$signKeyFile" "$signPassword" 1
	}
}

# Release builds
if ((IsSelected("build-release")) -or (IsSelected("commit")))
{
	Build-Solution "FieldLog.sln" "Release" "Any CPU" 8
	EnsureDirExists ".local"
	Exec-Console "PdbConvert\bin\Release\PdbConvert.exe" "$sourcePath\FieldLogViewer\bin\Release\* /srcbase $sourcePath /optimize /outfile $sourcePath\.local\FieldLog-$revId.pdbx" 1

	if (IsSelected("sign-lib"))
	{
		. "$sourcePath\.local\sign_config.ps1"
		Sign-File "FieldLog\bin\ReleaseNET40\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
		Sign-File "FieldLog\bin\ReleaseNET20\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
		Sign-File "FieldLog\bin\ReleaseASPNET40\Unclassified.FieldLog.dll" "$signKeyFile" "$signPassword" 1
	}
	if (IsSelected("sign-app"))
	{
		. "$sourcePath\.local\sign_config.ps1"
		Sign-File "FieldLogViewer\bin\Release\FieldLogViewer.exe" "$signKeyFile" "$signPassword" 1
		Sign-File "PdbConvert\bin\Release\PdbConvert.exe" "$signKeyFile" "$signPassword" 1
	}
}

# Release setups
if ((IsSelected("setup-release")) -or (IsSelected("commit")))
{
	Create-Setup "Setup\FieldLog.iss" Release 1

	if (IsSelected("sign-setup"))
	{
		. "$sourcePath\.local\sign_config.ps1"
		Sign-File "Setup\bin\FieldLogSetup-$revId.exe" "$signKeyFile" "$signPassword" 1
	}
}

# Install release setup
if (IsSelected("install"))
{
	Exec-File "Setup\bin\FieldLogSetup-$revId.exe" "/norestart /verysilent" 1
}

# Commit to repository
if (IsSelected("commit"))
{
	Delete-File "Setup\bin\FieldLogSetup-$revId.exe" 0
	Delete-File ".local\FieldLog-$revId.pdbx" 0
	Git-Commit 1
}

End-BuildScript