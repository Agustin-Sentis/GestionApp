# Contexto de build: ./src (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la capa de restore se cachea mientras no cambien las dependencias
COPY GastosBackend.Domain/*.csproj GastosBackend.Domain/
COPY GastosBackend.Application/*.csproj GastosBackend.Application/
COPY GastosBackend.Infrastructure/*.csproj GastosBackend.Infrastructure/
COPY GastosBackend/*.csproj GastosBackend/
RUN dotnet restore GastosBackend/GastosBackend.Presentation.csproj

COPY GastosBackend.Domain/ GastosBackend.Domain/
COPY GastosBackend.Application/ GastosBackend.Application/
COPY GastosBackend.Infrastructure/ GastosBackend.Infrastructure/
COPY GastosBackend/ GastosBackend/
RUN dotnet publish GastosBackend/GastosBackend.Presentation.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "GastosBackend.Presentation.dll"]
