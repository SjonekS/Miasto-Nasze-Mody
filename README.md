# Among Us Mod Manager — v2.0

Oficjalny, bezpieczny menedżer modyfikacji dla gry **Among Us** dedykowany paczce modów **Miasto Nasze Mira**.

Aplikacja typu **Single-File Portable** — działa od razu bez instalacji i bez konieczności instalowania zewnętrznych pakietów SDK .NET.

---

## 🌟 Główne Funkcje

- **Pobieranie i aktualizacja jednym kliknięciem**: Manager automatycznie sprawdza nowe wydania modyfikacji na GitHub Releases i pobiera właściwą paczkę.
- **Kapsuły Czasu (Niezależne Instancje)**: Możliwość zamrożenia i utworzenia osobnych kopii gry Among Us z modami. Aktualizacje gry na platformie Steam nie naruszą Twoich ulubionych wersji!
- **Zasada zerowego ryzyka dla plików gracza**:
  - Automatyczne tworzenie kopii zapasowej ZIP (`backups/`) przed każdą aktualizacją (rotacja max 2 kopii).
  - Zabezpieczenie konfiguracji modów (`BepInEx/config`) podczas resetowania plików.
  - Ochrona przed modyfikacją plików systemowych oraz zabezpieczenie przed Zip Slip.
- **Procedury Naprawcze (Troubleshoot)**:
  - *Szybkie czyszczenie folderu plugins* bez dotykania konfiguracji.
  - *Hard Clear plików modyfikacji* z zachowaniem ustawień.
  - *Pełny reset Among Us* z instrukcją czystej reinstalacji Steam.
- **Personalizacja**: 4 dopracowane motywy kolorystyczne (Steel Blue, Dark Amethyst, Emerald Forest, Crimson Red).
- **Interaktywny Visual Guide**: Wbudowany na dole okna przewodnik krok po kroku wyjaśniający działanie poszczególnych funkcji.

---

## 🚀 Pobieranie i Uruchamianie

1. Przejdź do zakładki [Releases](../../releases).
2. Pobierz plik `AmongUsModManager.exe`.
3. Uruchom program — aplikacja automatycznie wykryje Twoją instalację Among Us na Steam.

---

## 🛠️ Wymagania Techniczne

- **System operacyjny**: Windows 10 / 11 (64-bit)
- **Gra**: Among Us (Steam / wersja PC)
- **Brak zewnętrznych zależności**: Aplikacja zawiera w sobie środowisko .NET 10 Runtime (Self-Contained).

---

## 💻 Kompilacja ze źródeł

Wymagane .NET 10 SDK:

```bash
# Budowanie projektu
dotnet build AmongUsModManager.csproj -c Release

# Publikacja pojedynczego pliku wykonywalnego
dotnet publish AmongUsModManager.csproj -c Release -r win-x64 --self-contained true -o publish
```
