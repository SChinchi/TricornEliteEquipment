param(
	[Parameter(Mandatory=$true, Position=0)]
	[string]
	$assembly,
	[Parameter(Mandatory=$true, Position=1)]
	[string]
	$projectVersion,
	[Parameter(ValueFromRemainingArguments=$true)]
	[string[]]$dependencies
)
$dll = [System.Reflection.Assembly]::LoadFrom($assembly)
foreach ($lib in $dependencies)
{
	[System.Reflection.Assembly]::LoadFrom($lib) | Out-Null
}
$plugin = ($dll.GetExportedTypes() | Where-Object {$_.BaseType.Name -eq "BaseUnityPlugin"})[0]
if ($plugin.GetField("PluginVersion").GetValue($null) -ne $projectVersion)
{
	Write-Host -ForegroundColor Red "Project and source code versions are in disagreement"
	Exit 1
}