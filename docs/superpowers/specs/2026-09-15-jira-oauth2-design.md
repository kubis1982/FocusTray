# Migracja integracji JIRA na OAuth 2.0 (3LO) — Design

Data: 2026-09-15
Status: zatwierdzony (do implementacji)

## Kontekst i cel

Integracja z JIRA obecnie korzysta z Basic Auth (email + API token), przechowywanego
zaszyfrowanego (DPAPI) w generycznym `ICredentialService`/`WindowsCredentialService`
pod kluczem `FocusTray_Jira`. Integracja z Microsoft Teams została wcześniej
zmigrowana na OAuth 2.0 (Authorization Code + PKCE, via MSAL.NET), z interaktywnym
logowaniem przez przeglądarkę systemową i dedykowanym, zaszyfrowanym cache tokenów.

Celem tego dokumentu jest zaplanowanie analogicznej migracji dla JIRA: zastąpienie
Basic Auth pełnym OAuth 2.0 (3LO) Atlassiana, przy zachowaniu tego samego poziomu
UX (logowanie przez przeglądarkę, brak ręcznego wpisywania sekretów) i architektury
warstwowej repo (Core / Infrastructure / UI).

Migracja jest **pełnym zastąpieniem** — Basic Auth i API tokeny znikają całkowicie
z integracji JIRA, tak jak to zrobiono dla Teams.

## Różnice względem wzorca Teams (Atlassian vs Azure AD/Graph)

Atlassian nie ma odpowiednika MSAL.NET, a OAuth 2.0 (3LO) Atlassiana to **czysty
OAuth2, nie OIDC** — brak discovery document, brak id_tokena, brak JWKS. To
wyklucza użycie pełnego pakietu `IdentityModel.OidcClient` (zaprojektowanego pod
protokół OIDC z walidacją id_tokena). Zamiast tego użyjemy niższopoziomowego pakietu
`IdentityModel` (bez `.OidcClient`) — helpery PKCE oraz rozszerzenia `HttpClient` do
wymiany kodu na token (`RequestAuthorizationCodeTokenAsync`,
`RequestRefreshTokenAsync`), bez narzucania semantyki OIDC.

Kluczowe różnice techniczne:

- **Endpointy**: autoryzacja `https://auth.atlassian.com/authorize`
  (`audience=api.atlassian.com`), token `https://auth.atlassian.com/oauth/token`.
- **cloudId zamiast subdomeny**: po zalogowaniu trzeba wywołać
  `GET https://api.atlassian.com/oauth/token/accessible-resources`, aby uzyskać
  `cloudId` powiązany z instancją JIRA. Wywołania REST API idą przez
  `https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/...` zamiast
  dotychczasowego `https://{company}.atlassian.net/rest/api/2/...`.
- **Refresh token rotujący**: każde użycie refresh tokena zwraca nowy refresh
  token, który trzeba zapisać na nowo (inaczej niż w MSAL, gdzie refresh jest
  zarządzany całkowicie wewnętrznie i niewidoczny dla kodu aplikacji). Wymaga
  scope'u `offline_access`.
- **Rejestracja aplikacji**: OAuth 2.0 (3LO) app w Atlassian Developer Console
  (developer.atlassian.com/console/myapps), typ **klient publiczny + PKCE** (bez
  `client_secret` — spójne z modelem bezpieczeństwa Teams/MSAL, gdzie sekret też
  nie jest osadzony w aplikacji desktopowej).
- **Klient REST**: zostajemy przy obecnym kliencie
  `Kubis1982.Atlassian.Jira.RestClient.v2` (REST API v2) — 3LO Atlassiana działa
  z API v2 pod tym samym proxy `api.atlassian.com/ex/jira/{cloudId}/rest/api/2/`.
  Zmienia się tylko auth provider (Bearer zamiast Basic) i sposób budowy base URL
  (cloudId zamiast company). Migracja do REST API v3 jest świadomie poza zakresem.

## Architektura / flow logowania

1. Użytkownik klika „Zaloguj się” w `JiraLoginDialog` (bez pól Company/Email/Token
   — jeden przycisk, jak w `TeamsLoginDialog`).
2. `JiraAuthService.LoginAsync()` generuje PKCE `code_verifier`/`code_challenge`
   (S256) + losowy `state`, otwiera systemową przeglądarkę na URL autoryzacji
   Atlassiana, i uruchamia lokalny `HttpListener` na loopback redirect URI.
3. Po zgodzie użytkownika przeglądarka wraca na loopback z `code`/`state` w query
   string — listener je przechwytuje (weryfikując `state`), zwraca prostą stronę
   HTML „możesz zamknąć tę kartę” i się zamyka.
4. Wymiana `code` → `access_token` + `refresh_token` przez
   `IdentityModel.Client` (`POST .../oauth/token`, `grant_type=authorization_code`,
   z `code_verifier`).
5. Wywołanie `GET .../accessible-resources` → lista dostępnych site'ów JIRA.
   - Dokładnie 1 wynik → wybierany automatycznie.
   - >1 wynik → `JiraSitePickerDialog` (nazwa + URL site'u do wyboru).
   - 0 wyników → błąd „brak dostępnej instancji JIRA dla tego konta”.
6. Zapis `{AccessToken, RefreshToken, ExpiresAtUtc, CloudId, SiteUrl, SiteName}`
   do `JiraTokenCacheHelper` (JSON zaszyfrowany DPAPI, plik
   `%LocalAppData%\FocusTray\jira_token_cache.dat`).
7. `GetAccessTokenAsync()` (wywoływane przez `JiraService.CreateJiraClient()`)
   sprawdza czas wygaśnięcia (bufor 5 min); jeśli token wygasł, wymienia refresh
   token na nowy (`grant_type=refresh_token`) i **zapisuje nowy, rotujący refresh
   token z powrotem do cache**.

## Komponenty — nowe pliki

| Plik | Rola |
|---|---|
| `src/FocusTray.Infrastructure/Jira/JiraOAuthConfiguration.cs` | Stała konfiguracja: `ClientId`, `Scopes` (`read:jira-work write:jira-work read:jira-user offline_access`), `RedirectUri`, endpointy autoryzacji/tokena/accessible-resources, szablon base URL API. Analog `TeamsConfiguration.cs`. |
| `src/FocusTray.Infrastructure/Jira/JiraTokenCacheHelper.cs` | Load/Save/Clear zaszyfrowanego (DPAPI) pliku z modelem tokenów OAuth JIRA. Analog `MsalTokenCacheHelper.cs`. |
| `src/FocusTray.Infrastructure/Jira/JiraLoopbackListener.cs` | Minimalny `HttpListener` przechwytujący redirect z przeglądarki (MSAL robi to wewnętrznie dla Teams; dla JIRA trzeba to dopisać samodzielnie). |
| `src/FocusTray/ViewModels/JiraSitePickerDialogViewModel.cs` | ViewModel dialogu wyboru site'u JIRA (pokazywany tylko gdy accessible-resources zwraca >1 wynik). |
| `src/FocusTray/Views/JiraSitePickerDialog.xaml(.cs)` | Widok dialogu wyboru site'u. |

## Komponenty — pliki do zmiany

| Plik | Zmiana |
|---|---|
| `src/FocusTray.Core/Services/IJiraAuthService.cs` | `LoginAsync()` bezparametrowe (bez `company/email/apiToken`); dodanie `GetAccessTokenAsync()`; zachowanie `LogoutAsync`, `GetCurrentUserAsync`, `TryAutoLoginAsync`, `AuthStateChanged`; ekspozycja bieżącego site'u (`CloudId`/`SiteUrl`/`SiteName`) zamiast `CurrentCompany`. |
| `src/FocusTray.Infrastructure/Jira/JiraAuthService.cs` | Przepisanie na flow Authorization Code + PKCE; usunięcie `BasicAuthProvider` i zależności od `ICredentialService` (analogicznie do commitu Teams „Remove ICredentialService from TeamsAuthService”). |
| `src/FocusTray.Infrastructure/Jira/JiraConfiguration.cs` | Usunięcie pola `Company` (informacja o site'cie żyje teraz w `JiraTokenCacheHelper`, powiązana z kontem, nie jest ustawieniem użytkownika); pozostaje `JqlFilter`. |
| `src/FocusTray.Infrastructure/Jira/JiraService.cs` | `CreateJiraClient()` buduje klienta z Bearer auth providerem i base URL `https://api.atlassian.com/ex/jira/{cloudId}/` zamiast `{company}.atlassian.net`. |
| `src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs` | Usunięcie pól `Company`/`Email`/`ApiToken`; bezparametrowy `LoginAsync()` wywoływany po kliknięciu przycisku logowania, wzorem `TeamsLoginDialogViewModel`. |
| `src/FocusTray/Views/JiraLoginDialog.xaml(.cs)` | UI: jeden przycisk logowania, brak `ApiTokenPasswordBox`; wyświetlenie połączonego site'u (read-only) po zalogowaniu. |
| `src/FocusTray/Views/JiraAdvancedSettingsDialog.xaml(.cs)` | Zostaje pole JQL filter; usunięcie ew. odwołań do `Company`. |
| `docs/JIRA_INTEGRATION.md` | Pełna aktualizacja: rejestracja aplikacji w Atlassian Developer Console (typ: public client + PKCE, redirect URI, scope'y), usunięcie sekcji o API tokenach i rotacji tokenów, nowa sekcja o rotujących refresh tokenach i wylogowaniu. |
| `src/FocusTray/App.xaml.cs` | Rejestracja DI dla `JiraOAuthConfiguration` (singleton, analogicznie do `TeamsConfiguration` L52-58); usunięcie wiązania `JiraConfiguration.Company` z `SettingsService`. |
| `src/FocusTray.Infrastructure/FocusTray.Infrastructure.csproj` | Dodanie zależności NuGet `IdentityModel`. |
| `tests/FocusTray.Tests/Infrastructure/JiraServiceTests.cs` | Przepisanie pod Bearer auth + cloudId-based base URL. |
| `tests/FocusTray.IntegrationTests/Jira/JiraServiceIntegrationTests.cs` | Przepisanie pod nowy auth; patrz sekcja Testowanie poniżej. |

## Obsługa błędów

- **Odmowa/anulowanie logowania w przeglądarce** (`error=access_denied` na
  redirect) → komunikat w UI, stan pozostaje wylogowany.
- **Niezgodność `state`** na redirect → odrzucenie wymiany kodu, traktowane jak
  błąd logowania.
- **Konflikt portu loopback** (port zajęty) → czytelny komunikat błędu z sugestią
  zamknięcia aplikacji blokującej port; brak automatycznego retry na innym porcie
  w pierwszej iteracji (YAGNI — Teams też używa jednego stałego portu).
- **Wygaśnięcie/unieważnienie refresh tokena** (np. po 90 dniach nieużywania, lub
  odwołanie dostępu przez użytkownika w Atlassian) → `GetAccessTokenAsync()`
  zwraca błąd, `AuthStateChanged` przechodzi w stan wylogowany, UI wymusza pełny
  re-login (analogicznie do fallbacku Teams z silent auth na interactive).
- **`accessible-resources` zwraca 0 wyników** → błąd „Konto nie ma dostępu do
  żadnej instancji JIRA”, logowanie przerywane, tokeny nie są zapisywane.

## Migracja istniejących użytkowników

Przy pierwszym uruchomieniu po aktualizacji: `JiraAuthService` wykrywa istniejący
wpis `FocusTray_Jira` w `ICredentialService` (stary Basic Auth), usuwa go (sekret w
postaci API tokena nie powinien dłużej leżeć na dysku bez użycia) i ustawia stan
jako wylogowany — użytkownik zobaczy standardowy ekran logowania OAuth przy
następnej próbie użycia integracji JIRA. Dokumentacja (`JIRA_INTEGRATION.md`)
opisuje ten one-time re-login.

## Testowanie

Pełny interaktywny flow OAuth (przeglądarka + loopback) nie da się
zautomatyzować w CI — analogicznie do integracji Teams, która też nie ma testów
integracyjnych logowania. Zakres testów:

- **Testy jednostkowe**:
  - Generowanie PKCE (`code_verifier`/`code_challenge`, poprawność S256).
  - Serializacja/deserializacja `JiraTokenCacheHelper` (round-trip zapisu,
    odczytu, czyszczenia zaszyfrowanego pliku).
  - Logika `GetAccessTokenAsync()`: token ważny → brak refresh; token
    wygasły/w buforze 5 min → wywołanie refresh; zapis nowego rotującego refresh
    tokena.
  - Logika wyboru site'u: 1 wynik → auto-select; >1 wynik → wywołanie pickera;
    0 wyników → błąd.
  - Budowa base URL API z `cloudId`.
- **Testy integracyjne** (`JiraServiceIntegrationTests.cs`): przepisane pod
  Bearer token + cloudId-based URL, ze stubowanym/mockowanym tokenem (bez
  prawdziwego interaktywnego logowania).
- **Test manualny** (udokumentowany w `JIRA_INTEGRATION.md`): pełny flow
  logowania przez przeglądarkę, wylogowanie, wygaśnięcie i odświeżenie tokena,
  wybór site'u przy koncie z dostępem do wielu instancji.

## Poza zakresem (świadomie)

- Migracja klienta REST z API v2 na v3.
- Automatyczny retry loopback listenera na innym porcie przy konflikcie.
- Zachowanie Basic Auth jako fallbacku — pełne zastąpienie, zgodnie z decyzją.
