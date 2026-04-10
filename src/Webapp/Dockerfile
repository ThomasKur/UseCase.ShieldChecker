# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ShieldChecker.WebApp/ShieldChecker.WebApp.csproj ShieldChecker.WebApp/
RUN dotnet restore ShieldChecker.WebApp/ShieldChecker.WebApp.csproj

COPY . .
RUN dotnet publish ShieldChecker.WebApp/ShieldChecker.WebApp.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShieldChecker.WebApp.dll"]
