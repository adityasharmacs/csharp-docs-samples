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
Import-Module ..\BuildTools.psm1 -DisableNameChecking

BackupAndEdit-TextFile "SessionState\appsettings.json" `
    @{"YOUR-PROJECT-ID" = $env:GOOGLE_PROJECT_ID} `
{    
    dotnet restore   
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed."
    }
    $env:ASPNETCORE_URLS = 'http://localhost:61123'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $webServer = Start-Job -ArgumentList (resolve-path .\SessionState) {
        Set-Location $args[0]
        dotnet run --no-build -c Release
    }
    Start-Sleep -Seconds 10
    $webClient = Start-Job -ArgumentList (resolve-path .\WebClient) {
        Set-Location $args[0]
        dotnet run --no-build -c Release -- $env:ASPNETCORE_URLS
    }
    $webClient | Wait-Job | Out-Null
    $webServer | Stop-Job -PassThru | Remove-Job
    $webClient | Receive-Job
    $webClient | Remove-Job
    if ($LASTEXITCODE -ne 0) {
        throw "WebClient failed!"
    }
}
