FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Incidencias/Incidencias.csproj src/Incidencias/
RUN dotnet restore src/Incidencias/Incidencias.csproj
COPY . .
RUN dotnet publish src/Incidencias/Incidencias.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# Render inyecta PORT; Program.cs escucha en ese puerto.
EXPOSE 10000
ENTRYPOINT ["dotnet", "Incidencias.dll"]
