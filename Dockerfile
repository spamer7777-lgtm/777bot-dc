# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy all files
COPY . ./

# Clear NuGet cache and restore packages
RUN dotnet nuget locals all --clear \
    && dotnet restore --force

# Build and publish
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /app/out .

# Run the bot
ENTRYPOINT ["dotnet", "main.dll"]
