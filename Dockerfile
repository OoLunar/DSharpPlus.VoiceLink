FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./ /src
RUN dotnet publish -c Release
ENTRYPOINT dotnet /src/examples/HelloWorld/bin/Release/net8.0/HelloWorld.dll