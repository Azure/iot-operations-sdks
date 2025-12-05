az acr login -n akripreview.azurecr.io
dotnet publish --os linux --arch x64 /t:PublishContainer -p ContainerRegistry=akripreview.azurecr.io