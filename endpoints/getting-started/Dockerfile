FROM microsoft/dotnet:1.0-runtime
WORKDIR /dotnetapp
COPY src/IO.Swagger/bin/Release/netcoreapp1.0/publish .
ENTRYPOINT ["dotnet", "IO.Swagger.dll"]
