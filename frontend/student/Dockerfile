FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Synapse.Blocks.Core/ shared/Synapse.Blocks.Core/
COPY frontend/student/ frontend/student/
RUN dotnet publish frontend/student/Synapse.Blocks.csproj -c Release -o /app/publish

FROM nginx:1.27-alpine
COPY frontend/student/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot/ /usr/share/nginx/html/
EXPOSE 80
