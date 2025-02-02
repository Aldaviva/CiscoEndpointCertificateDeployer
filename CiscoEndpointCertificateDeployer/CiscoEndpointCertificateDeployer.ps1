param($result, $endpointHostname, $endpointUsername, $endpointPassword, $applyToServices="https")

$pfxFilename = $result.ManagedItem.CertificatePath

$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = "CiscoEndpointCertificateDeployer.exe"
$process.StartInfo.Arguments = $pfxFilename, $endpointHostname, $endpointUsername, $endpointPassword, $applyToServices
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.CreateNoWindow = $true
$null = $process.Start()
 
$stdout = $process.StandardOutput.ReadToEnd()
$process | Wait-Process

if ($process.ExitCode -eq 0) {
    Write-Output $stdout
} else {
    Write-Error "$($process.StartInfo.FileName) exited with code $($process.ExitCode): $stdout"
}
