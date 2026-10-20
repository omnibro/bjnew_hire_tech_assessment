# syntax=docker/dockerfile:1

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached
# until dependencies actually change.
COPY src/BuildInfo.Api/barak_tech_assesment.csproj src/BuildInfo.Api/
RUN dotnet restore src/BuildInfo.Api/barak_tech_assesment.csproj

COPY src/ src/
RUN dotnet publish src/BuildInfo.Api/barak_tech_assesment.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Build metadata, supplied by CI with --build-arg. Declared late so changing
# them doesn't invalidate the earlier (expensive) layers.
ARG GIT_SHA=unknown
ARG BUILD_NUMBER=local
ARG BUILD_TIME=unknown
ENV GIT_SHA=${GIT_SHA} \
    BUILD_NUMBER=${BUILD_NUMBER} \
    BUILD_TIME=${BUILD_TIME} \
    ASPNETCORE_HTTP_PORTS=8080

LABEL org.opencontainers.image.revision=${GIT_SHA} \
      org.opencontainers.image.created=${BUILD_TIME}

COPY --from=build /app/publish .

# Built-in non-root user in the official .NET 8 images. Port 8080 needs no privileges.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "barak_tech_assesment.dll"]