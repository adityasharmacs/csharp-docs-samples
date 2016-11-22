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

# List all my Google Cloud Resources.

function Get-GcResource {
    # Google Cloud Storage
    Try {
        $buckets = Get-GcsBucket
        $buckets
        foreach ($bucket in $buckets) {
          Find-GcsObject -Bucket $bucket
        }
    }
    Catch {
        Report-Exception $_.Exception
    }

    # Google Compute Engine
    Try {
        Get-GceAddress
        Get-GceBackendService
        Get-GceDisk
        Get-GceFirewall
        Get-GceForwardingRule
        Get-GceHealthCheck
        Get-GceInstance
        Get-GceInstanceTemplate
        Get-GceManagedInstanceGroup
        Get-GceNetwork
        Get-GceRoute
        Get-GceSnapshot
        Get-GceTargetPool
        Get-GceTargetProxy
        Get-GceUrlMap
    }
    Catch {
        Report-Exception $_.Exception
    }

    # Google Cloud Sql
    Try {
        Get-GcSqlInstance
    }
    Catch {
        Report-Exception $_.Exception
    }

    # Google Cloud Dns
    Try {
        $zones = Get-GcdManagedZone
        foreach ($zone in $zones) {
            Get-GcdResourceRecordSet $zone
        }
    }
    Catch {
        Report-Exception $_.Exception
    }

}

function Report-Exception($Exception) {
    if (-not $Exception.Message.Contains("accessNotConfigured")) {
        Write-Warning $Exception.Message
    }
}

Get-GcResource | Format-Table -GroupBy Kind -Property Size, SizeGb, DiskSizeGb, Name