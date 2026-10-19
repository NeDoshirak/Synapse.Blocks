FROM node:22-alpine AS markdown-assets
WORKDIR /assets
COPY frontend/student/package.json frontend/student/package-lock.json ./
COPY frontend/student/scripts/ ./scripts/
COPY frontend/student/wwwroot/js/markdown-renderer.entry.js ./wwwroot/js/markdown-renderer.entry.js
RUN npm ci && npm run build:markdown-assets

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Synapse.Blocks.Core/ shared/Synapse.Blocks.Core/
COPY frontend/student/ frontend/student/
COPY --from=markdown-assets /assets/wwwroot/lib/markdown/ frontend/student/wwwroot/lib/markdown/
RUN dotnet publish frontend/student/Synapse.Blocks.csproj -c Release -o /app/publish

FROM nginx:1.27-alpine
COPY frontend/student/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot/ /usr/share/nginx/html/
EXPOSE 80
