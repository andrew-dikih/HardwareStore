FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies (layer-cached until project files change)
COPY src/HardwareStore.Core/HardwareStore.Core.csproj src/HardwareStore.Core/
COPY src/HardwareStore.Infrastructure/HardwareStore.Infrastructure.csproj src/HardwareStore.Infrastructure/
COPY src/HardwareStore.Api/HardwareStore.Api.csproj src/HardwareStore.Api/
RUN dotnet restore src/HardwareStore.Api/HardwareStore.Api.csproj

# Build and publish
COPY src/ src/
RUN dotnet publish src/HardwareStore.Api/HardwareStore.Api.csproj -c Release -o /app/publish

# Use the official ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "HardwareStore.Api.dll"]
