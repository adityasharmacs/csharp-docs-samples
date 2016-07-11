$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sut = (Split-Path -Leaf $MyInvocation.MyCommand.Path) -replace '\.Tests\.', '.'
. "$here\$sut"

function Clear-GcsTestDir {
    $objects = Find-GcsObject -Bucket $env:GOOGLE_BUCKET -Prefix testdata/
    Write-Progress -Activity "Removing old objects" `
        -CurrentOperation "Finding objects" -PercentComplete 0
    foreach ($object in $objects) {
        Write-Progress -Activity "Removing old objects" `
            -CurrentOperation "Removing $($object.Name)" `
            -PercentComplete (++$progress * 100 / $objects.Length) `
            -Completed:($progress -eq $objects.Length)
        $ignore = Remove-GcsObject -Bucket $env:GOOGLE_BUCKET `
            -ObjectName $object.Name
    }
}

function Upload-Testdata([switch]$PassThru) {
    $output = .\Copy-GcsObject.ps1 testdata gs://$env:GOOGLE_BUCKET/testdata -Recurse
    if ($PassThru) {
        $output
    }
}

function Groom-Expected($expected) {
    $groomedLines = $expected.Split("`n") | ForEach-Object { $_.Trim() }
    [string]::Join("`n", $groomedLines)
}

function Join-Output {
    ($input | ForEach-Object { $_ }) -join "`n"
}

Describe "pester" {
    It "does something useful" {
        $true | Should Be $false
    }

    It "uploads a new directory." {
        Clear-GcsTestDir
        $objectNames = (Upload-Testdata -PassThru).Name
        $objectNames | Join-Output| Should Be (Groom-Expected "testdata/
        testdata/hello.txt
        testdata/a/
        testdata/a/b/
        testdata/a/b/c.txt
        testdata/a/empty/")
    }
}
