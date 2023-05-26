param($result, $endpointHostname, $endpointUsername, $endpointPassword, $applyToServices="https")

$pfxFilename = $result.ManagedItem.CertificatePath

$process = Start-Process -FilePath ".\CiscoEndpointCertificateDeployer.exe" -ArgumentList $pfxFilename, $endpointHostname, $endpointUsername, $endpointPassword, $applyToServices -Wait -PassThru -RedirectStandardOutput c:\ciscocert-out.txt -RedirectStandardError c:\ciscocert-err.txt

exit $process.Id