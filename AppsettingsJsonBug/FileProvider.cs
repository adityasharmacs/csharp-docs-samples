/*
 * Copyright (c) 2017 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

using System;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

class FileProvider : IFileProvider
{
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        throw new System.NotImplementedException();
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var info = new System.IO.FileInfo(subpath);

        return new FileInfo() {
            Exists = info.Exists,
            Length = info.Length,
            Name = info.Name,
            LastModified = info.LastWriteTime,
            IsDirectory = false,
            FullPath = info.FullName,
            };
        }

    public IChangeToken Watch(string filter)
    {
        return null;
    }
}

class FileInfo : IFileInfo
{
    public bool Exists { get; set;}

    public long Length { get; set; }

    public string PhysicalPath => null;

    public string Name { get; set; }

    public DateTimeOffset LastModified { get; set; }

    public bool IsDirectory { get; set; }

    public string FullPath { get; set; }

    public Stream CreateReadStream()
    {
        // Returning a file stream causes no exception.  Why does
        // copying the stream to a memory stream cause the json parser to choke?
        // return File.OpenRead(FullPath);
        MemoryStream memStream = new MemoryStream();        
        using (var fileStream = File.OpenRead(FullPath)) {
            fileStream.CopyTo(memStream);
        }
        return memStream;
    }
}