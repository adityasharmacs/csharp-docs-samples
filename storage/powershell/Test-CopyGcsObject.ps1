# Copyright(c) 2016 Google Inc.
#
# Licensed under the Apache License, Version 2.0 (the "License"); you may not
# use this file except in compliance with the License. You may obtain a copy of
# the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
# WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
# License for the specific language governing permissions and limitations under
# the License.


# Clear out all existing test data.
$objects = Find-GcsObject -Bucket $env:GOOGLE_BUCKET -Prefix test/
Write-Progress -Activity "Removing old objects" `
    -CurrentOperation "Finding objects" -PercentComplete 0
foreach ($object in $objects) {
    Write-Progress -Activity "Removing old objects" `
        -CurrentOperation "Removing $($object.Name)" `
        -PercentComplete (++$progress * 100 / $objects.Length) `
        -Completed:($progress -eq $objects.Length)
    $ignore = Remove-GcsObject -Bucket $env:GOOGLE_BUCKET -ObjectName $object.Name
}

$failCount = 0

function Expect-Equal($expected, $observed, $invocation) {
    if (!($expected -eq $observed)) {
        if (-not $invocation) {
            $invocation = $MyInvocation
        }
        $failCount += 1
        Write-Warning ("Expectation failed: {0}:{1}`n{2}`nExpected: {3}`nObserved: {4}" -f @(
            (Split-Path $invocation.ScriptName -Leaf), 
            $invocation.ScriptLineNumber,
            $invocation.Line.Trim(),
            $expected,
            $observed))
    }
} 

function Expect-Output([string]$expected) {
    $output = ($input | ForEach-Object { $_ }) -join "`n"
    $tidyExpected = $expected.Trim().Replace("`r", "")
    Expect-Equal $tidyExpected $output $MyInvocation
}    

# Test uploading a new directory.
(.\Copy-GcsObject.ps1 testdata gs://$env:GOOGLE_BUCKET/test/UploadDir `
    -Recurse).Name | Expect-Output "test/UploadDir/
test/UploadDir/hello.txt
test/UploadDir/a/
test/UploadDir/a/b/
test/UploadDir/a/b/c.txt
test/UploadDir/a/empty/"

# Test uploading a single file to a directory.
(.\Copy-GcsObject.ps1 testdata/hello.txt gs://$env:GOOGLE_BUCKET/test/UploadDir/a `
    ).Name | Expect-Output "test/UploadDir/a/hello.txt"

# Test uploading a single file to a file name.
(.\Copy-GcsObject.ps1 testdata/hello.txt gs://$env:GOOGLE_BUCKET/test/UploadDir/a/b/bye.txt `
    ).Name | Expect-Output "test/UploadDir/a/b/bye.txt"

# Test uploading a directory into an existing directory structure.
(.\Copy-GcsObject.ps1 testdata/a/b gs://$env:GOOGLE_BUCKET/test/UploadDir/ `
    -Recurse).Name
