$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "backend-summary-$([guid]::NewGuid())"
$null = New-Item -ItemType Directory -Path $testDirectory
$summary = Join-Path $testDirectory 'summary.md'
$fixture = Join-Path $testDirectory 'tests.trx'
$secondFixture = Join-Path $testDirectory 'integration.trx'
try {
    foreach ($case in @(
        @{ Outcome = 'Completed'; Passed = 3; Failed = 0; Skipped = 0; Files = 1; Other = 0 },
        @{ Outcome = 'Failed'; Passed = 1; Failed = 1; Skipped = 1; Files = 0; Other = 0 },
        @{ Outcome = 'Aborted'; Passed = 1; Failed = 0; Skipped = 0; Files = 0; Other = 2 }
    )) {
        @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <TestDefinitions><UnitTest storage="/tests/Example.Tests.dll" /></TestDefinitions>
  <ResultSummary outcome="$($case.Outcome)"><Counters total="3" passed="$($case.Passed)" failed="$($case.Failed)" notExecuted="$($case.Skipped)" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $fixture
        & "$PSScriptRoot/Write-TestSummary.ps1" -ResultsDirectory $testDirectory -SummaryPath $summary
        $actual = Get-Content -LiteralPath $summary -Raw
        foreach ($expected in @("**$($case.Files) passed** · 1 total", "**$($case.Passed) passed** · ❌ **$($case.Failed) failed** · ⏭️ **$($case.Skipped) skipped** · **3 total**", 'Example.Tests.dll')) {
            if (-not $actual.Contains($expected)) { throw "Missing summary text: $expected" }
        }
        if ($case.Other -gt 0 -and -not $actual.Contains('**Other outcomes**: ⚠️ **2**')) { throw 'Missing other outcomes.' }
        Remove-Item -LiteralPath $summary
        if ($case.Files -eq 1) {
            Copy-Item -LiteralPath $fixture -Destination $secondFixture
            & "$PSScriptRoot/Write-TestSummary.ps1" -ResultsDirectory $testDirectory -SummaryPath $summary
            $actual = Get-Content -LiteralPath $summary -Raw
            if (-not $actual.Contains('**2 passed** · 2 total') -or -not $actual.Contains('**6 passed**') -or -not $actual.Contains('**6 total**')) { throw 'Incorrect combined totals.' }
            Remove-Item -LiteralPath $summary, $secondFixture
        }
    }
    Remove-Item -LiteralPath $fixture
    & "$PSScriptRoot/Write-TestSummary.ps1" -ResultsDirectory $testDirectory -SummaryPath $summary
    if (-not (Get-Content -LiteralPath $summary -Raw).Contains('No TRX reports were produced.')) { throw 'Missing no-report message.' }
    Write-Host 'Test summary checks passed.'
} finally {
    foreach ($file in @($summary, $fixture, $secondFixture)) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $testDirectory
}
