
if (Test-Path $PSScriptRoot\publish ){
  Remove-Item -Recurse $PSScriptRoot\publish -Force | Out-Null
}
New-Item -Path "$PSScriptRoot\publish" -ItemType Directory | Out-Null
#New-Item -Path "$PSScriptRoot\src\ReportModel" -ItemType Directory | Out-Null

$outputFolder = "$PSScriptRoot\..\bin\Release\net6.0-windows"
$targetExe = "$outputFolder\ReportMaker.exe"
"Collect source files for package from $outputFolder"
if (!(Test-Path $targetExe)){
	"Not found target file $targetExe"
	EXIT 1
} 
Copy-Item $outputFolder\*.exe $PSScriptRoot\publish\ 
Copy-Item $outputFolder\*.dll $PSScriptRoot\publish\ 
Copy-Item $outputFolder\*.json $PSScriptRoot\publish\ 
Copy-Item $outputFolder\*.dat $PSScriptRoot\publish\
Copy-Item $outputFolder\*.xlsx $PSScriptRoot\publish\
Copy-Item $outputFolder\runtimes $PSScriptRoot\publish\ -Recurse
Copy-Item $PSScriptRoot\..\app.ico $PSScriptRoot\publish\app.ico


#######################动态修改ISS脚本中的版本号#################################
# 从 ver.txt 读取版本号: Version:[1.0.0.0]
$csprojFile = Get-ChildItem -Path $PSScriptRoot\..\ -Filter *.csproj -File | Select-Object -First 1
[xml]$xml = Get-Content $csprojFile
$version = $xml.Project.PropertyGroup.FileVersion
Write-Output "Update ISS version with $version"

# 更新 .iss 文件  
#define MyAppVersion "1.0.0"
$newVersion = '#define MyAppVersion "' + "$version" + '"'
Write-Output $newVersion
Remove-Item "$PSScriptRoot\SetupUtf8.iss" -ErrorAction SilentlyContinue
(Get-Content -Path "$PSScriptRoot\Setup.iss" -Encoding UTF8) |
    ForEach-Object { $_ -replace '#define MyAppVersion "\d+\.\d+\.\d+"', $newVersion } | Set-Content -Path "$PSScriptRoot\SetupUtf8.iss" -Encoding UTF8

$iscc_cmd = Get-Command "ISCC" -CommandType Application -ErrorAction Ignore
if (!$iscc_cmd) {
	"Not found the installer ISCC"
	EXIT 1
}

"Begin making installer..."
ISCC "$PSScriptRoot\SetupUtf8.iss"

if(!($?)){
	"Making installer failed"
	EXIT 1
}

Remove-Item "$PSScriptRoot\SetupUtf8.iss"

$installerName = Get-ChildItem -Path "$PSScriptRoot\Release" -Filter "*.exe" | 
             Sort-Object -Property CreationTime -Descending | 
             Select-Object -First 1 -ExpandProperty FullName
"$installerName is ready"
