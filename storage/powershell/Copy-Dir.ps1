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


function Split-GcsPath([string] $path) {
    $path = $path.replace('\', '/')  # It's easy to use the wrong slashes.
    if ($path -match '^gs://([^/]+)(/.*)') {
        $matches[1], $matches[2]
    }
}

function Append-Slash([string] $path, [string]$slash = '\') {
    if ($path.EndsWith($slash)) { $path } else { "$path$slash" }
}

function Upload-Dir([string] $sourcePath, [string] $destPath,
        [string] $bucket) {
    $sourceDir = Append-Slash($sourcePath)
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
    $sourceDir = Append-Slash $sourcePath '/'
    foreach ($object in (Find-GcsObject -Bucket $bucket -Prefix $sourceDir)) {
        $relPath = $object.Name.Substring(
            $sourceDir.Length, $object.Name.Length - $sourceDir.Length)
        $destFilePath = (Join-Path $destPath $relPath)
        $destDirPath = (Split-Path -Path $destFilePath)
        $destDir = New-Item -ItemType Directory -Force -Path $destDirPath
        Read-GcsObject -Bucket $bucket -ObjectName $object.Name `
            -OutFile $destFilePath -Force:$force
        Get-Item $destFilePath
    }
}

function Main {
    if (-not ($sourcePath -and $destPath)) {
        Write-Error "Usage:

Copy-Dir.ps1 [-sourcePath] <String> [-destPath] <String> [-Force]

Google Cloud Storage paths look like:
gs://bucket/a/b/c.txt

Note that the concept of a directory does not exist in Cloud Storage, so in
the example above, the object name is a/b/c.txt.
"
        return
    }
    $destBucketAndPath = Split-GcsPath $destPath
    $sourceBucketAndPath = Split-GcsPath $sourcePath
    if ($sourceBucketAndPath) {
        if ($destBucketAndPath) {
            "Not yet implemented."
        } else {
            Download-Dir $sourceBucketAndPath[1] $destPath $sourceBucketAndPath[0]
        }
    } else {
        if ($destBucketAndPath) {
            Upload-Dir $sourcePath $destBucketAndPath[1] $destBucketAndPath[0]
        }
    }        
}

Main