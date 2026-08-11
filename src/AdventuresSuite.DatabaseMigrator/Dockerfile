# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build
ARG RELEASE_SHA
WORKDIR /src
COPY . .
RUN dotnet restore src/AdventuresSuite.DatabaseMigrator/AdventuresSuite.DatabaseMigrator.csproj \
    --runtime linux-x64
RUN dotnet publish src/AdventuresSuite.DatabaseMigrator/AdventuresSuite.DatabaseMigrator.csproj \
    --configuration Release --runtime linux-x64 --self-contained true --no-restore \
    --output /out /p:ContinuousIntegrationBuild=true /p:Deterministic=true
RUN cp src/AdventuresSuite.DatabaseMigrator/container-entrypoint.sh /out/container-entrypoint.sh \
    && chmod 0555 /out/container-entrypoint.sh /out/AdventuresSuite.DatabaseMigrator

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0@sha256:894098eafc82e5fa02ba9f2b71d426dc78252876b9e914caae77ed95cfce185a
ARG RELEASE_SHA
LABEL org.opencontainers.image.revision=$RELEASE_SHA \
      org.opencontainers.image.source="https://github.com/ssimonton007/adventures-studio" \
      org.opencontainers.image.vendor="Adventures Studio" \
      org.opencontainers.image.title="AdventuresSuite Database Migrator"
WORKDIR /app
COPY --from=build --chown=1654:1654 /out/ /app/
USER 1654:1654
ENTRYPOINT ["/app/container-entrypoint.sh"]
