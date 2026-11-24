FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/IgFoodBot.Api/IgFoodBot.Api.csproj src/IgFoodBot.Api/
RUN dotnet restore src/IgFoodBot.Api/IgFoodBot.Api.csproj
COPY src/ src/
RUN dotnet publish src/IgFoodBot.Api/IgFoodBot.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "IgFoodBot.Api.dll"]
