FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Synapse.Blocks.Api/ backend/Synapse.Blocks.Api/
COPY shared/Synapse.Blocks.Core/ shared/Synapse.Blocks.Core/
RUN dotnet publish backend/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Synapse.Blocks.Api.dll"]
