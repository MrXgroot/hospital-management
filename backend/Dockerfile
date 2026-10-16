# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy NuGet configuration, solution, and shared build properties
COPY nuget.config Directory.Build.props HospitalManagement.sln ./

# Copy all project files for dependency caching
COPY src/HMS.Domain/HMS.Domain.csproj src/HMS.Domain/
COPY src/HMS.Application/HMS.Application.csproj src/HMS.Application/
COPY src/HMS.Infrastructure/HMS.Infrastructure.csproj src/HMS.Infrastructure/
COPY src/HMS.Api/HMS.Api.csproj src/HMS.Api/
COPY tests/HMS.UnitTests/HMS.UnitTests.csproj tests/HMS.UnitTests/
COPY tests/HMS.IntegrationTests/HMS.IntegrationTests.csproj tests/HMS.IntegrationTests/

# Restore dependencies
RUN dotnet restore HospitalManagement.sln

# Copy application source code
COPY src/ src/

# Publish HMS.Api in Release mode
RUN dotnet publish src/HMS.Api/HMS.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create entrypoint script to dynamically bind to Render's $PORT or default to 8080
RUN echo '#!/bin/sh' > /app/entrypoint.sh \
    && echo 'if [ -n "$PORT" ]; then' >> /app/entrypoint.sh \
    && echo '  export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"' >> /app/entrypoint.sh \
    && echo 'elif [ -z "$ASPNETCORE_URLS" ]; then' >> /app/entrypoint.sh \
    && echo '  export ASPNETCORE_URLS="http://0.0.0.0:8080"' >> /app/entrypoint.sh \
    && echo 'fi' >> /app/entrypoint.sh \
    && echo 'exec dotnet HMS.Api.dll "$@"' >> /app/entrypoint.sh \
    && chmod +x /app/entrypoint.sh

# Copy published application
COPY --from=build /app/publish .

# Environment defaults
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=8080

EXPOSE 8080

# Run as non-root user (app UID 1654 in .NET images)
USER $APP_UID

ENTRYPOINT ["/app/entrypoint.sh"]
