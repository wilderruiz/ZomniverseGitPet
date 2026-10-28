$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src/ZomniverseGitPet/GuardianForm.cs'

if (-not (Test-Path -LiteralPath $target)) {
    throw "GuardianForm.cs not found at $target"
}

$text = [System.IO.File]::ReadAllText($target)
$failures = New-Object System.Collections.Generic.List[string]

function Expect-Contains {
    param([string]$Needle, [string]$Label)
    if (-not $text.Contains($Needle)) {
        $failures.Add("missing: $Label")
    }
}

function Expect-NotContains {
    param([string]$Needle, [string]$Label)
    if ($text.Contains($Needle)) {
        $failures.Add("unexpected legacy contract: $Label")
    }
}

Expect-Contains 'ZGITPET_RESPONSIVE_REPOSITORY_HEADER_2026_10_28' 'responsive header marker'
Expect-Contains 'repositoryCardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));' 'dedicated repository strip row'
Expect-Contains 'repositoryCardLayout.Controls.Add(repositoryLinkRow, 0, 1);' 'repository strip placement'
Expect-Contains '_repositoryWebLink.AutoEllipsis = true;' 'repository URL visual ellipsis'
Expect-Contains '_chatVitality.MinimumSize = new Size(280, 0);' 'compressible Chat Vitality minimum'
Expect-Contains 'Math.Clamp((int)Math.Round(availableWidth * 0.34), 300, 410)' 'responsive status width floor'
Expect-Contains 'Math.Clamp((int)Math.Round(summaryWidth * 0.47), 280, 428)' 'responsive Chat Vitality width floor'
Expect-Contains 'repositoryCard.Resize += (_, _) => UpdateResponsiveRepositoryHeader();' 'live resize hook'
Expect-Contains 'summary.SetRowSpan(_chatVitality, 3);' 'three-row summary span'
Expect-NotContains 'summary.Controls.Add(repositoryLinkRow, 0, 3);' 'repository identity trapped inside summary'
Expect-NotContains '_repositoryWebLink.AutoEllipsis = false;' 'non-responsive repository URL'
Expect-NotContains '_chatVitality.MinimumSize = new Size(410, 220);' 'fixed Chat Vitality minimum'

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error "FAIL $failure" -ErrorAction Continue
    }
    exit 1
}

Write-Output 'PASS responsive repository header structural contract.'
Write-Output 'PASS repository identity remains a dedicated full-width strip.'
Write-Output 'PASS Chat Vitality and status card widths have practical responsive floors.'
Write-Output 'PASS repository link preserves visibility through visual ellipsis.'
Write-Output 'NEXT: dotnet build ZomniverseGitPet.sln -c Release'