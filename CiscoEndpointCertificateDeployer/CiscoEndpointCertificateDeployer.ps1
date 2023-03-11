param($result, $endpointHostname, $endpointUsername, $endpointPassword)

$pfxFilename = $result.ManagedItem.CertificatePath

#throw ".\CiscoEndpointCertificateDeployer.exe $pfxFilename $endpointHostname $endpointUsername $endpointPassword"
$process = Start-Process -FilePath ".\CiscoEndpointCertificateDeployer.exe" -ArgumentList $pfxFilename, $endpointHostname, $endpointUsername, $endpointPassword -RedirectStandardOutput c:\ciscocert-out.txt -RedirectStandardError c:\ciscocert-err.txt -Wait -PassThru

#exit $process.Id