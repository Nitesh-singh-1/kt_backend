# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore/build/publish the API project directly (NOT the .sln) — the solution also
# references KTransport.API.Tests (added in TASK-013), and `dotnet restore` against
# the .sln enumerates every project it lists. Since only the API's .csproj is copied
# into this build context, restoring the .sln fails looking for the test project's
# .csproj that was never copied in. The production image has no use for the test
# project anyway, so we just never point dotnet at the .sln here.
COPY KTransport.API/*.csproj ./KTransport.API/
RUN dotnet restore ./KTransport.API/KTransport.API.csproj

# Copy the rest of the source code and publish
COPY KTransport.API/. ./KTransport.API/
WORKDIR /source/KTransport.API
RUN dotnet publish KTransport.API.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .

# Expose standard ASP.NET Core 8 port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "KTransport.API.dll"]
