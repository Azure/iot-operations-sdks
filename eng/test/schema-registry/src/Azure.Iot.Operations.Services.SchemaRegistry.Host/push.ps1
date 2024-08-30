$v=(nbgv get-version -v NuGetPackageVersion)
$registry="edgebuilds.azurecr.io"

dotnet publish -c Release /t:PublishContainer  /p:ContainerImageTags="""$v;latest""" /p:ContainerRegistry="$registry"
