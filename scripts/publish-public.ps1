<#
.SYNOPSIS
  Publishes a clean snapshot of this (private) repo to the public repo.

.DESCRIPTION
  The public repo has its own history, separate from this one. `public-release` is the local branch
  that tracks it: each run adds ONE commit on top, holding the tree of -Source minus the paths in
  $Exclude, so private history, deploy plumbing and scratch files never reach the public repo.

  It works in a temporary git worktree, so your working directory, current branch and uncommitted
  changes are not touched. Before committing it scans the result for the server IP, personal paths
  and secret-looking strings, and aborts if it finds any.

.EXAMPLE
  scripts/publish-public.ps1 -DryRun            # build and verify the commit, push nothing
  scripts/publish-public.ps1                    # publish master
  scripts/publish-public.ps1 -Source dev -Message "Search eval results"
  scripts/publish-public.ps1 -Reset             # rewrite the public repo as ONE fresh commit (force push)
#>
[CmdletBinding()]
param(
    [string]$Source = 'master',
    [string]$Message = '',
    [string]$Remote = 'public',
    [string]$Branch = 'public-release',
    [string]$Target = 'main',
    [switch]$DryRun,
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Paths of the private repo that must never be published (git pathspecs, relative to the repo root).
$Exclude = @(
    'scratch_isnad_test.mjs', 'test.cs', 'hadith_text.txt', 'challange.pdf', 'responses-evaluation',
    'TestResolve',
    'Smart_Hadith_Tree_Presentation.pptx', 'Smart_Hadith_Tree_Presentation.pdf',
    'frontend/playwright-report', 'frontend/test-results',
    '.github/workflows/deploy.yml', 'deploy/deploy-remote.sh', 'deploy/ssh-deploy-gate.sh',
    'scripts/fetch_shamela.py', 'scripts/convert_pptx_to_pdf.ps1', 'scripts/fix.ps1'
)

# Content that aborts the run if it appears anywhere in the snapshot.
$Forbidden = @(
    '169\.58\.190\.201',
    '[A-Za-z]:\\Users\\[A-Za-z]', '[A-Za-z]:\\Programming',
    'AIza[0-9A-Za-z_\-]{30,}', 'sk-[A-Za-z0-9]{20,}', 'ghp_[A-Za-z0-9]{30,}', 'AKIA[0-9A-Z]{16}',
    '-----BEGIN [A-Z ]*PRIVATE KEY'
)

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit $LASTEXITCODE)" }
}

$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root

if (-not (& git remote | Where-Object { $_ -eq $Remote })) {
    throw "Remote '$Remote' does not exist. Add it: git remote add $Remote <public repo url>"
}
& git rev-parse --verify --quiet "refs/heads/$Branch" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Local branch '$Branch' does not exist (it holds the public history)." }
& git rev-parse --verify --quiet "$Source^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Source '$Source' is not a branch or commit." }

$sourceSha = (& git rev-parse --short $Source).Trim()
if (-not $Message) { $Message = if ($Reset) { 'Initial public release' } else { "Update from $Source ($sourceSha)" } }

$wt = Join-Path ([IO.Path]::GetTempPath()) ("sht-public-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
Invoke-Git worktree add --quiet $wt $Branch

try {
    Push-Location $wt

    # Make index and files equal to the Source tree (deletes what Source no longer has).
    Invoke-Git read-tree -u --reset $Source

    # Public-only files live on the public branch, not on Source: bring them back.
    Invoke-Git checkout $Branch -- LICENSE

    # Drop the private paths from index and disk.
    foreach ($p in $Exclude) {
        & git rm -rq --cached --ignore-unmatch -- $p 2>$null
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
    }

    # Docs carry absolute Windows paths of the author's machine; make them relative to the repo root.
    foreach ($f in (& git ls-files '*.md')) {
        $path = Join-Path $wt $f
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $t = [IO.File]::ReadAllText($path)
        $n = [regex]::Replace($t, '[A-Za-z]:\\[^\r\n"'']*?\\Smart-Hadith-Tree\\', '')
        if ($n -ne $t) { [IO.File]::WriteAllText($path, $n) }
    }

    Invoke-Git add -A

    # Safety scan of what is about to be published.
    $bad = @()
    foreach ($pat in $Forbidden) {
        $hits = & git grep --cached -nIE $pat 2>$null
        if ($hits) { $bad += $hits | ForEach-Object { "[$pat] $_" } }
    }
    if ($bad.Count) {
        Write-Host "ABORTED: forbidden content in the snapshot:" -ForegroundColor Red
        $bad | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
        throw "Fix the source (or add the path to `$Exclude) and run again."
    }

    & git diff --cached --quiet
    if ($LASTEXITCODE -eq 0 -and -not $Reset) {
        Write-Host "Nothing to publish: '$Source' ($sourceSha) matches the public branch." -ForegroundColor Yellow
        return
    }

    Write-Host "Changes to publish:" -ForegroundColor Cyan
    Invoke-Git diff --cached --stat --stat-width=100

    if ($Reset) {
        # Start the public history over: one parentless commit replaces everything.
        $tree = (& git write-tree).Trim()
        $commit = (& git commit-tree $tree -m $Message).Trim()
        Invoke-Git update-ref "refs/heads/$Branch" $commit
    }
    else {
        Invoke-Git commit --quiet -m $Message
    }
    Write-Host "Committed on '$Branch': $(& git log --oneline -1)" -ForegroundColor Green
}
finally {
    Pop-Location
    & git worktree remove --force $wt 2>$null
    & git worktree prune
}

if ($DryRun) {
    Write-Host "Dry run: not pushed. Push with: git push $Remote ${Branch}:$Target" -ForegroundColor Yellow
    return
}

if ($Reset) {
    # Overwrites the public history; refuses if someone pushed there since the fetch.
    Invoke-Git fetch --quiet $Remote
    $remoteSha = (& git rev-parse "$Remote/$Target").Trim()
    Invoke-Git push "--force-with-lease=${Target}:$remoteSha" $Remote "${Branch}:$Target"
}
else {
    Invoke-Git push $Remote "${Branch}:$Target"
}
Write-Host "Published to $Remote/$Target." -ForegroundColor Green
