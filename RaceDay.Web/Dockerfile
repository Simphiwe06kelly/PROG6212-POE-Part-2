# syntax=docker/dockerfile:1

# =====================================================================
# RaceDay web app (ASP.NET Core MVC, .NET 10)
# Multi-stage build: the big SDK image builds the app, and only the
# small ASP.NET runtime image plus the published files are shipped.
# =====================================================================

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first, so the "restore" layer is cached
# and only re-runs when the project's packages change.
COPY RaceDay.Web.csproj ./
RUN dotnet restore RaceDay.Web.csproj

# Now copy the rest of the source and publish a Release build.
COPY . ./
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# The app listens on port 8080 inside the container.
# ApiSettings__BaseUrl is where the RaceDay API lives. The default points at an API
# running on the host machine; override it with: -e ApiSettings__BaseUrl=...
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ApiSettings__BaseUrl=http://host.docker.internal:5286/

EXPOSE 8080

# Do not run as root: the .NET images include a low-privilege user called "app".
USER app

ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
