FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["backend-api-transcricao.csproj", "backend-api-transcricao/"]
RUN dotnet restore "backend-api-transcricao/backend-api-transcricao.csproj"
COPY . .
WORKDIR "/src/backend-api-transcricao"
RUN dotnet build "backend-api-transcricao.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-api-transcricao.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend-api-transcricao.dll"]
