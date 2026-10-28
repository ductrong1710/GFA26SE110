param(
    [string]$ResultsDirectory = 'TestResults',
    [string]$SummaryPath = $env:GITHUB_STEP_SUMMARY
)

$ErrorActionPreference = 'Stop'
if (-not $SummaryPath) { throw 'A summary output path is required.' }
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -Recurse -File -ErrorAction SilentlyContinue)
$lines = @('## Backend Test Report', '', '### Summary', '')
if ($reports.Count -eq 0) {
    $lines += 'No TRX reports were produced. Check the build and test steps.'
}
foreach ($file in $reports) {
    [xml]$report = Get-Content -LiteralPath $file.FullName -Raw
    $counters = $report.TestRun.ResultSummary.Counters
    if (-not $counters) { throw "Missing test counters in $($file.Name)." }
    $test = @($report.TestRun.TestDefinitions.UnitTest)[0]
    $name = if ($test.storage) { ($test.storage -replace '\\', '/').Split('/')[-1] } else { $file.Name }
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $skipped = [int]$counters.notExecuted
    $other = $total - $passed - $failed - $skipped
    $lines += @("#### $name", '', "- **Test Results**: ✅ **$passed passed** · ❌ **$failed failed** · ⏭️ **$skipped skipped** · **$total total**")
    if ($other -gt 0) { $lines += "- **Other outcomes**: ⚠️ **$other** (see TRX artifact for details)" }
    $lines += @("- **Run outcome**: $($report.TestRun.ResultSummary.outcome)", '')
}
$lines | Out-File -LiteralPath $SummaryPath -Encoding utf8 -Append
