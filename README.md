# Synapse Blocks

Платформа для создания учебных уровней и кейсов на блочном программировании. Учителя работают со своим каталогом, собирают из версий уровней кейсы и выдают ученикам QR-ссылку. Ученики вводят имя, решают уровни и могут начинать отдельные попытки. Сервер проверяет программу и хранит прогресс.

## Структура проекта

- `frontend/student` — существующая игра на Blazor WebAssembly.
- `backend/Synapse.Blocks.Api` — ASP.NET Core API, Identity, авторизация, контент, QR-ссылки, попытки и отчёты.
- `shared/Synapse.Blocks.Core` — общие модели уровней и исполнитель программ на C#.
- `tests` — модульные и интеграционные тесты. Для API используются отдельные временные экземпляры PostgreSQL через Testcontainers.

## Запуск API и базы

Нужны Docker Compose и .NET 10 для локальной сборки и запуска тестов.

1. Скопируйте `.env.example` в `.env`.
2. Замените `POSTGRES_PASSWORD` и `BOOTSTRAP_ADMIN_PASSWORD` на локальные секреты. Пароль администратора должен соответствовать требованиям Identity.
3. Запустите сервисы:

```sh
docker compose up --build -d
```

API доступен на `http://localhost:8081`, PostgreSQL — только внутри сети Compose. `GET /health/ready` проверяет соединение с базой и наличие всех миграций. PostgreSQL хранит данные в именованном томе `synapse-postgres-data`; обычный `docker compose down` останавливает контейнеры и сохраняет базу.

Переменные в `.env` не добавляйте в Git. В production передавайте секреты через окружение и включайте `COOKIE_SECURE=true` за HTTPS.

## Администратор и учителя

При первом запуске API создаёт администратора платформы из `BOOTSTRAP_ADMIN_EMAIL` и `BOOTSTRAP_ADMIN_PASSWORD`. Администратор выдаёт одноразовое приглашение учителю; в базе хранится только хеш приглашения. Учитель входит по cookie-сессии, защищённой от CSRF.

Каталог уровней, кейсы и QR-ссылки принадлежат создавшему их учителю. Изменение уровня создаёт новую неизменяемую версию. Кейсы ссылаются на выбранные версии, а QR-токены хранятся только в виде хеша. Архив закрывает доступ по ссылке и сохраняет попытки и отчёты.

## Проверка и тесты

```sh
dotnet test tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj
dotnet test tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj
dotnet build Synapse.Blocks.slnx
```

Интеграционные тесты API запускают PostgreSQL через Testcontainers. Для локальной базы достаточно Docker Compose; не подключайте тесты к рабочей базе.
