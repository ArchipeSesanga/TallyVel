# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

# 1) Project files only, then restore: this layer is cached until a .csproj changes.
COPY TallyVel.slnx ./
COPY Api/Api.csproj Api/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore Api/Api.csproj

# 2) Then the source. A code change only invalidates layers from here down.
COPY Api/ Api/
COPY Domain/ Domain/
COPY Infrastructure/ Infrastructure/
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- migration bundle (used by the one-shot migrate service) ----------
FROM build AS bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"
# dotnet ef starts Program.cs, which refuses to start without a connection string.
# This placeholder exists only for this one command; nothing connects to it,
# and this stage is never shipped (only /efbundle is copied out of it).
RUN ConnectionStrings__TallyVel="Host=design-time;Database=design-time;Username=none;Password=none" \
    dotnet ef migrations bundle --project Infrastructure --startup-project Api \
      --configuration Release -o /efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS migrator
WORKDIR /app
COPY --from=bundle /efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------- runtime (the default, last stage) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]