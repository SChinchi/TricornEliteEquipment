param(
	[Parameter(Mandatory=$true, Position=0)]
	[string]
	$pluginFile,
	[Parameter(Mandatory=$true, Position=1)]
	[string]
	$projectVersion
)
try
{
	$pattern = Select-String -Path $pluginFile -Pattern 'PluginVersion = "(.*)"'
	if ($pattern.matches[0].Groups[1].Value -ne $projectVersion)
	{
		Write-Host -ForegroundColor Red "Project and source code versions are in disagreement"
		Exit 1
	}
}
catch
{
	Write-Host -ForegroundColor Red "Source code file not found"
	Exit 1
}