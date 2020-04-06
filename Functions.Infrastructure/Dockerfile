FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS installer-env

COPY ./ /src
RUN cd /src && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish *.fsproj --configuration Release --output /home/site/wwwroot

FROM mcr.microsoft.com/azure-functions/dotnet:3.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    AzureWebJobsStorage=UseDevelopmentStorage=true \
    FUNCTIONS_V2_COMPATIBILITY_MODE=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]