Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()

$script:PackageRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$script:DefaultConfigPath = Join-Path $script:PackageRoot 'config.default.json'
$script:LocalRoot = Join-Path $env:LOCALAPPDATA 'ZomniverseGitPetPrototype'
$script:ConfigPath = Join-Path $script:LocalRoot 'config.json'
$script:AuditPath = Join-Path $script:LocalRoot 'audit.jsonl'
$script:ReadGitBackendResolved = $null
$script:WriteGitBackendResolved = $null
$script:Dashboard = $null
$script:StatusFingerprint = ''
$script:LastChangeAt = Get-Date
$script:LastAutoCheckpointFingerprint = ''
$script:AutoCheckpointBusy = $false

New-Item -ItemType Directory -Force -Path $script:LocalRoot | Out-Null
if (-not (Test-Path $script:ConfigPath)) {
    Copy-Item $script:DefaultConfigPath $script:ConfigPath
}

function Load-Config {
    try {
        $script:Config = Get-Content -Raw -Path $script:ConfigPath | ConvertFrom-Json

        # v0.1.2 config migration: use fast Windows Git for read-only monitoring when
        # available, but preserve WSL Git for staging/commits to match the repository's
        # established write workflow. Existing user config is upgraded in place.
        $changed = $false
        if (-not ($script:Config.PSObject.Properties.Name -contains 'schemaVersion')) {
            $script:Config | Add-Member -NotePropertyName schemaVersion -NotePropertyValue 2 -Force
            $changed = $true
        }
        if (-not ($script:Config.PSObject.Properties.Name -contains 'readGitBackend')) {
            $script:Config | Add-Member -NotePropertyName readGitBackend -NotePropertyValue 'auto' -Force
            $changed = $true
        }
        if (-not ($script:Config.PSObject.Properties.Name -contains 'writeGitBackend')) {
            $legacyBackend = if ($script:Config.PSObject.Properties.Name -contains 'gitBackend') { [string]$script:Config.gitBackend } else { 'wsl' }
            $writeBackend = if ($legacyBackend -eq 'windows') { 'windows' } else { 'wsl' }
            $script:Config | Add-Member -NotePropertyName writeGitBackend -NotePropertyValue $writeBackend -Force
            $changed = $true
        }
        if ($changed) {
            $script:Config | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -Path $script:ConfigPath
        }
    } catch {
        [System.Windows.Forms.MessageBox]::Show(
            "Could not read config:`r`n$($script:ConfigPath)`r`n`r`n$($_.Exception.Message)",
            'ZomniverseGitPet Prototype',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        exit 1
    }
}

function Save-Config {
    $script:Config | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -Path $script:ConfigPath
}

function Write-Audit {
    param(
        [Parameter(Mandatory=$true)][string]$Event,
        [hashtable]$Data = @{}
    )
    $obj = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        event = $Event
        data = $Data
    }
    ($obj | ConvertTo-Json -Compress -Depth 8) | Add-Content -Encoding UTF8 -Path $script:AuditPath
}

function Convert-ToWslPath {
    param([Parameter(Mandatory=$true)][string]$WindowsPath)
    if ($WindowsPath -match '^([A-Za-z]):\\(.*)$') {
        $drive = $Matches[1].ToLowerInvariant()
        $rest = $Matches[2] -replace '\\','/'
        return "/mnt/$drive/$rest"
    }
    return ($WindowsPath -replace '\\','/')
}

function Resolve-GitBackend {
    param([ValidateSet('read','write')][string]$Mode = 'read')

    if ($Mode -eq 'read' -and $script:ReadGitBackendResolved) { return $script:ReadGitBackendResolved }
    if ($Mode -eq 'write' -and $script:WriteGitBackendResolved) { return $script:WriteGitBackendResolved }

    $preferred = if ($Mode -eq 'read') { [string]$script:Config.readGitBackend } else { [string]$script:Config.writeGitBackend }
    if ([string]::IsNullOrWhiteSpace($preferred)) { $preferred = if ($Mode -eq 'read') { 'auto' } else { 'wsl' } }

    $resolved = $null
    if ($preferred -eq 'windows' -or $preferred -eq 'auto') {
        $cmd = Get-Command git.exe -ErrorAction SilentlyContinue
        if ($cmd) { $resolved = 'windows' }
    }

    if (-not $resolved -and ($preferred -eq 'wsl' -or $preferred -eq 'auto')) {
        $cmd = Get-Command wsl.exe -ErrorAction SilentlyContinue
        if ($cmd) { $resolved = 'wsl' }
    }

    if (-not $resolved) {
        if (Get-Command git.exe -ErrorAction SilentlyContinue) { $resolved = 'windows' }
        elseif (Get-Command wsl.exe -ErrorAction SilentlyContinue) { $resolved = 'wsl' }
    }

    if ($Mode -eq 'read') { $script:ReadGitBackendResolved = $resolved }
    else { $script:WriteGitBackendResolved = $resolved }
    return $resolved
}

function Invoke-Git {
    param(
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [ValidateSet('read','write')][string]$Mode = 'read'
    )

    $repo = [string]$script:Config.repoPath
    $backend = Resolve-GitBackend -Mode $Mode
    if (-not $backend) {
        return [pscustomobject]@{ ExitCode = 127; Output = 'Neither git.exe nor wsl.exe is available.'; Backend = '?' }
    }

    $invoke = {
        param($SelectedBackend)
        try {
            if ($SelectedBackend -eq 'windows') {
                $output = & git.exe -C $repo @Arguments 2>&1 | Out-String
                $code = $LASTEXITCODE
            } else {
                $wslRepo = Convert-ToWslPath $repo
                $output = & wsl.exe git -C $wslRepo @Arguments 2>&1 | Out-String
                $code = $LASTEXITCODE
            }
            return [pscustomobject]@{ ExitCode = $code; Output = $output.TrimEnd(); Backend = $SelectedBackend }
        } catch {
            return [pscustomobject]@{ ExitCode = 1; Output = $_.Exception.Message; Backend = $SelectedBackend }
        }
    }

    $result = & $invoke $backend

    # Read-only operations may prefer Windows Git for responsiveness. If Windows
    # Git cannot read this repository (for example safe.directory/installation
    # differences), transparently fall back to WSL for the session.
    if ($Mode -eq 'read' -and $backend -eq 'windows' -and $result.ExitCode -ne 0 -and (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
        $fallback = & $invoke 'wsl'
        if ($fallback.ExitCode -eq 0) {
            $script:ReadGitBackendResolved = 'wsl'
            return $fallback
        }
    }
    return $result
}

function Get-RepoStatus {
    # Include branch in the same Git process so the dashboard does not launch
    # a second Git/WSL process just to discover the branch name.
    $r = Invoke-Git -Arguments @('status','--porcelain=v1','-b','-uall') -Mode 'read'
    if ($r.ExitCode -ne 0) {
        return [pscustomobject]@{ Healthy = $false; Error = $r.Output; Files = @(); Raw = ''; Branch = '?'; Backend = $r.Backend }
    }
    $allLines = @()
    if ($r.Output) { $allLines = @($r.Output -split "`r?`n") }
    $branch = '?'
    $lines = @($allLines)
    if ($lines.Count -gt 0 -and $lines[0] -like '## *') {
        $header = $lines[0].Substring(3)
        if ($header -match '^([^\. ]+)') { $branch = $Matches[1] }
        elseif ($header) { $branch = $header }
        if ($lines.Count -gt 1) { $lines = @($lines[1..($lines.Count-1)]) } else { $lines = @() }
    }
    $files = foreach ($line in $lines) {
        if ($line.Length -lt 3) { continue }
        [pscustomobject]@{
            Status = $line.Substring(0,2)
            Path = $line.Substring(3)
        }
    }
    return [pscustomobject]@{ Healthy = $true; Error = ''; Files = @($files); Raw = ($lines -join "`n"); Branch = $branch; Backend = $r.Backend }
}

function Get-LastCommit {
    $r = Invoke-Git @('log','-1','--format=%H%x09%h%x09%ad%x09%s','--date=iso-strict')
    if ($r.ExitCode -ne 0 -or -not $r.Output) { return $null }
    $parts = $r.Output -split "`t",4
    return [pscustomobject]@{
        Hash = $parts[0]
        ShortHash = if ($parts.Count -gt 1) { $parts[1] } else { '' }
        Date = if ($parts.Count -gt 2) { $parts[2] } else { '' }
        Subject = if ($parts.Count -gt 3) { $parts[3] } else { '' }
    }
}

function Test-BaselineExists {
    $hash = [string]$script:Config.baselineCommit
    if ([string]::IsNullOrWhiteSpace($hash)) { return $false }
    $r = Invoke-Git @('cat-file','-e',("$hash`^{commit}"))
    return ($r.ExitCode -eq 0)
}

function Get-RecentCommits {
    $r = Invoke-Git @('log','-12','--date=short','--pretty=format:%h  %ad  %s')
    return $r.Output
}

function Get-StatusFingerprint {
    param($Status)
    if (-not $Status.Healthy) { return '' }
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$Status.Raw))
}

function Get-SuspiciousPaths {
    param($Files)
    $patterns = @($script:Config.suspiciousPathPatterns)
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($f in $Files) {
        $path = [string]$f.Path
        foreach ($pattern in $patterns) {
            if ($path -match $pattern) {
                $hits.Add($path)
                break
            }
        }
    }
    return @($hits | Sort-Object -Unique)
}

function Run-ConfiguredTests {
    $commands = @($script:Config.testCommands)
    if ($commands.Count -eq 0) {
        return [pscustomobject]@{ Passed = $false; Configured = $false; Output = 'No test commands are configured.' }
    }

    $allOutput = New-Object System.Text.StringBuilder
    $passed = $true
    foreach ($cmd in $commands) {
        [void]$allOutput.AppendLine("> $cmd")
        try {
            $compound = "cd /d `"$($script:Config.repoPath)`" && $cmd"
            $out = & cmd.exe /d /s /c $compound 2>&1 | Out-String
            $code = $LASTEXITCODE
            [void]$allOutput.AppendLine($out.TrimEnd())
            [void]$allOutput.AppendLine("exit: $code")
            [void]$allOutput.AppendLine('')
            if ($code -ne 0) { $passed = $false; break }
        } catch {
            [void]$allOutput.AppendLine($_.Exception.Message)
            $passed = $false
            break
        }
    }
    $text = $allOutput.ToString().TrimEnd()
    Write-Audit 'tests_run' @{ passed = $passed; output = $text }
    return [pscustomobject]@{ Passed = $passed; Configured = $true; Output = $text }
}

function Create-FullCheckpoint {
    param(
        [string]$Message,
        [bool]$Interactive = $true
    )

    $status = Get-RepoStatus
    if (-not $status.Healthy) {
        return [pscustomobject]@{ Success = $false; Message = $status.Error }
    }
    if ($status.Files.Count -eq 0) {
        return [pscustomobject]@{ Success = $false; Message = 'Working tree is already clean. Nothing to checkpoint.' }
    }

    $suspicious = @(Get-SuspiciousPaths $status.Files)
    if ($suspicious.Count -gt 0) {
        $msg = "Checkpoint blocked because suspicious paths are present:`r`n`r`n" + ($suspicious -join "`r`n") + "`r`n`r`nReview .gitignore and the files before committing."
        Write-Audit 'checkpoint_blocked_suspicious_paths' @{ files = $suspicious }
        return [pscustomobject]@{ Success = $false; Message = $msg }
    }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = 'checkpoint: sample repository ' + (Get-Date).ToString('yyyy-MM-dd HH:mm')
    }

    if ($Interactive) {
        $preview = ($status.Files | Select-Object -First 12 | ForEach-Object { "$($_.Status)  $($_.Path)" }) -join "`r`n"
        if ($status.Files.Count -gt 12) { $preview += "`r`n... and $($status.Files.Count - 12) more" }
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "Create a FULL restore point with all current non-ignored changes?`r`n`r`n$preview`r`n`r`nThis will run git add -A and commit the full current working state.",
            'Create sample repository Restore Point',
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) {
            return [pscustomobject]@{ Success = $false; Message = 'Checkpoint cancelled.' }
        }
        $input = [Microsoft.VisualBasic.Interaction]::InputBox('Commit message:', 'Restore Point', $Message)
        if (-not [string]::IsNullOrWhiteSpace($input)) { $Message = $input }
    }

    $stage = Invoke-Git -Arguments @('add','-A') -Mode 'write'
    if ($stage.ExitCode -ne 0) {
        Write-Audit 'checkpoint_stage_failed' @{ output = $stage.Output }
        return [pscustomobject]@{ Success = $false; Message = "Staging failed:`r`n$($stage.Output)" }
    }

    $commit = Invoke-Git -Arguments @('commit','-m',$Message) -Mode 'write'
    if ($commit.ExitCode -ne 0) {
        Write-Audit 'checkpoint_commit_failed' @{ output = $commit.Output }
        return [pscustomobject]@{ Success = $false; Message = "Commit failed:`r`n$($commit.Output)" }
    }

    $last = Get-LastCommit
    Write-Audit 'checkpoint_created' @{ hash = $last.Hash; shortHash = $last.ShortHash; message = $Message; fileCount = $status.Files.Count }
    return [pscustomobject]@{
        Success = $true
        Message = "Restore point created.`r`n`r`n$($last.ShortHash)  $Message"
        Hash = $last.Hash
        ShortHash = $last.ShortHash
    }
}

function Show-FileDiff {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return 'Select a changed file first.' }

    $r = Invoke-Git @('diff','--',$Path)
    if ($r.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($r.Output)) {
        return $r.Output
    }

    $r2 = Invoke-Git @('diff','--cached','--',$Path)
    if ($r2.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($r2.Output)) {
        return $r2.Output
    }

    $cleanPath = $Path
    if ($cleanPath -match ' -> ') { $cleanPath = ($cleanPath -split ' -> ')[-1] }
    $full = Join-Path ([string]$script:Config.repoPath) $cleanPath
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        try {
            $fi = Get-Item -LiteralPath $full
            if ($fi.Length -gt 150000) { return "Untracked or unchanged file. Preview skipped because file is larger than 150 KB.`r`n$full" }
            return "UNTRACKED / NO DIFF AVAILABLE`r`n$full`r`n`r`n" + (Get-Content -Raw -LiteralPath $full)
        } catch {}
    }
    return 'No diff output available for this file.'
}

Load-Config
Write-Audit 'app_started' @{ repo = [string]$script:Config.repoPath; baseline = [string]$script:Config.baselineCommit }

# --- Pet window --------------------------------------------------------------
$pet = New-Object System.Windows.Forms.Form
$pet.Text = 'ZomniverseGitPet Prototype'
$pet.Width = 230
$pet.Height = 170
$pet.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$pet.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$working = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$pet.Location = New-Object System.Drawing.Point(($working.Right - 250), ($working.Bottom - 190))
$pet.TopMost = $true
$pet.BackColor = [System.Drawing.Color]::FromArgb(35,35,40)
$pet.ShowInTaskbar = $true

$petFace = New-Object System.Windows.Forms.Label
$petFace.Text = '🐾'
$petFace.Font = New-Object System.Drawing.Font('Segoe UI Emoji',38,[System.Drawing.FontStyle]::Regular)
$petFace.AutoSize = $false
$petFace.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$petFace.Width = 80
$petFace.Height = 80
$petFace.Location = New-Object System.Drawing.Point(75,10)
$petFace.ForeColor = [System.Drawing.Color]::White
$pet.Controls.Add($petFace)

$petTitle = New-Object System.Windows.Forms.Label
$petTitle.Text = 'ZomniverseGitPet Prototype'
$petTitle.ForeColor = [System.Drawing.Color]::White
$petTitle.Font = New-Object System.Drawing.Font('Segoe UI',10,[System.Drawing.FontStyle]::Bold)
$petTitle.AutoSize = $false
$petTitle.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$petTitle.Width = 220
$petTitle.Height = 22
$petTitle.Location = New-Object System.Drawing.Point(5,88)
$pet.Controls.Add($petTitle)

$petStatus = New-Object System.Windows.Forms.Label
$petStatus.Text = 'Checking repository...'
$petStatus.ForeColor = [System.Drawing.Color]::Gainsboro
$petStatus.Font = New-Object System.Drawing.Font('Segoe UI',9)
$petStatus.AutoSize = $false
$petStatus.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$petStatus.Width = 220
$petStatus.Height = 45
$petStatus.Location = New-Object System.Drawing.Point(5,112)
$pet.Controls.Add($petStatus)

$script:dragging = $false
$script:dragOffset = New-Object System.Drawing.Point(0,0)
$mouseDownHandler = {
    param($sender,$e)
    if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        $script:dragging = $true
        $script:dragOffset = New-Object System.Drawing.Point($e.X,$e.Y)
    }
}
$mouseMoveHandler = {
    param($sender,$e)
    if ($script:dragging) {
        $screenPos = [System.Windows.Forms.Cursor]::Position
        $pet.Location = New-Object System.Drawing.Point(($screenPos.X - $script:dragOffset.X),($screenPos.Y - $script:dragOffset.Y))
    }
}
$mouseUpHandler = { param($sender,$e); $script:dragging = $false }
foreach ($control in @($pet,$petFace,$petTitle,$petStatus)) {
    $control.Add_MouseDown($mouseDownHandler)
    $control.Add_MouseMove($mouseMoveHandler)
    $control.Add_MouseUp($mouseUpHandler)
}

function Refresh-PetStatus {
    $status = Get-RepoStatus
    if (-not $status.Healthy) {
        $pet.BackColor = [System.Drawing.Color]::FromArgb(90,35,35)
        $petStatus.Text = "Git problem`r`nDouble-click for details"
        return $status
    }

    $last = Get-LastCommit
    if ($status.Files.Count -eq 0) {
        $pet.BackColor = [System.Drawing.Color]::FromArgb(30,80,55)
        if ($last) { $petStatus.Text = "Clean ✓`r`nLast: $($last.ShortHash)" }
        else { $petStatus.Text = 'Clean ✓' }
    } else {
        $pet.BackColor = [System.Drawing.Color]::FromArgb(105,80,25)
        $petStatus.Text = "$($status.Files.Count) changed file(s)`r`nReady to review"
    }
    return $status
}

function Refresh-Dashboard {
    if (-not $script:Dashboard -or $script:Dashboard.IsDisposed) { return }
    $status = Get-RepoStatus
    $branch = $status.Branch
    $last = Get-LastCommit
    $baselineOk = Test-BaselineExists

    $script:LblRepo.Text = if ($status.Healthy) { 'Repository: healthy ✓' } else { 'Repository: problem ✗' }
    $script:LblBranch.Text = "Branch: $branch"
    $script:LblBaseline.Text = if ($baselineOk) { "Baseline: present ✓  $([string]$script:Config.baselineCommit.Substring(0,8))" } else { 'Baseline: NOT FOUND ✗' }
    $script:LblLast.Text = if ($last) { "Last checkpoint: $($last.ShortHash)  $($last.Subject)" } else { 'Last checkpoint: none' }
    $script:LblCount.Text = "Changed files: $($status.Files.Count)"
    if ($script:LblBackend) {
        $readBackend = if ($status.Backend) { $status.Backend } else { Resolve-GitBackend -Mode 'read' }
        $writeBackend = Resolve-GitBackend -Mode 'write'
        $script:LblBackend.Text = "Git engine: read=$readBackend / write=$writeBackend"
    }

    $script:Grid.Rows.Clear()
    foreach ($f in $status.Files) {
        [void]$script:Grid.Rows.Add($f.Status,$f.Path)
    }

    if (-not $status.Healthy) {
        $script:Output.Text = $status.Error
    }
}

function Set-DashboardBusy {
    param([string]$Message = 'Working...')
    if ($script:Dashboard -and -not $script:Dashboard.IsDisposed) {
        $script:Dashboard.Cursor = [System.Windows.Forms.Cursors]::WaitCursor
        if ($script:Output) { $script:Output.Text = $Message }
        [System.Windows.Forms.Application]::DoEvents()
    }
}

function Clear-DashboardBusy {
    if ($script:Dashboard -and -not $script:Dashboard.IsDisposed) {
        $script:Dashboard.Cursor = [System.Windows.Forms.Cursors]::Default
        [System.Windows.Forms.Application]::DoEvents()
    }
}

function Show-Dashboard {
    if ($script:Dashboard -and -not $script:Dashboard.IsDisposed) {
        $script:Dashboard.Show()
        $script:Dashboard.WindowState = [System.Windows.Forms.FormWindowState]::Normal
        $script:Dashboard.Activate()
        Refresh-Dashboard
        return
    }

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'sample repository Git Guardian'
    $form.Width = 900
    $form.Height = 745
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $form.BackColor = [System.Drawing.Color]::FromArgb(245,245,247)
    $script:Dashboard = $form

    $script:LblRepo = New-Object System.Windows.Forms.Label
    $script:LblRepo.Location = New-Object System.Drawing.Point(20,15)
    $script:LblRepo.Size = New-Object System.Drawing.Size(260,24)
    $script:LblRepo.Font = New-Object System.Drawing.Font('Segoe UI',10,[System.Drawing.FontStyle]::Bold)
    $form.Controls.Add($script:LblRepo)

    $script:LblBranch = New-Object System.Windows.Forms.Label
    $script:LblBranch.Location = New-Object System.Drawing.Point(300,15)
    $script:LblBranch.Size = New-Object System.Drawing.Size(160,24)
    $form.Controls.Add($script:LblBranch)

    $script:LblBaseline = New-Object System.Windows.Forms.Label
    $script:LblBaseline.Location = New-Object System.Drawing.Point(470,15)
    $script:LblBaseline.Size = New-Object System.Drawing.Size(380,24)
    $form.Controls.Add($script:LblBaseline)

    $script:LblLast = New-Object System.Windows.Forms.Label
    $script:LblLast.Location = New-Object System.Drawing.Point(20,42)
    $script:LblLast.Size = New-Object System.Drawing.Size(650,24)
    $form.Controls.Add($script:LblLast)

    $script:LblCount = New-Object System.Windows.Forms.Label
    $script:LblCount.Location = New-Object System.Drawing.Point(680,42)
    $script:LblCount.Size = New-Object System.Drawing.Size(180,24)
    $form.Controls.Add($script:LblCount)

    $script:LblBackend = New-Object System.Windows.Forms.Label
    $script:LblBackend.Location = New-Object System.Drawing.Point(20,64)
    $script:LblBackend.Size = New-Object System.Drawing.Size(500,22)
    $script:LblBackend.ForeColor = [System.Drawing.Color]::DimGray
    $form.Controls.Add($script:LblBackend)

    $btnRefresh = New-Object System.Windows.Forms.Button
    $btnRefresh.Text = 'Refresh'
    $btnRefresh.Location = New-Object System.Drawing.Point(20,92)
    $btnRefresh.Size = New-Object System.Drawing.Size(100,32)
    $btnRefresh.Add_Click({ Set-DashboardBusy 'Refreshing repository status...'; Refresh-Dashboard; Clear-DashboardBusy })
    $form.Controls.Add($btnRefresh)

    $btnDiff = New-Object System.Windows.Forms.Button
    $btnDiff.Text = 'View Diff'
    $btnDiff.Location = New-Object System.Drawing.Point(130,92)
    $btnDiff.Size = New-Object System.Drawing.Size(100,32)
    $btnDiff.Add_Click({
        if ($script:Grid.SelectedRows.Count -eq 0) { $script:Output.Text = 'Select a changed file first.'; return }
        $path = [string]$script:Grid.SelectedRows[0].Cells[1].Value
        Set-DashboardBusy "Loading diff for $path ..."
        $script:Output.Text = Show-FileDiff $path
        Clear-DashboardBusy
    })
    $form.Controls.Add($btnDiff)

    $btnTests = New-Object System.Windows.Forms.Button
    $btnTests.Text = 'Run Tests'
    $btnTests.Location = New-Object System.Drawing.Point(240,92)
    $btnTests.Size = New-Object System.Drawing.Size(100,32)
    $btnTests.Add_Click({
        Set-DashboardBusy 'Running configured tests... the window may remain busy until the current test finishes.'
        $r = Run-ConfiguredTests
        Clear-DashboardBusy
        $script:Output.Text = $r.Output
        if ($r.Configured -and $r.Passed) {
            [System.Windows.Forms.MessageBox]::Show('All configured tests passed.','Tests',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
        } elseif (-not $r.Configured) {
            [System.Windows.Forms.MessageBox]::Show("No tests are configured yet. Use Open Config and add testCommands.",'Tests',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
        } else {
            [System.Windows.Forms.MessageBox]::Show('A configured test failed. No automatic checkpoint will be created.','Tests',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        }
    })
    $form.Controls.Add($btnTests)

    $btnCheckpoint = New-Object System.Windows.Forms.Button
    $btnCheckpoint.Text = 'Create Restore Point'
    $btnCheckpoint.Location = New-Object System.Drawing.Point(350,92)
    $btnCheckpoint.Size = New-Object System.Drawing.Size(155,32)
    $btnCheckpoint.Add_Click({
        $result = Create-FullCheckpoint -Message ('checkpoint: sample repository ' + (Get-Date).ToString('yyyy-MM-dd HH:mm')) -Interactive $true
        $script:Output.Text = $result.Message
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show($result.Message,'Restore Point',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
        } elseif ($result.Message -ne 'Checkpoint cancelled.') {
            [System.Windows.Forms.MessageBox]::Show($result.Message,'Restore Point',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        }
        Refresh-Dashboard
        [void](Refresh-PetStatus)
    })
    $form.Controls.Add($btnCheckpoint)

    $btnRecent = New-Object System.Windows.Forms.Button
    $btnRecent.Text = 'Recent Commits'
    $btnRecent.Location = New-Object System.Drawing.Point(515,92)
    $btnRecent.Size = New-Object System.Drawing.Size(120,32)
    $btnRecent.Add_Click({ Set-DashboardBusy 'Loading recent commits...'; $script:Output.Text = Get-RecentCommits; Clear-DashboardBusy })
    $form.Controls.Add($btnRecent)

    $btnHealth = New-Object System.Windows.Forms.Button
    $btnHealth.Text = 'Health Check'
    $btnHealth.Location = New-Object System.Drawing.Point(645,92)
    $btnHealth.Size = New-Object System.Drawing.Size(110,32)
    $btnHealth.Add_Click({
        Set-DashboardBusy 'Running git fsck health check... this can take a little while.'
        $r = Invoke-Git @('fsck','--no-progress')
        Clear-DashboardBusy
        if ($r.ExitCode -eq 0) { $script:Output.Text = "git fsck passed.`r`n`r`n$($r.Output)" }
        else { $script:Output.Text = "git fsck FAILED.`r`n`r`n$($r.Output)" }
        Write-Audit 'health_check' @{ passed = ($r.ExitCode -eq 0); output = $r.Output }
    })
    $form.Controls.Add($btnHealth)

    $btnConfig = New-Object System.Windows.Forms.Button
    $btnConfig.Text = 'Open Config'
    $btnConfig.Location = New-Object System.Drawing.Point(765,92)
    $btnConfig.Size = New-Object System.Drawing.Size(100,32)
    $btnConfig.Add_Click({ Start-Process notepad.exe $script:ConfigPath })
    $form.Controls.Add($btnConfig)

    $script:Grid = New-Object System.Windows.Forms.DataGridView
    $script:Grid.Location = New-Object System.Drawing.Point(20,137)
    $script:Grid.Size = New-Object System.Drawing.Size(845,255)
    $script:Grid.AllowUserToAddRows = $false
    $script:Grid.AllowUserToDeleteRows = $false
    $script:Grid.ReadOnly = $true
    $script:Grid.SelectionMode = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
    $script:Grid.MultiSelect = $false
    $script:Grid.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::Fill
    [void]$script:Grid.Columns.Add('Status','Status')
    [void]$script:Grid.Columns.Add('Path','Path')
    $script:Grid.Columns[0].FillWeight = 15
    $script:Grid.Columns[1].FillWeight = 85
    $script:Grid.Add_CellDoubleClick({
        param($sender,$e)
        if ($e.RowIndex -ge 0) {
            $path = [string]$script:Grid.Rows[$e.RowIndex].Cells[1].Value
            Set-DashboardBusy "Loading diff for $path ..."
            $script:Output.Text = Show-FileDiff $path
            Clear-DashboardBusy
        }
    })
    $form.Controls.Add($script:Grid)

    $autoBox = New-Object System.Windows.Forms.CheckBox
    $autoBox.Text = 'Automatic verified checkpoints after quiet period'
    $autoBox.Location = New-Object System.Drawing.Point(20,402)
    $autoBox.Size = New-Object System.Drawing.Size(360,24)
    $autoBox.Checked = [bool]$script:Config.autoCheckpointEnabled
    $autoBox.Add_CheckedChanged({
        $script:Config.autoCheckpointEnabled = $autoBox.Checked
        Save-Config
        Write-Audit 'auto_checkpoint_setting_changed' @{ enabled = $autoBox.Checked }
    })
    $form.Controls.Add($autoBox)

    $quietLabel = New-Object System.Windows.Forms.Label
    $quietLabel.Text = "Quiet period: $([int]$script:Config.quietMinutes) minute(s)"
    $quietLabel.Location = New-Object System.Drawing.Point(400,404)
    $quietLabel.Size = New-Object System.Drawing.Size(230,24)
    $form.Controls.Add($quietLabel)

    $hint = New-Object System.Windows.Forms.Label
    $hint.Text = 'Auto-checkpoint is blocked unless configured tests pass (default). Configure testCommands before enabling it.'
    $hint.Location = New-Object System.Drawing.Point(20,429)
    $hint.Size = New-Object System.Drawing.Size(830,36)
    $hint.ForeColor = [System.Drawing.Color]::DimGray
    $form.Controls.Add($hint)

    $script:Output = New-Object System.Windows.Forms.RichTextBox
    $script:Output.Location = New-Object System.Drawing.Point(20,472)
    $script:Output.Size = New-Object System.Drawing.Size(845,210)
    $script:Output.ReadOnly = $true
    $script:Output.Font = New-Object System.Drawing.Font('Consolas',9)
    $script:Output.BackColor = [System.Drawing.Color]::White
    $form.Controls.Add($script:Output)

    $form.Add_FormClosing({
        param($sender,$e)
        if ($e.CloseReason -eq [System.Windows.Forms.CloseReason]::UserClosing) {
            $e.Cancel = $true
            $form.Hide()
        }
    })

    Refresh-Dashboard
    $form.Show()
}

# Double-click anywhere on the pet to open the dashboard.
foreach ($control in @($pet,$petFace,$petTitle,$petStatus)) {
    $control.Add_DoubleClick({ Show-Dashboard })
}

# Context menu.
$menu = New-Object System.Windows.Forms.ContextMenuStrip
$itemReview = $menu.Items.Add('Review changes')
$itemReview.Add_Click({ Show-Dashboard })
$itemCheckpoint = $menu.Items.Add('Create full restore point')
$itemCheckpoint.Add_Click({
    $result = Create-FullCheckpoint -Message ('checkpoint: sample repository ' + (Get-Date).ToString('yyyy-MM-dd HH:mm')) -Interactive $true
    if ($result.Success) {
        [System.Windows.Forms.MessageBox]::Show($result.Message,'Restore Point',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
    } elseif ($result.Message -ne 'Checkpoint cancelled.') {
        [System.Windows.Forms.MessageBox]::Show($result.Message,'Restore Point',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
    }
    [void](Refresh-PetStatus)
})
$itemOpenRepo = $menu.Items.Add('Open sample repository folder')
$itemOpenRepo.Add_Click({ Start-Process explorer.exe ([string]$script:Config.repoPath) })
$itemOpenConfig = $menu.Items.Add('Open config')
$itemOpenConfig.Add_Click({ Start-Process notepad.exe $script:ConfigPath })
$itemReload = $menu.Items.Add('Reload config')
$itemReload.Add_Click({ Load-Config; $script:ReadGitBackendResolved = $null
$script:WriteGitBackendResolved = $null; [void](Refresh-PetStatus); Refresh-Dashboard })
[void]$menu.Items.Add('-')
$itemExit = $menu.Items.Add('Exit')
$itemExit.Add_Click({ Write-Audit 'app_exited'; $pet.Close(); [System.Windows.Forms.Application]::Exit() })
$pet.ContextMenuStrip = $menu
$petFace.ContextMenuStrip = $menu
$petTitle.ContextMenuStrip = $menu
$petStatus.ContextMenuStrip = $menu

# Poll repository and optionally create a verified automatic checkpoint.
$timer = New-Object System.Windows.Forms.Timer
$pollSeconds = [Math]::Max(5,[int]$script:Config.pollSeconds)
$timer.Interval = $pollSeconds * 1000
$timer.Add_Tick({
    $status = Refresh-PetStatus

    if (-not $status.Healthy) { return }
    $fp = Get-StatusFingerprint $status
    $statusChanged = ($fp -ne $script:StatusFingerprint)
    if ($statusChanged) {
        $script:StatusFingerprint = $fp
        $script:LastChangeAt = Get-Date
        # Only repopulate the heavier dashboard when the working-tree state actually
        # changed. This prevents needless Git/WSL calls while the user is clicking.
        if ($script:Dashboard -and -not $script:Dashboard.IsDisposed -and $script:Dashboard.Visible) {
            Refresh-Dashboard
        }
    }

    if (-not [bool]$script:Config.autoCheckpointEnabled) { return }
    if ($script:AutoCheckpointBusy) { return }
    if ($status.Files.Count -eq 0) { return }
    if ($fp -eq $script:LastAutoCheckpointFingerprint) { return }

    $quietMinutes = [Math]::Max(1,[int]$script:Config.quietMinutes)
    if (((Get-Date) - $script:LastChangeAt).TotalMinutes -lt $quietMinutes) { return }

    $script:AutoCheckpointBusy = $true
    try {
        $suspicious = @(Get-SuspiciousPaths $status.Files)
        if ($suspicious.Count -gt 0) {
            Write-Audit 'auto_checkpoint_blocked_suspicious_paths' @{ files = $suspicious }
            $petStatus.Text = "Auto checkpoint blocked`r`nReview suspicious path(s)"
            return
        }

        if ([bool]$script:Config.requireTestsForAutoCheckpoint) {
            $tests = Run-ConfiguredTests
            if (-not $tests.Configured) {
                $petStatus.Text = "Auto checkpoint waiting`r`nNo tests configured"
                return
            }
            if (-not $tests.Passed) {
                $petStatus.Text = "Tests failed ✗`r`nAuto checkpoint blocked"
                return
            }
        }

        $result = Create-FullCheckpoint -Message ('auto-checkpoint: sample repository ' + (Get-Date).ToString('yyyy-MM-dd HH:mm')) -Interactive $false
        if ($result.Success) {
            $script:LastAutoCheckpointFingerprint = $fp
            $petStatus.Text = "Auto checkpoint saved ✓`r`n$($result.ShortHash)"
            if ($script:Dashboard -and -not $script:Dashboard.IsDisposed) {
                $script:Output.Text = $result.Message
                Refresh-Dashboard
            }
        } else {
            $petStatus.Text = "Auto checkpoint blocked`r`nOpen dashboard"
            Write-Audit 'auto_checkpoint_failed' @{ message = $result.Message }
        }
    } finally {
        $script:AutoCheckpointBusy = $false
    }
})

$initial = Refresh-PetStatus
$script:StatusFingerprint = Get-StatusFingerprint $initial
$script:LastChangeAt = Get-Date
$timer.Start()

[System.Windows.Forms.Application]::Run($pet)


