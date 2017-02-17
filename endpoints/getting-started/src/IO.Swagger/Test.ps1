#
# Test.ps1
#
param([string]$EchoUrl)

$result = Invoke-WebRequest "$EchoUrl/echo" -Body "{'message': 'in a bottle'}" -Method POST -ContentType "application/json"
if ($result.StatusCode -ne 200) {
	throw ("Bad status code" + $result.StatusCode)
}
$responseObject = ConvertFrom-Json $result.Content
if ($responseObject.message -ne "in a bottle") {
	throw ("Bad response:" + $result.Content)
}
"2 Tests passed."
