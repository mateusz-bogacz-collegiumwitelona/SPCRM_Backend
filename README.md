# SPCRM (Simply Poor CRM) - Backend

Backend aplikacji CRM (Customer Relationship Management) przeznaczonej dla firmy handlującej wyrobami stalowymi. Projekt realizowany w ramach pracy inżynierskiej.

## Technologie i infrastruktura

Główne technologie to:

- **ASP.NET Core** – Główne REST API
- **PostgreSQL + PostGIS** – Relacyjna baza danych ze wsparciem dla danych przestrzennych (GIS)
- **Redis** – In-memory data store / Cache
- **Entity Framework Core** – ORM do komunikacji z bazą danych
- **MailPit** – Narzędzie do lokalnego testowania wysyłki wiadomości e-mail
- **Docker & Docker Compose** – Zarządzanie środowiskiem uruchomieniowym

## Wymagania wstępne

Aby uruchomić projekt lokalnie, będziesz potrzebować:

- [Docker](https://www.docker.com/) oraz Docker Compose
- [.NET SDK](https://dotnet.microsoft.com/download) (wymagane m.in. do uruchamiania migracji lokalnie)

## Uruchomienie projektu

1. **Konfiguracja zmiennych środowiskowych**
   W głównym katalogu projektu skopiuj szablon zmiennych środowiskowych `.env.example` i utwórz z niego plik `.env`:

   ```
   cp .env.example .env
   ```

   Pamiętaj, aby w pliku .env uzupełnić brakujące wartości, zwłaszcza ASPNETCORE_JWT_KEY (musi to być bezpieczny, odpowiednio długi ciąg znaków).

2. **Zbudowanie i uruchomienie kontenerów**
   Uruchom całe środowisko (Baza danych, Redis, MailPit oraz samo API) za pomocą komendy:
   ```
   docker-compose up -d --build
   ```

## Migracja bazy danych

Zarządzanie strukturą bazy danych odbywa się za pomocą narzędzia `dotnet ef`. Aby utworzyć nową migrację, wykonaj poniższą komendę w głównym folderze projektu (w miejscu, gdzie znajduje się rozwiązanie .slnx):

```
dotnet ef migrations add <NazwaMigracji> --project Infrastructure --startup-project Api
```

**Uwaga**: Upewnij się, że kontenery (w tym baza postgis) są uruchomione, jeśli Twoje migracje wymagają połączenia z bazą podczas ich generowania.

## Przydatne linki

Po poprawnym uruchomieniu środowiska (przy domyślnej konfiguracji portów z pliku .env), usługi będą dostępne pod poniższymi adresami:

| Narzędzie  | Adres URL                                | Opis                                                                     |
| ---------- | ---------------------------------------- | ------------------------------------------------------------------------ |
| Swagger    | http://localhost:8080/swagger/index.html | Dokumentacja API i panel do testowania endpointów                        |
| MailPit UI | http://localhost:8025/                   | Interfejs przeglądarkowy do podglądu przechwyconych e-maili systemowych. |
