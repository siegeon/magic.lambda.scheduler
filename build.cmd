
set version=%1
set key=%2

cd %~dp0
dotnet build magic.lambda.scheduler/magic.lambda.scheduler.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet nuget push magic.lambda.scheduler/bin/Release/magic.lambda.scheduler.%version%.nupkg -k %key% -s https://api.nuget.org/v3/index.json
