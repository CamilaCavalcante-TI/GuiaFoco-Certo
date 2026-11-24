# Imagem para publicar o GuiaFoco (API + site) em serviços como o Render.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY GuiaFoco.Api/GuiaFoco.Api.csproj GuiaFoco.Api/
RUN dotnet restore GuiaFoco.Api/GuiaFoco.Api.csproj
COPY GuiaFoco.Api/ GuiaFoco.Api/
RUN dotnet publish GuiaFoco.Api/GuiaFoco.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# tzdata: fuso horário America/Manaus para "hoje" e horário permitido
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "GuiaFoco.Api.dll"]
