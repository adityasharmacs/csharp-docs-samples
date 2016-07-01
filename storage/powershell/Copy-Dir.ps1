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

param([string]$sourcePath, [string] $destPath, [switch] $force, [switch] $recurse)


function Split-GcsPath([string] $path) {
    $path = $path.replace('\', '/')  # It's easy to use the wrong slashes.
    if ($path -match '^gs://([^/]+)(/.*)') {
        $matches[1], $matches[2]
    }
}

function Test-GcsObject([string] $Bucket, [string] $ObjectName) {
    try { 
        Get-GcsObject -Bucket $Bucket -ObjectName $ObjectName
        return $True
    } catch {
        if ($_.Exception.HttpStatusCode -eq "NotFound") {
            return $False
        }
        throw
    }
}

function Append-Slash([string] $path, [string]$slash = '\') {
    if ($path.EndsWith($slash)) { $path } else { "$path$slash" }
}

function Upload-Item([string] $sourcePath, [string] $destPath,
        [string] $bucket) {
    # Is the source path a file or a directory?  Does the
    # destination directory already exist?  It takes a lot of logic
    # to match the behavior of cp and copy.
    $destDir = Append-Slash $destPath '/'
    if (Test-Path -Path $sourcePath -PathType Leaf) {
        # It's a file.
        if ((Test-GcsObject $bucket $destDir) -or $destPath.EndsWith('/')) {
            # Copying a single file to a directory.
            New-GcsObject -Bucket $bucket `
                -ObjectName "$destDir$(Split-Path $sourcePath -Leaf)" `
                -File $sourcePath -Force:$force
        } else {
            # Copying a single file to a file name.
            New-GcsObject -Bucket $bucket -ObjectName $destPath `
                -File $sourcePath -Force:$force
        }
    } elseif (Test-Path -Path $sourcePath -PathType Container) {
        # It's a directory.
        if (-not $recurse) {
            throw [System.IO.FileNotFoundException] `
                "Use the -Recurse flag to copy directories."
        }
        if ((Test-GcsObject $bucket $destDir) -or $destPath.EndsWith('/')) {
            # Copying a directory to an existing directory.
            $destDir = "$destDir$($item.Name)"
        }
        New-GcsObject -Bucket $bucket -ObjectName $destDir -Contents "" `
            -Force:$force
        Upload-Dir $sourcePath $destDir $bucket
    } else {
        throw [System.IO.FileNotFoundException] `
        "$sourcePath does not exist."
    }
}

function Upload-Dir([string] $sourcePath, [string] $destDir,
        [string] $bucket) {
    $sourceDir = Append-Slash $sourcePath '\'
    $items = Get-ChildItem $sourceDir | Sort-Object -Property Mode,Name
    foreach ($item in $items) {
        if (Test-Path -Path $item.FullName -PathType Container) {
            New-GcsObject -Bucket $bucket -ObjectName "$destDir$($item.Name)/" `
                -Contents "" -Force:$force
            Upload-Dir "$sourceDir$($item.Name)" "$destDir$($item.Name)/" `
                $bucket
        } else {
            New-GcsObject -Bucket $bucket -ObjectName "$destDir$($item.Name)" `
                -File $item.FullName -Force:$force
        }
    }
}

function Download-Object([string] $sourcePath, [string] $destPath,
        [string] $bucket) {
    $outFile = if (Test-Path -Path $destPath -PathType Container) {
        Join-Path $destPath (Split-Path $sourcePath -Leaf)
    } else {
        $destPath
    }
    if (-not $sourcePath.EndsWith('/') `
        -and (Test-GcsObject $bucket $sourcePath)) {
        # Source path is a simple file.
        Read-GcsObject -Bucket $bucket -ObjectName $sourcePath `
            -OutFile $outFile -Force:$force
    } else {
        # Source is a directory.
        if (-not $recurse) {
            throw [System.IO.FileNotFoundException] `
                "Use the -Recurse flag to copy directories."
        }
        Download-Dir $sourcePath $outFile $bucket
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
        if ($relPath.EndsWith('/')) {
            # It's a directory
            New-Item -ItemType Directory -Force -Path $destFilePath
        } else {
            # It's a file
            $destDir = New-Item -ItemType Directory -Force -Path $destDirPath
            Read-GcsObject -Bucket $bucket -ObjectName $object.Name `
                -OutFile $destFilePath -Force:$force
            Get-Item $destFilePath
        }
    }
}

function Main {
    if (-not ($sourcePath -and $destPath)) {
        Write-Error "Usage:

Copy-Dir.ps1 [-sourcePath] <String> [-destPath] <String> [-Force]

Google Cloud Storage paths look like:
gs://bucket/a/b/c.txt

Note that the concept of a directory does not exist in Cloud Storage, so
empty directories will not be copied to Cloud Storage.
"
        return
    }
    $destBucketAndPath = Split-GcsPath $destPath
    $sourceBucketAndPath = Split-GcsPath $sourcePath
    if ($sourceBucketAndPath) {
        if ($destBucketAndPath) {
            "Not yet implemented."
        } else {
            Download-Object $sourceBucketAndPath[1] $destPath $sourceBucketAndPath[0]
        }
    } else {
        if ($destBucketAndPath) {
            Upload-Item $sourcePath $destBucketAndPath[1] $destBucketAndPath[0]
        } else {
            # Both paths are local.  Let the local file system do it.
            Copy-Item -Path $sourcePath -Destination $destPath -Force:$force -Recurse:$recurse
        }
    }        
}

Main


