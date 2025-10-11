# ------------------------------------------------------------------
# Build Stage: Creates the compiled application artifact.
# This stage is optimized for Docker layer caching and security.
# ------------------------------------------------------------------
# Declare a build-time argument for the target platform.
ARG BUILDPLATFORM
FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

WORKDIR /src

# 1. Copy only the files that define dependencies to leverage layer caching.
# This layer is only invalidated if you add/remove a project or a NuGet package.
COPY ["MeetingSystem.sln", "./"]
COPY ["MeetingSystem.Api/MeetingSystem.Api.csproj", "MeetingSystem.Api/"]
COPY ["MeetingSystem.Business/MeetingSystem.Business.csproj", "MeetingSystem.Business/"]
COPY ["MeetingSystem.Context/MeetingSystem.Context.csproj", "MeetingSystem.Context/"]
COPY ["MeetingSystem.Model/MeetingSystem.Model.csproj", "MeetingSystem.Model/"]

# 2. Restore dependencies. This creates a cached layer with all the NuGet packages.
RUN dotnet restore "MeetingSystem.Api/MeetingSystem.Api.csproj"

# 3. Copy the rest of the application's source code.
# This layer is invalidated on every code change, but the restore layer above remains cached.
COPY . .

# 4. Publish the application, creating the final build artifact.
# The --no-restore flag is used because dependencies were already restored.
WORKDIR "/src/MeetingSystem.Api"
RUN dotnet publish "MeetingSystem.Api.csproj" -c Release -o /app/publish --no-restore

# ------------------------------------------------------------------
# Final Stage: Prepares the lightweight, secure runtime environment.
# ------------------------------------------------------------------
FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/aspnet:9.0-alpine

# Install the ICU libraries required by SqlClient and other globalization-sensitive components.
RUN apk add --no-cache icu-libs

WORKDIR /app

# Create a non-root user and group for security. Running as a non-root user is a
# critical security best practice to limit the blast radius in case of a container compromise.
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Copy the compiled application artifact from the build stage.
COPY --from=build /app/publish .

# Set ownership of the application files to the non-root user.
RUN chown -R appuser:appgroup /app

# Switch to the non-root user for all subsequent commands.
USER appuser

# Expose the port the application will listen on.
EXPOSE 8080

# The standard entrypoint to run the application.
ENTRYPOINT ["dotnet", "MeetingSystem.Api.dll"]