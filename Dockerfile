# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine AS build
WORKDIR /source

# Copy csproj files and restore
COPY src/Matador.Core/Matador.Core.csproj src/Matador.Core/
COPY src/Matador.Web/Matador.Web.csproj src/Matador.Web/
RUN dotnet restore src/Matador.Web/Matador.Web.csproj

# Copy the rest and build
COPY src/ src/
WORKDIR /source/src/Matador.Web
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Matador.Web.dll"]
