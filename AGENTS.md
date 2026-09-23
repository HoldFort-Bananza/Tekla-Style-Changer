# AGENTS.md — Style Changer

Plik dla agentów (Claude i innych) pracujących w tym repo. Zastępuje
project-level `CLAUDE.md` — ogólne zasady środowiska Tekla są w
`C:\Users\HFT-FJarosz\Projekty\CLAUDE.md` (nadrzędny, wspólny dla wszystkich
projektów) i obowiązują też tutaj bez powtarzania.

## Jednym zdaniem

Dodatek do Tekla Structures 2025 (rysunki, zewnętrzny .exe łączący się przez
Open API, nie wtyczka ładowana wewnątrz Tekli): operator zaznacza w edytorze
Tekli widok (albo dowolny obiekt w widoku, np. wymiar czy część), klika jeden
przycisk "Pokaż sąsiadów" w appce, a appka podmienia temu jednemu widokowi
styl (View Properties) na wybrany z listy — domyślnie na
`W_View_Railing_Neighbour`. Zastępuje ręczny proces: zaznacz widok → View
Properties → szukaj na rozwijanej liście → zatwierdź (patrz zrzut ekranu z
opisu zadania).

**v0.1 działa end-to-end i jest potwierdzone na żywym rysunku** (patrz
"Status projektu").

## Skąd to się wzięło (kontekst)

Operator rysuje widoki, które pokazują tzw. "sąsiada" — część kontekstową
widoczną w widoku obok właściwej, detalowanej części (np. poręcz pokazana
razem z sąsiednim elementem dla kontekstu; sama Tekla ma na to natywny
atrybut widoku `ViewExtensionForNeighbourParts`). Dla takich widoków w
katalogu Tekli istnieje dedykowany, nazwany plik View Properties
(`W_View_Railing_Neighbour.vi`). Operator dziś robi to ręcznie za każdym
razem. Appka robi to za niego jednym kliknięciem — bez detekcji "czy to
widok z sąsiadem", po prostu aplikuje wskazany styl na to, co jest aktualnie
zaznaczone. Żadna heurystyka rozpoznawania nie jest potrzebna (potwierdzone
z użytkownikiem — wcześniejsza wersja tego pliku zakładała inaczej, było to
błędne domysł).

**Bitrix jest poza zakresem.** Nie integrować z Bitrix24 API, nie logować do
Bitrix, nie zakładać żadnej zależności od niego (potwierdzone z
użytkownikiem — wcześniejsza wzmianka w opisie zadania była prywatną notatką
autora, nie wymaganiem).

## Ustalone fakty o API (zweryfikowane refleksją na pakietach NuGet 2025.0.0 + na żywej Tekli, nie zgadane)

- **Pliki stylu widoku mają rozszerzenie `.vi`** i leżą w katalogu
  `attributes\` modelu oraz w katalogach firmowych/projektowych zwracanych
  przez `PropertyFileDirectories`. Przykład z żywego modelu (`Style_2D`):
  `W_View_Railing_Neighbour.vi` (jest tam też `.vi.copt` — sidecar, nie
  ruszać).
- **Listowanie dostępnych stylów:** `Tekla.Structures.TeklaStructuresFiles.
  GetFileDictionaryByExtension("vi", modelPath)` — metoda **statyczna**,
  rozszerzenie **BEZ kropki** ("vi", nie ".vi" — z kropką zwraca 0 wyników,
  sprawdzone empirycznie). Zwraca `Dictionary<nazwa-bez-rozszerzenia, pełna
  ścieżka>`. `GetStandardPropertyFileDirectories()` jest **internal** w tej
  wersji API mimo publicznej dokumentacji XML — użyj konstruktora
  `new TeklaStructuresFiles(modelPath)` i czytaj właściwość
  `PropertyFileDirectories` zamiast tego.
- **Wczytanie/aplikowanie stylu:** `view.Attributes = new
  View.ViewAttributes("W_View_Railing_Neighbour");` (konstruktor przyjmuje
  nazwę pliku BEZ rozszerzenia), potem `view.Modify()`, potem
  `drawing.CommitChanges()` (zgodnie z pułapką z nadrzędnego CLAUDE.md:
  `TransactionManager` nie dotyczy obiektów rysunkowych).
- **Wyznaczenie widoku z zaznaczenia:** `dh.GetDrawingObjectSelector().
  GetSelected()` zwraca `DrawingObjectEnumerator` zaznaczonych obiektów.
  Jeśli zaznaczony obiekt to `View` — użyj go wprost. W przeciwnym razie
  `obj.GetView()` (metoda na bazowej klasie `DrawingObject`) zwraca
  `ViewBase`; rzutować na `View`. Potwierdzone na żywo: zaznaczenie widoku w
  edytorze Tekli daje `obj is View` = true.
- **Weryfikacja end-to-end na żywym rysunku** (`[35099] Einzelteil
  Geländer`, 2026-09-23): po kliknięciu przycisku zaznaczony widok miał
  `Attributes.Scale == 10` i `Attributes.ViewExtensionForNeighbourParts ==
  50` — dokładnie wartości z pliku `W_View_Railing_Neighbour.vi` wczytanego
  osobno do izolowanego obiektu w pamięci. Styl realnie się zapisał.

## Wymagania funkcjonalne (stan zaimplementowany w v0.1)

1. **Wyzwalacz:** operator zaznacza widok (albo dowolny obiekt w widoku) w
   edytorze rysunku Tekli, klika przycisk "Pokaż sąsiadów" w appce.
2. **Działanie:** appka ustawia na tym jednym widoku wybrany zestaw View
   Properties — bez dodatkowych okien/dialogów po stronie operatora.
3. **Styl nie jest hardcodowany:** domyślna nazwa (`W_View_Railing_
   Neighbour`) jest w `StyleChangerService.DefaultStyleName` z komentarzem
   skąd się wzięła; pełna lista dostępnych stylów wypełnia się w UI z
   realnego katalogu na dysku (`GetFileDictionaryByExtension`), operator
   może wybrać inny z rozwijanej listy.
4. **Dane tylko z API** — zero screenshotów, zero analizy pikseli.

## Architektura (jak w RO Axis Dimension Remover / Radius Dimention Mover)

Zewnętrzny WinExe (nie plugin ładowany przez Teklę), łączy się przez Open
API. Pliki:

- `StyleChanger.csproj` — WinExe, net48, x64, PackageReference Tekla.
  Structures/.Drawing/.Model 2025.0.0 + TSAppConfigPatcherTask.
- `Program.cs` — wejście; `--diag-active` uruchamia `DiagRunner` (tryb
  konsolowy, **tylko odczyt**, do diagnostyki bez klikania w GUI).
- `MainForm.cs` — UI: `ComboBox` ze stylami + domyślnym zaznaczonym,
  przycisk "Pokaż sąsiadów", status, log.
- `StyleChangerService.cs` — rdzeń logiki (`GetAvailableStyles`,
  `ResolveTargetView`, `ApplyStyle`).
- `DiagRunner.cs` — diagnostyka: connection status, aktywny rysunek,
  zaznaczenie + jego atrybuty, katalogi property files, test
  `GetFileDictionaryByExtension`. **Trwały element projektu, nie
  rusztowanie do wyrzucenia** — automatyzacja (Claude Code) nie klika w
  GUI, więc to jedyny sposób sprawdzić stan bez człowieka przy ekranie.
  Świadomie tylko-do-odczytu: nigdy nie woła `Modify()`/`CommitChanges()`.

## Do zrobienia / możliwe rozszerzenia (nieblokujące, nikt o to jeszcze nie prosił)

- Nazwa przycisku/appki robocza — do potwierdzenia z operatorem, jeśli
  będzie inna niż "Pokaż sąsiadów"/"Style Changer".
- ~~Obsługa wielokrotnego zaznaczenia~~ — zrobione 2026-09-23:
  `ResolveTargetViews` zwraca WSZYSTKIE widoki z zaznaczenia (deduplikacja
  po `Identifier.GUID`, bo `GetView()` może zwrócić nowy obiekt-wrapper dla
  tego samego widoku co bezpośrednie zaznaczenie), `ApplyStyleToViews`
  aplikuje styl na każdym i commituje raz na koniec. Zweryfikowane na żywo:
  zaznaczenie dwóch widoków naraz na [35020] → oba dostały
  `ViewExtensionForNeighbourParts=50` po jednym kliknięciu.

## Kontrola wersji - TWARDA ZASADA

**Nigdy nie commitować/pushować bezpośrednio na `dev` ani `release`.**
Zawsze: nowa gałąź tematyczna (`feature/...`, `fix/...`, `docs/...`) →
commit(e) → `gh pr create` → merge PR-em. Repo:
https://github.com/HoldFort-Bananza/Tekla-Style-Changer

- `dev` - domyślna gałąź robocza (branch protection: PR wymagany,
  `enforce_admins: true` - dotyczy też właściciela org, force-push i
  usuwanie gałęzi zablokowane; 0 wymaganych review, bo to projekt
  jednoosobowy - PR może zmergować sam autor, ale gałąź musi istnieć).
- `release` - potwierdzony kod, te same zabezpieczenia co `dev`.
- Ustawione 2026-09-23 przez `gh api .../branches/<nazwa>/protection`
  (PUT) - jeśli trzeba kiedyś zmienić reguły ochrony, przez to samo API,
  nie przez ręczne ustawienia w UI, żeby zostało udokumentowane, co się
  zmieniło i kiedy.
- Scalanie PR-a przez API bywa blokowane przez klasyfikator auto mode w
  Claude Code (patrz `../CLAUDE.md`) - otwarcie PR-a przechodzi, merge
  może wymagać kliknięcia przez operatora.

## Środowisko i konwencje

Patrz `C:\Users\HFT-FJarosz\Projekty\CLAUDE.md` — obowiązuje w całości:
C#/.NET Framework 4.8/x64, Tekla Open API 2025.0.0 przez NuGet,
`TSAppConfigPatcherTask` w `.csproj` (inaczej `GetConnectionStatus()` zawsze
`false`), `Drawing.CommitChanges()` po `Modify()`, `Identifier`/
`ModelIdentifier` do przejścia rysunek→model, logowanie przez wstrzykiwany
`Action<string>`, komentarze/logi po polsku, komunikaty commitów po
angielsku, `_` przed polami prywatnymi UI, bez ścieżek na sztywno, zamknąć
`StyleChanger.exe` (`taskkill /F /IM StyleChanger.exe`) przed przebudowaniem
jeśli działa.

## Status projektu

**v0.1 zbudowane i działające end-to-end**, zweryfikowane na żywej Tekli
2026-09-23 na DWÓCH różnych rysunkach ([35099] i [35020], oba "Einzelteil
Geländer") — nie jest zahardcodowane pod jeden przypadek. Wizualnie
potwierdzone: po kliknięciu na widoku pojawia się wyraźna niebieska
ramka z zakreskowaniem (granica `ViewExtensionForNeighbourParts`), której
wcześniej nie było — efekt jest widoczny, nie tylko liczbowy w API.

**Zmierzone, nie zgadane:** jedyne pole, które zmienia aplikowanie stylu
`W_View_Railing_Neighbour`, to `ViewExtensionForNeighbourParts` (0→50).
Wszystkie inne pola (`LocationBy`, `FixedViewPlacing`,
`PartialProfileOffset`, `DatumLevel`, `Scale`...) są identyczne przed i po -
zweryfikowane pełnym zrzutem atrybutów (`DumpAttributes` w `DiagRunner.cs`)
na tym samym widoku przed kliknięciem i po. Wizualne "przesunięcie części",
które operator zauważył przy pierwszym teście na [35020], to bezpośredni,
oczekiwany skutek rozszerzenia ramki widoku, nie efekt uboczny nadpisania
niepowiązanych ustawień.

**Poprawiona usterka UI (2026-09-23):** `MainForm` ustawiał wynik operacji
TYLKO w etykiecie statusu, nie w logu - a etykieta bywała nadpisywana z
powrotem na ogólny opis rysunku, bo `Activated` odpalało się ponownie tuż
po kliknięciu (podejrzenie: Tekla na chwilę przejmuje fokus w trakcie
`CommitChanges()`). Efekt: log pokazywał tylko błędy, sukces znikał bez
śladu. Naprawione przez `MainForm.SetResult()`, które ZAWSZE loguje wynik
- patrz też pamięć `feedback_log_everything` (operator: "log jest poto żeby
logować, ma logować wszystko").

Brak repo git. Brak `README.md`. Brak instalatora (RO Axis Dimension
Remover ma wzorzec Inno Setup w `installer/` — do rozważenia, gdy appka
będzie gotowa do dystrybucji na inne stanowiska). `DiagRunner.cs` ma teraz
też `--test-other-drawing` (SZUKA i PRZEŁĄCZA rysunki - operator wolał
ręcznie wskazywać rysunek, więc w praktyce nieużywane, ale zostaje jako
dostępna opcja) i `--dump-style <nazwa>` (bezpieczny odczyt zawartości
pliku .vi).
