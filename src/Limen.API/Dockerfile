FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node for Angular build
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs

COPY Directory.Build.props Limen.slnx ./
COPY contracts/ ./contracts/
COPY src/ ./src/
RUN dotnet publish src/Limen.API/Limen.API.csproj -c Release -o /app --no-restore=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Limen.API.dll"]
