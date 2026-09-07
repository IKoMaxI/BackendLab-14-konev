# StoreApiLR11 — CORS

ASP.NET Core Web API интернет-магазина с двумя политиками CORS.

## Запуск

Создайте базу данных `store_db`, при необходимости измените строку подключения в `appsettings.json`, затем выполните:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

Swagger: `https://localhost:7193/swagger`.

## Проверка CORS

Запустите API, затем из каталога приложения запустите внешний клиент:

```bash
python -m http.server 8000
```

Откройте `http://localhost:8000/test-cors.html`.

- `Development`: политика `AllowAll`, разрешены любые источники, методы и заголовки.
- `Production`: политика `AllowFrontend`, разрешены `http://localhost:8000` и `https://myshop-frontend.com`, методы GET, POST, PUT, DELETE, OPTIONS и заданные заголовки.

Для проверки запрета запустите клиент на порте 8001 и включите среду `Production`.
