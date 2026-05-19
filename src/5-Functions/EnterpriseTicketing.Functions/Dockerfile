FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 80

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/5-Functions/EnterpriseTicketing.Functions/EnterpriseTicketing.Functions.csproj", "5-Functions/EnterpriseTicketing.Functions/"]
COPY ["src/3-Infrastructure/EnterpriseTicketing.Infrastructure/EnterpriseTicketing.Infrastructure.csproj", "3-Infrastructure/EnterpriseTicketing.Infrastructure/"]
COPY ["src/2-Application/EnterpriseTicketing.Application/EnterpriseTicketing.Application.csproj", "2-Application/EnterpriseTicketing.Application/"]
COPY ["src/1-Domain/EnterpriseTicketing.Domain/EnterpriseTicketing.Domain.csproj", "1-Domain/EnterpriseTicketing.Domain/"]
RUN dotnet restore "5-Functions/EnterpriseTicketing.Functions/EnterpriseTicketing.Functions.csproj"

COPY src/ .
WORKDIR /src/5-Functions/EnterpriseTicketing.Functions
RUN dotnet publish "EnterpriseTicketing.Functions.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=build /app/publish .
