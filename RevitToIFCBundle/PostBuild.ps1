param ($Configuration, $TargetName, $ProjectDir, $TargetPath, $TargetDir)
write-host $Configuration
write-host $TargetName
write-host $ProjectDir
write-host $TargetPath
write-host $TargetDir

# sign the dll
$cert=Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert

Set-AuthenticodeSignature -FilePath $TargetPath -Certificate $cert -IncludeChain All -TimestampServer "http://timestamp.comodoca.com/authenticode"

# Copy the file to Revit addin directory for debug
$revitVersion = "2022"
$addinFolder = ($env:APPDATA + "\Autodesk\REVIT\Addins\" + $revitVersion)

if (Test-Path $addinFolder) {
    try {
        # Remove previous versions
        if (Test-Path ($addinFolder  + "\" + $TargetName + ".addin")) { Remove-Item ($addinFolder  + "\" + $TargetName + ".addin") }
        if (Test-Path ($addinFolder  + "\" + $TargetName)) { Remove-Item ($addinFolder  + "\" + $TargetName) -Recurse }
            
        # create the RevitToIFCBundle folder
        New-Item -ItemType Directory -Path ($addinFolder  + "\" + $TargetName)

        # Copy the addin file
        xcopy /Y ($ProjectDir + $TargetName + ".addin") ($addinFolder)
        xcopy /Y ($TargetDir + "\*.dll*") ($addinFolder  + "\" + $TargetName)
    }
    catch {
        Write-Host "Something went wrong"
    }
}

# Update the bundle folder
xcopy /Y ($ProjectDir + $TargetName + ".addin") ($ProjectDir + "\RevitToIFCBundle.bundle\Contents")
xcopy /Y ($TargetDir + "\*.dll*") ($ProjectDir + "\RevitToIFCBundle.bundle\Contents\RevitToIFCBundle")

# Zip the folder

$ReleasePath="C:\Users\Simon\Github\Forge\RevitToIFCApp\deploy"
$ReleaseZip = ($ReleasePath + "\" + $TargetName + ".zip")

if ( Test-Path -Path $ReleasePath ) {
  7z a -tzip $ReleaseZip ($ProjectDir + "\RevitToIFCBundle.bundle")
}