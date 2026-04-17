param(
    [string]$version
)

$releaseNotes = Get-Content "$PSScriptRoot\..\..\..\docs\release-notes.md" -Encoding utf8

$isAdd = $false
$node = ""
foreach ($row in $releaseNotes) {
    if ($row -match '^##\s*v(.+)$') {
        $rowVersion = $matches[1].Trim()

        if ($isAdd -and $rowVersion -ne $version) {
            break
        }

        if ($rowVersion -eq $version) {
            $isAdd = $true
        }
    }

    if ($isAdd) {
        $node += "$row`r`n"
    }
}

if ([string]::IsNullOrWhiteSpace($node)) {
    throw "Cannot find release note section for version '$version' in docs/release-notes.md"
}

$releaseBody = $node.TrimEnd()
Write-Host $node

if ($env:GITHUB_OUTPUT) {
    $delimiter = "EOF_$([guid]::NewGuid().ToString('N'))"
    "body<<$delimiter" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    $releaseBody | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    "$delimiter" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}