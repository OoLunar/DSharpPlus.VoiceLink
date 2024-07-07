FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ./ /src
RUN dotnet pack -c Release \
    && cd examples/HelloWorld \
    && dotnet add package "OoLunar.DSharpPlus.VoiceLink.Natives.Sodium" --source "/src/src/DSharpPlus.VoiceLink.Natives.Sodium/bin/Release/" \
    && dotnet add package "OoLunar.DSharpPlus.VoiceLink.Natives.Opus" --source "/src/src/DSharpPlus.VoiceLink.Natives.Opus/bin/Release/" \
    && dotnet publish -c Release
ENTRYPOINT dotnet /src/examples/HelloWorld/bin/Release/net8.0/publish/HelloWorld.dll