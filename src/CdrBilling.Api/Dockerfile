FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CdrBilling.slnx ./
COPY src/CdrBilling.Api/CdrBilling.Api.csproj src/CdrBilling.Api/
COPY src/CdrBilling.Application/CdrBilling.Application.csproj src/CdrBilling.Application/
COPY src/CdrBilling.Domain/CdrBilling.Domain.csproj src/CdrBilling.Domain/
COPY src/CdrBilling.Infrastructure/CdrBilling.Infrastructure.csproj src/CdrBilling.Infrastructure/

RUN dotnet restore src/CdrBilling.Api/CdrBilling.Api.csproj

COPY src ./src

RUN dotnet publish src/CdrBilling.Api/CdrBilling.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:13000

COPY --from=build /app/publish ./

EXPOSE 13000

ENTRYPOINT ["dotnet", "CdrBilling.Api.dll"]
