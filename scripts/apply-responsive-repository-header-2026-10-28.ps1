$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src/ZomniverseGitPet/GuardianForm.cs'
$marker = 'ZGITPET_RESPONSIVE_REPOSITORY_HEADER_2026_10_28'

if (-not (Test-Path -LiteralPath $target)) {
    throw "GuardianForm.cs not found at $target"
}

$text = [System.IO.File]::ReadAllText($target)

if ($text.Contains($marker)) {
    Write-Output 'PASS responsive repository header already applied.'
    exit 0
}

function Replace-Once {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Before,
        [Parameter(Mandatory = $true)][string]$After,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $first = $Source.IndexOf($Before, [StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "$Label anchor not found."
    }

    $second = $Source.IndexOf($Before, $first + $Before.Length, [StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "$Label anchor is ambiguous."
    }

    return $Source.Substring(0, $first) + $After + $Source.Substring($first + $Before.Length)
}

$beforeSummary = @'
        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Margin = new Padding(0, 0, 28, 0),
            BackColor = GuardianTheme.Surface
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 428));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
'@

$afterSummary = @'
        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 0, 28, 0),
            BackColor = GuardianTheme.Surface
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 428));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
'@
$text = Replace-Once $text $beforeSummary $afterSummary 'summary row contract'

$text = Replace-Once $text `
    '        _chatVitality.MinimumSize = new Size(410, 220);' `
    '        _chatVitality.MinimumSize = new Size(280, 0);' `
    'Chat Vitality responsive minimum'

$text = Replace-Once $text `
    '        _repositoryWebLink.AutoEllipsis = false;' `
    '        _repositoryWebLink.AutoEllipsis = true;' `
    'repository link ellipsis'

$beforePlacement = @'
        summary.Controls.Add(projectHeading, 0, 0);
        summary.Controls.Add(overview, 0, 1);
        summary.Controls.Add(commitArea, 0, 2);
        summary.Controls.Add(repositoryLinkRow, 0, 3);
        summary.Controls.Add(_chatVitality, 1, 0);
        summary.SetRowSpan(_chatVitality, 4);
'@

$afterPlacement = @'
        summary.Controls.Add(projectHeading, 0, 0);
        summary.Controls.Add(overview, 0, 1);
        summary.Controls.Add(commitArea, 0, 2);
        summary.Controls.Add(_chatVitality, 1, 0);
        summary.SetRowSpan(_chatVitality, 3);
'@
$text = Replace-Once $text $beforePlacement $afterPlacement 'summary child placement'

$beforeHost = @'
        repositoryLayout.Controls.Add(summary, 0, 0);
        repositoryLayout.Controls.Add(statusCard, 1, 0);
        repositoryCard.Controls.Add(repositoryLayout);
        panel.Controls.Add(repositoryCard);
'@

$afterHost = @'
        repositoryLayout.Controls.Add(summary, 0, 0);
        repositoryLayout.Controls.Add(statusCard, 1, 0);

        // ZGITPET_RESPONSIVE_REPOSITORY_HEADER_2026_10_28
        // Repository identity is primary context, so it gets a dedicated row
        // that never competes with Chat Vitality or the status card for width.
        var repositoryCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = GuardianTheme.Surface
        };
        repositoryCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        repositoryCardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        repositoryCardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        repositoryLinkRow.Margin = new Padding(0, 4, 0, 0);
        repositoryLinkRow.Padding = new Padding(0, 4, 0, 0);
        repositoryLinkRow.Paint += (_, e) =>
        {
            using var separator = new Pen(GuardianTheme.BorderSoft);
            e.Graphics.DrawLine(separator, 0, 0, repositoryLinkRow.ClientSize.Width, 0);
        };

        repositoryCardLayout.Controls.Add(repositoryLayout, 0, 0);
        repositoryCardLayout.Controls.Add(repositoryLinkRow, 0, 1);
        repositoryCard.Controls.Add(repositoryCardLayout);

        void UpdateResponsiveRepositoryHeader()
        {
            var availableWidth = Math.Max(
                1,
                repositoryCard.ClientSize.Width - repositoryCard.Padding.Horizontal);

            // Keep the existing roomy desktop proportions, but protect practical
            // floors at the 920 px minimum window instead of squeezing project/repo context.
            var wide = availableWidth >= 1080;
            var statusWidth = wide
                ? 410
                : Math.Clamp((int)Math.Round(availableWidth * 0.34), 300, 410);

            repositoryLayout.ColumnStyles[1].SizeType = SizeType.Absolute;
            repositoryLayout.ColumnStyles[1].Width = statusWidth;

            var summaryWidth = Math.Max(1, availableWidth - statusWidth);
            var chatWidth = wide
                ? 428
                : Math.Clamp((int)Math.Round(summaryWidth * 0.47), 280, 428);

            summary.ColumnStyles[1].SizeType = SizeType.Absolute;
            summary.ColumnStyles[1].Width = chatWidth;
        }

        repositoryCard.Resize += (_, _) => UpdateResponsiveRepositoryHeader();
        repositoryCardLayout.Layout += (_, _) => UpdateResponsiveRepositoryHeader();
        UpdateResponsiveRepositoryHeader();

        panel.Controls.Add(repositoryCard);
'@
$text = Replace-Once $text $beforeHost $afterHost 'repository card responsive host'

[System.IO.File]::WriteAllText($target, $text, [System.Text.UTF8Encoding]::new($false))

Write-Output 'PASS responsive repository header APPLIED.'
Write-Output 'PASS repository identity now owns a dedicated full-width strip.'
Write-Output 'PASS Chat Vitality and status widths now adapt down to practical floors.'
Write-Output 'PASS repository URL keeps its full target while visually ellipsizing if needed.'
Write-Output 'NEXT: powershell -ExecutionPolicy Bypass -File scripts/validate-responsive-repository-header-2026-10-28.ps1'