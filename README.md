# Synapse Blocks

Учитель создаёт свои уровни и тесты, объединяет выбранные версии в учебный набор и выдаёт классу QR-код. Ученик открывает ссылку, вводит имя и проходит уровни по порядку. Сервер проверяет решение, сохраняет каждую попытку и показывает учителю лучший прогресс.

## Структура

- `frontend/teacher` — админка на React, TypeScript и Refine.
- `frontend/student` — игра на Blazor WebAssembly.
- `backend/Synapse.Blocks.Api` — ASP.NET Core API, авторизация, версии уровней, наборы, попытки и отчёты.
- `shared/Synapse.Blocks.Core` — общие модели уровней и исполнитель блоковых программ.
- `deploy/gateway` — Nginx-маршрутизация единого домена.
- `tests` — тестовые проекты .NET; тесты API используют PostgreSQL через Testcontainers.

## Запуск всего проекта

Нужны Docker Compose и .NET 10 для локальной разработки.

1. Скопируйте `.env.example` в `.env`.
2. Замените `POSTGRES_PASSWORD` и `BOOTSTRAP_ADMIN_PASSWORD` на локальные секреты. Пароль администратора должен соответствовать требованиям Identity.
3. Запустите сервисы:

```sh
docker compose up --build -d
```

Откройте `http://localhost:8080/admin/` для админки. Первый вход: `BOOTSTRAP_ADMIN_EMAIL` и `BOOTSTRAP_ADMIN_PASSWORD` из `.env`. Администратор платформы создаёт одноразовую ссылку-приглашение для каждого учителя.

Чтобы открыть сайт с другого устройства в той же Wi-Fi сети, задайте в `.env` `WEB_BIND_ADDRESS=0.0.0.0` и `PUBLIC_ORIGIN=http://<локальный-IP-компьютера>:8080`, затем выполните `docker compose up -d api gateway`. На другом устройстве откройте `http://<локальный-IP-компьютера>:8080/admin/`. API остаётся доступным только локально на компьютере; при необходимости разрешите входящие подключения к порту 8080 в файрволе.

PostgreSQL доступен только внутри Docker-сети и хранит данные в томе `synapse-postgres-data`. API, админка, игра, база и gateway работают в отдельных контейнерах. `http://localhost:8080/health/ready` сообщает о готовности API и миграций базы. Обычная команда `docker compose down` останавливает контейнеры и сохраняет данные; для запуска снова используйте `docker compose up -d`.

В production задайте `PUBLIC_ORIGIN` равным внешнему HTTPS-адресу и включите `COOKIE_SECURE=true`. Если меняете `WEB_PORT`, обновите `PUBLIC_ORIGIN`, чтобы QR-ссылки указывали на правильный адрес. Секреты из `.env` не добавляйте в Git.

## Работа учителя и ученика

Учитель входит в `/admin/`, редактирует собственный каталог, добавляет открытые и скрытые тесты, собирает упорядоченный набор уровней, публикует его и создаёт QR-код. QR-код можно распечатать. Ссылка показывается при создании или перевыпуске; сохраните её сразу.

Ученик открывает `/case/<qr-token>`, вводит имя и начинает попытку без аккаунта. Незавершённая попытка продолжается после обновления страницы на том же устройстве. Ученик может начать несколько попыток. В отчёте хранятся все попытки и лучший прогресс — максимальное количество завершённых уровней для имени в рамках набора. Равные результаты не ранжируются по времени.

Сохранённые версии уровней и порядок набора фиксируются для новой попытки. Изменение или архивирование набора не меняет историю; закрытый QR перестаёт открываться новым участникам. Скрытые тесты и ожидаемые ответы остаются на сервере.

## Локальная разработка

Для админки запустите API и PostgreSQL в Docker, затем Vite отдельно:

```sh
docker compose up -d postgres api
cd frontend/teacher
npm ci
npm run dev
```

Откройте адрес Vite из вывода команды (обычно `http://localhost:5173/admin/`). Запросы `/api` перенаправляются на API-контейнер. Чтобы QR-ссылки открывали ученическую игру, запустите весь Compose-стек и используйте `PUBLIC_ORIGIN` из `.env`.

Чтобы запустить ученическую игру с hot reload, остановите контейнер `student` и запустите Blazor отдельно:

```sh
docker compose stop student
cd frontend/student
dotnet watch
```

Полный стек, включая статические сборки всех приложений и gateway:

```sh
docker compose up --build -d
```

Для изменений API пересоберите `api`, для изменений админки или игры пересоберите соответствующий сервис:

```sh
docker compose up --build -d api
docker compose up --build -d teacher
docker compose up --build -d student
```

Линтер и production-сборка админки:

```sh
cd frontend/teacher
npm run lint
npm run build
```

## Сборка и тестовые команды

```sh
dotnet build Synapse.Blocks.slnx
dotnet test tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj
dotnet test tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj
dotnet test tests/Synapse.Blocks.Student.Tests/Synapse.Blocks.Student.Tests.csproj
cd frontend/teacher && npm test -- --run
```

API-тесты запускают отдельный PostgreSQL через Testcontainers; они не подключаются к базе из Compose.
