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

param([string]$sourcePath, [string] $destPath, [switch] $force)


function Get-ItemToCopy([string] $path) {
    if (Test-Path -Path $Path) {
        $path
        if (Test-Path -Path $sourcePath -PathType Container) {
            Get-ChildItem -Recurse $path
        }
    } else {
        throw [System.IO.FileNotFoundException] "$sourcePath not found."
    }    
}

function Split-GcsPath([string] $path) {
    if ($path -match '^gs://([^/]+)(/.*)') {
        $matches[1], $matches[2]
    }
}

function Upload-Dir([string] $sourcePath, [string] $destPath,
        [string] $bucket) {
    # Should yield same result
    # Copy-Dir c:\Users\Jeff\Downloads gs://JeffsBucket/home/
    # Copy-Dir c:\Users\Jeff\Downloads\ gs://JeffsBucket/home/Downloads
    if (-not $bucket) {
        $bucket, $destPath = Split-GcsPath $destPath
    }
    $sourceDir = if ($sourcePath.EndsWith('\')) {
        $sourcePath
    } else {
        "$sourcePath\"
    }

    # assume $destPath in correct form gs://bucket/a/b/
    $destDir = if ($destPath.EndsWith('/')) {
        "$destPath$(Split-Path -Leaf $sourcePath)/"
    } else {
        "$destPath/"
    }
    if (-not (Test-Path -Path $sourcePath -PathType Container)) {
        throw [System.IO.DirectoryNotFoundException] `
            "$sourcePath does not exist or is not a directory."
    }
    $items = Get-ChildItem $sourceDir | Sort-Object -Property Mode,Name
    foreach ($item in $items) {
        if (Test-Path -Path $item.FullName -PathType Container) {
            Upload-Dir "$sourceDir$($item.Name)" "$destDir$($item.Name)" `
                $bucket
        } else {
            New-GcsObject -Bucket $bucket -ObjectName "$destDir$($item.Name)" `
                -File $item.FullName -Force:$force
        }
    }
}

function Download-Dir([string] $sourcePath, [string] $destPath, 
        [string] $bucket) {
    # Should yield same result
    # Copy-Dir gs://JeffsBucket/home/ c:\Users\Jeff\Downloads
    if (-not $bucket) {
        $bucket, $sourcePath = Split-GcsPath $sourcePath
    }
    $sourceDir = if ($sourcePath.EndsWith('/')) { 
        $sourcePath 
    } else {
        "$sourcePath/"
    }
    foreach ($object in (Find-GcsObject -Bucket $bucket -Prefix $sourceDir)) {
        $relPath = $object.Name.Substring(
            $sourceDir.Length, $object.Name.Length - $sourceDir.Length)
        $destFilePath = (Join-Path $destPath $relPath)
        $destDirPath = (Split-Path -Path $destFilePath)
        $destDir = New-Item -ItemType Directory -Force -Path $destDirPath
        Read-GcsObject -Bucket $bucket -ObjectName $object.Name -OutFile $destFilePath
        Get-Item $destFilePath
    }
}


Download-Dir gs://asqnet/projects/wc 'C:\Users\Jeffrey Rennie\Documents\Visual Studio 2015\Projects\wc2'

