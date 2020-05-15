FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

WORKDIR /build

COPY *.fsproj ./

RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o ./output --runtime alpine-x64 --self-contained true /p:PublishTrimmed=true /p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-alpine AS runtime

LABEL maintainer "Alberto De Pena <albertodepena89@gmail.com>"

WORKDIR /DbMigrator

COPY --from=build ./build/output/ ./

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DB_CONNECTION_STRING=""
    
RUN chmod +x ./EventStore.DbMigrator.SqlServer

ENTRYPOINT ["./EventStore.DbMigrator.SqlServer"]