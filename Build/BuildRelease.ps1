param($author, $name, $version, $url, $desc)
$payload = [ordered]@{
	name = $name
	version_number = $version
	website_url = $url
	description = $desc
	dependencies = ((Get-Content $PSScriptRoot"\dependencies.txt") -split "\r?\n").ForEach({
		if ($_.Length -gt 0) { $_ }
	})
}
$PackagePath = Resolve-Path ($PSScriptRoot + "\..\Thunderstore")
ConvertTo-Json $payload | Out-File $PackagePath"\manifest.json"
Remove-Item -Path $PackagePath"\*.zip"
Get-ChildItem $PackagePath | Compress-Archive -DestinationPath $PackagePath"\"$author"-"$name"-"$version".zip"