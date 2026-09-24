# Matador 🎲🇩🇰

En moderne .NET implementation af det klassiske danske brætspil **Matador**, komplet med en robust domænemotor, web-baseret multiplayer lobby og en interaktiv frontend.

---

## 🌟 Features

- **Matador.Core Engine**:
  - Komplet spilmotor med felter, grupper, skøder, huse/hoteller, fængsel og prøv lykken-kort.
  - Håndtering af køb, leje, pantsætning og fallit.
  - Støtte til byer/temaer (f.eks. København).
- **Multiplayer & Lobbies**:
  - Opret eller deltag i spilrum via rumkoder.
  - Vært/spiller-roller med sessionsstyring.
- **Web App**:
  - Minimal API backend bygget på .NET 9/ASP.NET Core.
  - Servérbar frontend via wwwroot med real-time spilinteraktion.
- **Console Demo**:
  - Hurtig CLI-udgave til at afprøve regler og simulere runder.
- **Unit Tests**:
  - Testsuite i `Matador.Core.Tests` til validering af spillets forretningslogik og regler.

---

## 🏗️ Projektstruktur

```plaintext
Matador/
├── src/
│   ├── Matador.Core/          # Domænemodel, regler, terninger, felter og spilmotor
│   ├── Matador.Web/           # ASP.NET Core API + statisk Web UI (wwwroot)
│   └── Matador.ConsoleDemo/   # Simpelt konsol-interface til demo og test
├── tests/
│   └── Matador.Core.Tests/    # Enhedstests til test af regler og motoren
├── infra/                     # Infrastruktur og deploymentscripts
└── Matador.slnx               # Løsningsfil
```

---

## 🚀 Kom i gang

### Forudsætninger
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) eller nyere

### Kør Web Applikationen

1. Naviger til Web-projektet:
   ```bash
   cd src/Matador.Web
   ```

2. Start applikationen:
   ```bash
   dotnet run
   ```

3. Åbn browseren på den angivne URL (typisk `http://localhost:5000` eller `https://localhost:5001`).

### Kør Konsol Demoen

```bash
dotnet run --project src/Matador.ConsoleDemo
```

### Kør Tests

```bash
dotnet test
```

---

## 📄 Licens

Dette projekt er til personlig/uddannelsesmæssig brug.
