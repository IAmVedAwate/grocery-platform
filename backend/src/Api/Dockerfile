# Multi-stage build: SDK image builds/publishes, runtime image ships —
# the final image has no build tooling in it. See docs/PRD.md §22 and
# docs/architecture/deployment-architecture.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/Api/Api.csproj backend/src/Api/
COPY backend/src/Application/Application.csproj backend/src/Application/
COPY backend/src/Domain/Domain.csproj backend/src/Domain/
COPY backend/src/Infrastructure/Infrastructure.csproj backend/src/Infrastructure/
COPY backend/src/Shared/Shared.csproj backend/src/Shared/
RUN dotnet restore backend/src/Api/Api.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Api/Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

# Non-root execution — see docs/PRD.md §22. Microsoft's ASP.NET runtime
# images ship a built-in unprivileged "app" user (UID $APP_UID) for
# exactly this purpose, so there's no need to create one manually.
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# No curl in the slim runtime image — a raw TCP probe avoids installing
# extra packages just for the health check.
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
    CMD ["/bin/bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080"]

ENTRYPOINT ["dotnet", "Api.dll"]
