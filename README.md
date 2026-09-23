# Style Changer

Dodatek do Tekla Structures 2025 (rysunki). Operator zaznacza w edytorze
Tekli widok (albo dowolny obiekt w widoku, np. wymiar czy część), klika
jeden przycisk **"Pokaż sąsiadów"**, a appka podmienia temu jednemu
widokowi styl (View Properties) na wybrany z listy — domyślnie na
`W_View_Railing_Neighbour`. Zastępuje ręczny proces: zaznacz widok → View
Properties → szukaj na rozwijanej liście → zatwierdź.

Zewnętrzny `.exe`, nie wtyczka ładowana wewnątrz Tekli — łączy się przez
Tekla Open API do już uruchomionej Tekli.

## Wymagania

- Tekla Structures 2025, uruchomiona i z otwartym modelem/rysunkiem.
- Windows, .NET Framework 4.8 (instalowany razem z Teklą 2025).

## Build

```
dotnet build StyleChanger.csproj -c Debug -p:Platform=x64
```

Jeśli appka jest uruchomiona, zamknij ją przed przebudowaniem
(`taskkill /F /IM StyleChanger.exe`) — inaczej build się nie uda
(plik `.exe` zablokowany).

## Użycie

1. Uruchom `StyleChanger.exe`.
2. W Tekli zaznacz widok (albo obiekt w widoku) w otwartym rysunku.
3. W oknie appki wybierz styl z listy (domyślnie zaznaczony
   `W_View_Railing_Neighbour`) i kliknij **"Pokaż sąsiadów"**.
4. Wynik (sukces albo błąd) pojawia się w logu w oknie appki. Zmiana jest
   od razu zapisana w rysunku i odwracalna przez Ctrl+Z w Tekli.

## Diagnostyka bez GUI

```
StyleChanger.exe --diag-active
```

Tryb konsolowy, tylko do odczytu — pokazuje status połączenia, aktywny
rysunek, aktualne zaznaczenie i jego atrybuty widoku, oraz dostępne katalogi
i pliki stylów. Bezpieczne do uruchamiania w dowolnym momencie, nic nie
zmienia w rysunku.

```
StyleChanger.exe --dump-style <nazwa-stylu-bez-rozszerzenia>
```

Wczytuje wskazany plik `.vi` do izolowanego obiektu w pamięci i wypisuje
jego zawartość — też tylko do odczytu.

## Więcej

Szczegóły architektury, ustalone fakty o Tekla Open API i historia decyzji:
[AGENTS.md](AGENTS.md). Zasady środowiska wspólne dla wszystkich projektów
Tekla w tym katalogu: `../CLAUDE.md`.
