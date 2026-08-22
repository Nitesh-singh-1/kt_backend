# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files for layer caching
COPY *.sln .
COPY KTransport.API/*.csproj ./KTransport.API/
RUN dotnet restore

# Copy the rest of the source code and publish
COPY KTransport.API/. ./KTransport.API/
WORKDIR /source/KTransport.API
RUN dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .

# Expose standard ASP.NET Core 8 port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "KTransport.API.dll"]
