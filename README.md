# Vaihe 3: Service-kerros, Repository, Result Pattern ja API-dokumentaatio

Tervetuloa kolmanteen vaiheeseen. Tässä harjoituksessa **refaktoroit** `ProductApi`:n vaihe vaiheelta tuotantotasoiseksi sovellukseksi:

1. **Service-kerros** — Controller siivoaa itsensä ohueksi HTTP-kerrokseksi ja liiketoimintalogiikka siirtyy `ProductService`-luokkaan
2. **Repository-kerros** — Tietokantakoodi eristetään omaan kerrokseensa, jotta service ei tunne EF Corea
3. **Exception-käsittely** — Odottamattomat virheet lokitetaan ja käsitellään hallitusti
4. **Result Pattern** — `null` ja `bool` korvataan eksplisiittisillä `Result`-olioilla, jotka kertovat miksi operaatio epäonnistui
5. **API-dokumentaatio** — `ActionResult<T>` ja `[ProducesResponseType]` tekevät Swaggerista itseään dokumentoivan

Sovelluksen toiminta pysyy täysin samana — samat endpointit, sama data — mutta **rakenne** ja **laatu** paranevat merkittävästi.

---

## Lisämateriaali

**Teoriamateriaalit:**
- [Service-kerros ja DI](https://github.com/xamk-mire/Xamk-wiki/blob/main/C%23/fin/04-Advanced/WebAPI/Services-and-DI.md) - Miksi service, interface vs toteutus, elinkaaret
- [Dependency Injection](https://github.com/xamk-mire/Xamk-wiki/blob/main/C%23/fin/04-Advanced/Dependency-Injection.md) - DI:n teoria syvemmin
- [Repository Pattern](https://github.com/xamk-mire/Xamk-wiki/blob/main/C%23/fin/04-Advanced/Patterns/Repository-Pattern.md) - Tietokanta-abstraktion teoria, generic vs. spesifi repository
- [Result Pattern](https://github.com/xamk-mire/Xamk-wiki/blob/main/C%23/fin/04-Advanced/Patterns/Result-Pattern.md) - Virheenkäsittely Result-oliolla, Railway Oriented Programming

**Ulkoiset linkit:**
- [Microsoft: Dependency injection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)

---

## Mitä tarvitset?

Tämä harjoitus jatkaa suoraan **Vaihe 2: Tietokanta** -harjoituksesta. Sinulla pitää olla:

- `ProductApi` jossa on `AppDbContext` ja SQLite-tietokanta toiminnassa

> **Aloituskoodi saatavilla:** Kansiosta [`Starter code/ProductApi/`](Starter%20code/ProductApi/) löytyy valmis aloituskoodi, joka vastaa Vaihe 2 (Tietokanta) -harjoituksen lopputilannetta. Siinä on `ProductsController` ja `CategoriesController`, jotka käyttävät `AppDbContext`:ia suoraan, sekä DTO:t ja mapping-metodit.

### Ennen kuin aloitat

Varmista, että aloituskoodi toimii ennen kuin alat refaktoroimaan. Aja seuraavat komennot projektin juuressa:

**1. Luo tietokanta migraatioilla:**
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**2. Käynnistä sovellus:**
```bash
dotnet run
```

**3. Testaa Swaggerilla**, että kaikki endpointit toimivat (GET, POST, PUT, DELETE sekä Products- että Categories-endpointeille).

Kun kaikki toimii, voit sulkea sovelluksen ja aloittaa refaktoroinnin.

---

## Mitä rakennamme?

Sama API kuin ennen, mutta paremmin rakennettu:

```
Lähtötilanne (Vaihe 2: Tietokanta) — kaikki yhdessä controllerissa:
┌──────────────────────────────────────────────────┐
│  ProductsController                              │
│  - HTTP-parametrien käsittely (DTO:t)           │
│  - DTO → Entity -muunnokset (Mappings)          │
│  - Tietokantakyselyt (_context.Products...)     │
│  - Entity → Response -muunnokset                │
└──────────────────────────────────────────────────┘

Vaihe 1–6: Service-kerros — logiikka erilleen controllerista:
┌────────────────────────┐     ┌────────────────────────┐
│  ProductsController   │────►│  ProductService        │
│  - HTTP-käsittely     │     │  - DTO ↔ Entity        │
│  - Välittää DTO:t     │     │  - Logiikka            │
│  - Palauttaa DTO:t    │     │  - Tietokantakyselyt   │
└────────────────────────┘     └────────────────────────┘

Vaihe 7: Repository — tietokantakoodi erilleen servicestä:
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Controller      │───►│  Service         │───►│  Repository      │
│  HTTP-käsittely  │    │  DTO ↔ Entity    │    │  Tietokanta      │
│  Palauttaa DTO:t │    │  Logiikka        │    │  EF Core kyselyt │
└──────────────────┘    └──────────────────┘    └──────────────────┘

Vaihe 8–9: Exception-käsittely + Result Pattern:
  Service lokittaa virheet ja palauttaa Result-olioita throw:n ja null:n sijaan

Vaihe 10: API-dokumentaatio:
  Controller käyttää ActionResult<T> + [ProducesResponseType] → Swagger dokumentoi itsensä
```

---

# Ohjattu osio — ProductsController

Seuraavissa vaiheissa refaktoroimme `ProductsController`:n askel kerrallaan. Jokaisen vaiheen jälkeen sovellus toimii täysin samoin kuin ennen — varmista tämä testaamalla.

## Vaihe 1: Projektin rakenne

### Miksi tämä vaihe?

Tällä hetkellä kaikki koodi on `Controllers/`-kansiossa. Kun lisäämme service-kerroksen, logiikka tarvitsee oman paikkansa. `Services/`-kansio viestii kaikille kehittäjille: "liiketoimintalogiikka löytyy täältä, ei controllerista."

### Mitä tehdään?

**Luo `Services`-kansio** projektin juureen:

**Komentorivillä:**
```bash
mkdir Services
```

**Visual Studiolla:** Solution Explorerissa klikkaa projektia oikealla → Add → New Folder → `Services`.

Tavoitteena on seuraava rakenne:

```
ProductApi/
├── Controllers/
│   └── ProductsController.cs    ← HTTP-kerros (ohut)
├── Services/                    ← UUSI
│   ├── IProductService.cs       ← Interface (sopimus)
│   └── ProductService.cs        ← Toteutus (logiikka)
├── Mappings/
│   └── ProductMappings.cs       ← DTO ↔ Entity (jo olemassa)
├── Data/
│   └── AppDbContext.cs
├── Models/
│   ├── BaseEntity.cs
│   ├── Product.cs
│   └── Dtos/                    ← DTO:t (jo olemassa)
└── Program.cs
```

---

## Vaihe 2: Interface — sopimus

### Miksi tämä vaihe?

Interface määrittelee **mitä service osaa tehdä** ilman, että kertoo miten. Controller riippuu vain interfacesta, ei konkreettisesta toteutuksesta — tämä mahdollistaa testaamisen ja helpon vaihdon.

### Mitä tehdään?

**Luo `Services/IProductService.cs`:**

**Visual Studiolla:** Klikkaa `Services`-kansiota oikealla → Add → New Item → Interface → `IProductService`.

```csharp
using ProductApi.Models.Dtos;

namespace ProductApi.Services;

public interface IProductService
{
    Task<List<ProductResponse>> GetAllAsync();
    Task<ProductResponse?> GetByIdAsync(int id);
    Task<ProductResponse> CreateAsync(CreateProductRequest request);
    Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request);
    Task<bool> DeleteAsync(int id);
}
```

### Mitä tässä tapahtuu?

Interface on kuin "lupaus" — se kertoo mitä metodeja toteutuksessa täytyy olla. Jokainen metodi palauttaa `Task<...>`, koska operaatiot ovat asynkronisia.

Paluutyypit:
- `Task<List<ProductResponse>>` — asynkroninen lista tuotteista DTO-muodossa
- `Task<ProductResponse?>` — asynkroninen yksittäinen tuote tai `null`
- `Task<bool>` — asynkroninen totuusarvo (onnistuiko toiminto)

### Miksi service käsittelee DTO:ita — ei entiteettejä?

Huomaa, että interface **ottaa vastaan** Request-DTO:ita (`CreateProductRequest`, `UpdateProductRequest`) ja **palauttaa** Response-DTO:ita (`ProductResponse`). Controller ei koskaan näe `Product`-entiteettiä:

| Controller hoitaa | Service hoitaa |
|-------------------|----------------|
| HTTP-pyyntöjen vastaanotto | DTO → Entity -muunnos (sisään) |
| Statuskoodien valinta | Entity → Response DTO -muunnos (ulos) |
| Palauttaa servicen antaman DTO:n | Liiketoimintalogiikka + tietokantaoperaatiot |

**Miksi kaikki DTO-muunnokset kuuluvat serviceen?**

- **Controller ei tunne entiteettejä** — controllerin ei tarvitse tietää miten `Product` luodaan tai miltä se näyttää sisäisesti
- **Service omistaa koko muunnoksen** — se tietää miten DTO muunnetaan entiteetiksi (luonti, päivitys) ja miten entiteetti muunnetaan vastaukseksi
- **Testattavuus** — yksikkötesteissä voidaan testata koko ketju (DTO sisään → DTO ulos) ilman controlleria
- **Yksi muutospaikka** — jos entiteetin rakenne muuttuu, vain service ja mapping päivitetään — controller pysyy ennallaan

---

## Vaihe 3: Toteutus — ProductService

### Miksi tämä vaihe?

Nyt kirjoitetaan varsinainen koodi. `ProductService` toteuttaa `IProductService`-interfacen, ja kaikki tietokanta- ja muunnoskoodi siirtyy tänne controllerista. Tämän jälkeen controller voi käyttää vain interfacen metodeja.

### Mitä tehdään?

**Luo `Services/ProductService.cs`:**

**Visual Studiolla:** Klikkaa `Services`-kansiota oikealla → Add → Class → `ProductService`.

```csharp
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Mappings;
using ProductApi.Models;
using ProductApi.Models.Dtos;

namespace ProductApi.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductResponse>> GetAllAsync()
    {
        List<Product> products = await _context.Products.ToListAsync();
        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        Product? product = await _context.Products.FindAsync(id);
        return product?.ToResponse();
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        Product product = request.ToEntity();

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product.ToResponse();
    }

    public async Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request)
    {
        Product? existing = await _context.Products.FindAsync(id);

        if (existing == null)
            return null;

        request.UpdateEntity(existing);
        await _context.SaveChangesAsync();
        return existing.ToResponse();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        Product? product = await _context.Products.FindAsync(id);

        if (product == null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }
}
```

### Mitä tässä tapahtuu?

- `ProductService : IProductService` — luokka toteuttaa interfacen (pitää toteuttaa kaikki interfacen metodit)
- `AppDbContext _context` — injektoidaan konstruktorin kautta, aivan kuten controlleriin aiemmin
- `CreateAsync` — ottaa `CreateProductRequest`:n, muuntaa entiteetiksi (`request.ToEntity()`), tallentaa ja **palauttaa `ProductResponse`:n**
- `UpdateAsync` — ottaa `UpdateProductRequest`:n, päivittää kentät (`request.UpdateEntity(existing)`) ja **palauttaa `ProductResponse`:n**
- `GetAllAsync` / `GetByIdAsync` — hakee entiteetit tietokannasta ja **muuntaa ne `ProductResponse`:ksi** ennen palautusta
- **Kaikki DTO ↔ Entity -muunnokset tapahtuvat servicessä** — controller ei koskaan näe `Product`-entiteettiä

---

## Vaihe 4: Rekisteröinti Program.cs:ssä

### Miksi tämä vaihe?

Olemme luoneet `IProductService`-interfacen ja `ProductService`-toteutuksen, mutta sovellus ei vielä tiedä niistä mitään. Miten controller saa `ProductService`:n käyttöönsä?

Tässä kohtaa tulee kuvaan **DI-kontti** (Dependency Injection Container).

### Mikä on DI-kontti?

DI-kontti on ASP.NET Coren sisäänrakennettu "palvelurekisteri", joka:

1. **Tietää mitä palveluita on olemassa** — koska rekisteröimme ne `Program.cs`:ssä
2. **Osaa luoda palveluita automaattisesti** — kun jokin luokka pyytää palvelua konstruktorissa
3. **Hallitsee palveluiden elinkaaren** — luo, jakaa ja hävittää instanssit oikea-aikaisesti

```
Ilman DI-konttia (manuaalinen luonti):
AppDbContext context = new AppDbContext(options);      // Sinun pitää luoda itse
ProductService service = new ProductService(context);   // Sinun pitää tietää riippuvuudet
ProductsController controller = new ProductsController(service); // Sinun pitää ketjuttaa kaikki

DI-kontin kanssa (automaattinen):
builder.Services.AddScoped<IProductService, ProductService>();
// DI-kontti hoitaa kaiken — se tietää mitä ProductService tarvitsee
// ja luo koko ketjun automaattisesti
```

DI-kontti toimii kuin **reseptikirja**: rekisteröinnit ovat reseptejä, ja kontti osaa valmistaa oikean palvelun tarvittaessa.

### Mitä tehdään?

Lisää `Program.cs`:ään seuraava rivi `AddDbContext`-rivin jälkeen:

```csharp
using ProductApi.Data;
using ProductApi.Services;

// ...

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Lisää tämä rivi:
builder.Services.AddScoped<IProductService, ProductService>();
```

### Mitä tämä rivi tekee?

`AddScoped<IProductService, ProductService>()` kertoo DI-kontille:

- **"Kun joku pyytää `IProductService`:ä"** → anna `ProductService`-instanssi
- **"Luo uusi instanssi jokaiselle HTTP-pyynnölle"** → `Scoped`-elinkaari

Ilman tätä riviä sovellus kaatuu käynnistyessä seuraavaan virheeseen:

```
InvalidOperationException: Unable to resolve service for type 'IProductService'
```

### Miten DI-kontti luo palvelun?

Kun HTTP-pyyntö saapuu ja controller tarvitsee `IProductService`:ä, DI-kontti tekee seuraavaa:

```
1. Controller pyytää IProductService:ä konstruktorissa
2. DI-kontti katsoo reseptikirjasta: "IProductService → ProductService"
3. DI-kontti tarkistaa: "ProductService tarvitsee AppDbContext:n konstruktorissa"
4. DI-kontti katsoo: "AppDbContext on jo rekisteröity AddDbContext:lla"
5. DI-kontti luo AppDbContext:n
6. DI-kontti luo ProductService:n ja antaa sille AppDbContext:n
7. DI-kontti antaa valmiin ProductService:n controllerille
```

Kaikki tapahtuu **automaattisesti** — sinun tarvitsee vain rekisteröidä palvelut.

### DI-elinkaaret vertailussa

ASP.NET Core tarjoaa kolme elinkaarta. Valinta riippuu siitä, miten palvelua käytetään:

| Elinkaari | Luodaan | Sopii kun... |
|-----------|---------|--------------|
| `AddTransient` | Joka kerta kun pyydetään | Kevyet, tilattomat palvelut (esim. laskuri, validaattori) |
| `AddScoped` | Kerran per HTTP-pyyntö | Tietokantapalvelut — sama `DbContext` koko pyynnön ajan |
| `AddSingleton` | Kerran koko sovelluksen elinaikana | Konfiguraatio, välimuisti, HttpClient-tehdas |

Miksi `AddScoped` on oikea valinta `ProductService`:lle?

- `AppDbContext` rekisteröidään oletuksena **Scoped**-elinkaarella
- Jos `ProductService` olisi `Singleton`, se yrittäisi käyttää jo hävitettyä `DbContext`:ia seuraavissa pyynnöissä
- Jos se olisi `Transient`, jokainen injektio loisi uuden instanssin — turhaa kun yksi riittää per pyyntö

**Nyrkkisääntö:** Jos palvelu käyttää `DbContext`:ia, rekisteröi se `AddScoped`:lla.

---

## Vaihe 5: Controllerin päivittäminen

### Miksi tämä vaihe?

Nyt controller voi luopua kaikesta tietokanta- ja muunnoskoodista. Se pyytää `IProductService`:n DI-kontilta, välittää Request-DTO:t serviceen ja palauttaa servicen antamat Response-DTO:t suoraan. Controller ei tiedä `Product`-entiteetistä mitään.

### Mitä tehdään?

Korvaa koko `Controllers/ProductsController.cs` sisältö:

```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ProductApi.Models.Dtos;
using ProductApi.Services;

namespace ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        List<ProductResponse> products = await _service.GetAllAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        ProductResponse? product = await _service.GetByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        ProductResponse created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request)
    {
        ProductResponse? updated = await _service.UpdateAsync(id, request);

        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        bool deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
```

### Miten controller saa servicen DI-kontista?

Katsotaan konstruktoria tarkemmin:

```csharp
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }
}
```

Tämä on **konstruktori-injektio** (constructor injection) — DI:n yleisin muoto:

1. Controller **ei luo** `ProductService`:ä itse (`new ProductService(...)`)
2. Sen sijaan se **pyytää** `IProductService`:ä konstruktoriparametrissa
3. **DI-kontti tunnistaa** pyynnön automaattisesti ja antaa oikean instanssin
4. Controller **tallentaa** sen `_service`-kenttään ja käyttää sitä metodeissa

```
Mitä tapahtuu kun HTTP-pyyntö saapuu:

GET /api/products
    ↓
ASP.NET Core luo ProductsController:n
    ↓
DI-kontti huomaa: "Konstruktori pyytää IProductService:ä"
    ↓
DI-kontti katsoo rekisteröinnin: IProductService → ProductService (Scoped)
    ↓
DI-kontti luo ProductService:n (ja sen riippuvuudet)
    ↓
ProductsController saa valmiin IProductService:n
    ↓
Controller kutsuu _service.GetAllAsync()
```

**Tärkeää:** Controller pyytää `IProductService`:ä (interface), ei `ProductService`:ä (luokka). Se ei tiedä eikä välitä mikä konkreettinen toteutus on taustalla — tämä on Dependency Inversion -periaate käytännössä.

### Mitä tässä tapahtuu?

- Controller **vastaanottaa** Request-DTO:ita HTTP-pyynnöistä (`CreateProductRequest`, `UpdateProductRequest`)
- Controller **välittää** ne suoraan serviceen
- Service **palauttaa** Response-DTO:ita (`ProductResponse`) — controller palauttaa ne sellaisenaan
- **Ei tietokantakoodia, ei muunnoskoodia, ei `Mappings`-riippuvuutta** — controller on puhdas HTTP-kerros

### Vertailu ennen ja jälkeen

| Ennen (Vaihe 2) | Nyt (Vaihe 5) |
|------------------|---------------|
| `private readonly AppDbContext _context` | `private readonly IProductService _service` |
| `request.ToEntity()` controllerissa | Service hoitaa muunnoksen |
| `product.ToResponse()` controllerissa | Service palauttaa valmiin DTO:n |
| `using ProductApi.Mappings` | Ei tarvita — controller ei tunne mappingia |
| `_context.Products.ToListAsync()` | `_service.GetAllAsync()` |
| Controller tuntee Entity + DTO + Mapping | Controller tuntee vain DTO:t |

---

## Vaihe 6: Testaaminen

Käynnistä sovellus ja testaa Swaggerilla. Kaiken pitäisi toimia täysin samoin kuin ennen — vain rakenne on parempi.

**Komentorivillä:**
```bash
dotnet run
```

**Visual Studiolla:** Paina F5.

Testaa kaikki viisi endpointtia kuten aiemminkin. Jos kaikki toimii, refaktorointi onnistui.

### Mitä saavutimme vaiheissa 1–6?

Controllerista siirtyi pois kaikki tietokanta- ja muunnoskoodi. Se on nyt ohut HTTP-kerros, joka vain välittää DTO:t serviceen ja palauttaa vastaukset. Tämä on merkittävä parannus, mutta seuraavat ongelmat ovat vielä jäljellä:

| Ongelma | Vaihe jossa ratkaistaan |
|---------|------------------------|
| Service käyttää `AppDbContext`:ia suoraan — testaus on vaikeaa | **Vaihe 7** (Repository) |
| Tietokantavirhe kaataa sovelluksen hallitsemattomasti | **Vaihe 8** (Exception-käsittely) |
| `null` ja `bool` eivät kerro miksi operaatio epäonnistui | **Vaihe 9** (Result Pattern) |
| Swagger ei tiedä mitä endpoint palauttaa | **Vaihe 10** (API-dokumentaatio) |

---

## Vaihe 7: Repository-kerros

### Miksi tämä vaihe?

`ProductService` toimii hyvin, mutta sillä on edelleen ongelma: se käyttää `AppDbContext`:ia suoraan. Tämä tarkoittaa, että:

- **Tight coupling** — Service tuntee EF Core:n ja tietokantateknologian
- **Ei testattavissa** — Servicen yksikkötestaus vaatii oikean tietokannan tai InMemory-konfiguraation
- **Tietokantakyselyt sekoittuvat logiikkaan** — Service sisältää sekä liiketoimintalogiikkaa että tietokantakutsuja

Repository Pattern ratkaisee tämän: tietokantakoodi siirtyy omaan kerrokseen, ja service käyttää vain rajapintaa.

```
Ennen (Vaihe 6):
Controller → Service → AppDbContext (suoraan)

Nyt (Vaihe 7):
Controller → Service → IProductRepository → ProductRepository → AppDbContext
```

### 7.1 Kansiorakenne

**Luo `Repositories`-kansio** projektin juureen:

```bash
mkdir Repositories
```

Tavoiterakenne:

```
ProductApi/
├── Controllers/
│   └── ProductsController.cs
├── Services/
│   ├── IProductService.cs
│   └── ProductService.cs
├── Repositories/              ← UUSI
│   ├── IProductRepository.cs  ← Interface
│   └── ProductRepository.cs   ← Toteutus
├── Mappings/
│   └── ProductMappings.cs
├── Data/
│   └── AppDbContext.cs
├── Models/
│   ├── BaseEntity.cs
│   ├── Product.cs
│   └── Dtos/
└── Program.cs
```

### 7.2 Repository-interface

**Luo `Repositories/IProductRepository.cs`:**

```csharp
using ProductApi.Models;

namespace ProductApi.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<Product> AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task<bool> DeleteAsync(int id);
}
```

### Mitä tässä tapahtuu?

Interface kuvaa **mitä tietokantaoperaatioita** on saatavilla — ei sitä, miten ne toteutetaan. Huomaa ero service-interfaceen:

| IProductService | IProductRepository |
|-----------------|--------------------|
| Ottaa **DTO:ita** (`CreateProductRequest`) | Ottaa **entiteettejä** (`Product`) |
| Palauttaa **DTO:ita** (`ProductResponse`) | Palauttaa **entiteettejä** (`Product`) |
| DTO ↔ Entity -muunnos + liiketoimintalogiikka | Vain tietokannan CRUD-operaatiot |

### 7.3 Repository-toteutus

**Luo `Repositories/ProductRepository.cs`:**

```csharp
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _context.Products.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products.FindAsync(id);
    }

    public async Task<Product> AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        Product? product = await _context.Products.FindAsync(id);

        if (product == null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }
}
```

### Mitä tässä tapahtuu?

- Tietokantakoodi on **täsmälleen sama** kuin ennen `ProductService`:ssä — se on vain siirretty tänne
- `AppDbContext` injektoidaan konstruktorin kautta
- Repository vastaa **vain** tietokannan käsittelystä — ei validointia, ei liiketoimintasääntöjä

### 7.4 ProductServicen päivittäminen

Nyt `ProductService` ei enää tarvitse `AppDbContext`:ia. Se käyttää `IProductRepository`:a:

**Päivitä `Services/ProductService.cs`:**

```csharp
using System.Collections.Generic;
using ProductApi.Mappings;
using ProductApi.Models;
using ProductApi.Models.Dtos;
using ProductApi.Repositories;

namespace ProductApi.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ProductResponse>> GetAllAsync()
    {
        List<Product> products = await _repository.GetAllAsync();
        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        Product? product = await _repository.GetByIdAsync(id);
        return product?.ToResponse();
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        Product product = request.ToEntity();
        Product created = await _repository.AddAsync(product);
        return created.ToResponse();
    }

    public async Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request)
    {
        Product? existing = await _repository.GetByIdAsync(id);

        if (existing == null)
            return null;

        request.UpdateEntity(existing);
        await _repository.UpdateAsync(existing);
        return existing.ToResponse();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}
```

### Mitä muuttui?

| Ennen | Nyt |
|-------|-----|
| `private readonly AppDbContext _context` | `private readonly IProductRepository _repository` |
| `_context.Products.ToListAsync()` | `_repository.GetAllAsync()` |
| `_context.Products.Add(product)` + `SaveChangesAsync()` | `_repository.AddAsync(product)` |
| Service tuntee EF Core:n | Service tuntee vain rajapinnan |

**Tärkeää:** `ProductService` ei enää tarvitse `using Microsoft.EntityFrameworkCore` -lausetta! Se käyttää `ProductApi.Models`-nimiavaruutta vain muunnoksissa (`ToEntity`, `ToResponse`); tietokantakyselyt ovat repositoryssa.

**Huomaa:** `ProductsController` ei muuttunut lainkaan! Se käyttää edelleen `IProductService`:ä eikä tiedä eikä välitä, miten service hakee datansa. Tämä on rajapintojen (interface) voima — sisäinen toteutus voi muuttua ilman, että kutsuja tarvitsee päivittää.

### 7.5 Rekisteröinti Program.cs:ssä

Lisää `Program.cs`:ään repository-rekisteröinti:

```csharp
using ProductApi.Repositories;

// ... muiden rekisteröintien jälkeen:

builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

`Program.cs` näyttää nyt tältä:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
```

DI-kontti yhdistää ketjun automaattisesti:

```
ProductsController pyytää IProductService
  → DI antaa ProductService:n
    → ProductService pyytää IProductRepository
      → DI antaa ProductRepository:n
        → ProductRepository pyytää AppDbContext
          → DI antaa AppDbContext:n
```

### 7.6 Testaaminen

Käynnistä sovellus ja testaa Swaggerilla. Kaiken pitäisi toimia täysin samoin — olemme vain erottaneet tietokantakoodin omaan kerrokseen.

---

## Vaihe 8: Exception-käsittely servicessä

### Miksi tämä vaihe?

Tällä hetkellä jos tietokantaoperaatio epäonnistuu (esim. tietokanta ei ole saatavilla, uniikki kenttä loukkaa rajoitetta), sovellus kaatuu hallitsemattomasti ja asiakas saa raa'an 500-virheen teknisillä yksityiskohdilla.

```
Mitä nyt tapahtuu tietokantavirheessä:

Client ← 500 Internal Server Error + "SqliteException: ..." (vuotaa teknisiä yksityiskohtia!)
```

Servicen pitää käsitellä odottamattomat virheet hallitusti ja lokittaa ne.

### 8.1 ILogger — mikä se on ja miten sitä käytetään?

**Mikä on `ILogger`?**

`ILogger` on ASP.NET Coren sisäänrakennettu lokitusrajapinta. Se mahdollistaa viestien kirjoittamisen sovelluksen lokiin — konsoliin, tiedostoon tai ulkoiseen lokipalveluun. Ajattele sitä kuin sovelluksen "päiväkirjana", johon kirjataan mitä tapahtui ja milloin.

**Lokitasot (Log Levels):**

Jokainen lokiviesti kirjoitetaan tietyllä vakavuustasolla:

| Taso | Metodi | Milloin käytetään |
|------|--------|-------------------|
| `Trace` | `_logger.LogTrace(...)` | Hyvin yksityiskohtainen debug-tieto (ei tuotantoon) |
| `Debug` | `_logger.LogDebug(...)` | Kehitysaikainen debug-tieto |
| `Information` | `_logger.LogInformation(...)` | Normaali toiminta ("Tuote luotu", "Käyttäjä kirjautui") |
| `Warning` | `_logger.LogWarning(...)` | Odottamaton tilanne, joka ei ole virhe ("Cache tyhjä") |
| `Error` | `_logger.LogError(...)` | Virhe, joka estää yksittäisen operaation ("Tietokantavirhe") |
| `Critical` | `_logger.LogCritical(...)` | Vakava virhe, sovellus ei voi jatkaa |

**Miltä lokit näyttävät?**

Oletuksena lokit tulostuvat **konsoliin** (terminaaliin) kun ajaät `dotnet run`:

```
info: ProductApi.Services.ProductService[0]
      Tuote luotu: Kahvikuppi (Id: 5)
warn: ProductApi.Services.ProductService[0]
      Tuotetta 999 ei löytynyt
error: ProductApi.Services.ProductService[0]
      Virhe tuotteen luomisessa: Kahvikuppi
      Microsoft.Data.Sqlite.SqliteException: SQLite Error 19: UNIQUE constraint failed
         at Microsoft.Data.Sqlite.SqliteException...
```

Huomaa, miten jokaisen lokiviestin alussa näkyy **taso** (`info`, `warn`, `error`) ja **luokan nimi** (`ProductApi.Services.ProductService`). Tämä kertoo **missä** viesti kirjoitettiin — siksi käytämme `ILogger<ProductService>` eikä pelkkää `ILogger`.

**Mistä lokit näkee?**

| Ympäristö | Missä lokit näkyvät |
|-----------|---------------------|
| Kehitys (`dotnet run`) | **Terminaali/konsoli** — lokit tulostuvat suoraan |
| Visual Studio (F5) | **Output-ikkuna** → "ASP.NET Core Web Server" |
| Tuotanto (Azure App Service) | **Azure Portal** → App Service → Log stream |
| Docker | `docker logs <container-name>` |

> **Vinkki:** Tuotantoympäristössä lokitus on usein ainoa tapa selvittää miksi jokin meni pieleen — et voi laittaa breakpointia tuotantopalvelimelle. Siksi hyvä lokitus on kriittistä.

**Lisää `ILogger` `ProductService`:n konstruktoriin.** Ainoa muutos aiempaan on korostettu:

```csharp
public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductService> _logger;       // ← UUSI

    public ProductService(
        IProductRepository repository,
        ILogger<ProductService> logger)                     // ← UUSI
    {
        _repository = repository;
        _logger = logger;                                   // ← UUSI
    }

    // ... metodit pysyvät ennallaan toistaiseksi
}
```

`ILogger<ProductService>` rekisteröityy DI-konttiin automaattisesti — ei tarvita erillistä `AddScoped`-kutsua. ASP.NET Core tarjoaa `ILogger<T>`:n sisäänrakennetusti kaikille luokille.

### 8.2 Try-catch repository-kutsuihin

Lisää exception-käsittely `CreateAsync`-metodiin:

```csharp
public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
{
    try
    {
        Product product = request.ToEntity();
        Product created = await _repository.AddAsync(product);
        return created.ToResponse();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Virhe tuotteen luomisessa: {ProductName}", request.Name);
        throw;
    }
}
```

### Mitä tässä tapahtuu?

1. **`try`** — Yritetään normaali operaatio
2. **`catch (Exception ex)`** — Jos jokin menee pieleen (tietokantavirhe, yhteysvirhe, jne.)
3. **`_logger.LogError(...)`** — Lokitetaan virhe yksityiskohtineen (kehittäjälle/ylläpidolle)
4. **`throw;`** — Heitetään exception eteenpäin (ASP.NET Coren middleware käsittelee sen)

### Milloin throw vs. käsittele?

| Virhetyyppi | Esimerkki | Mitä tehdään? |
|-------------|-----------|---------------|
| **Odottamaton** (infra) | Tietokanta alhaalla, yhteys katkennut | `_logger.LogError` + `throw;` |
| **Odotettu** (logiikka) | Tuotetta ei löydy, validointi epäonnistuu | Käsittele itse (palauta `null`, `false`, tai Result) |

**Nyrkkisääntö:** Lokita ja heitä eteenpäin odottamattomat virheet. Käsittele odotetut virheet palauttamalla sopiva arvo.

### 8.3 Koko ProductService exception-käsittelyllä

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProductApi.Mappings;
using ProductApi.Models;
using ProductApi.Models.Dtos;
using ProductApi.Repositories;

namespace ProductApi.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repository, ILogger<ProductService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<ProductResponse>> GetAllAsync()
    {
        try
        {
            List<Product> products = await _repository.GetAllAsync();
            return products.Select(p => p.ToResponse()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteiden haussa");
            throw;
        }
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        try
        {
            Product? product = await _repository.GetByIdAsync(id);
            return product?.ToResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen haussa: {ProductId}", id);
            throw;
        }
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        try
        {
            Product product = request.ToEntity();
            Product created = await _repository.AddAsync(product);
            return created.ToResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen luomisessa: {ProductName}", request.Name);
            throw;
        }
    }

    public async Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request)
    {
        try
        {
            Product? existing = await _repository.GetByIdAsync(id);

            if (existing == null)
                return null;

            request.UpdateEntity(existing);
            await _repository.UpdateAsync(existing);
            return existing.ToResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen päivittämisessä: {ProductId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            return await _repository.DeleteAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen poistamisessa: {ProductId}", id);
            throw;
        }
    }
}
```

### 8.4 Testaaminen

Käynnistä sovellus ja testaa normaalit operaatiot — kaiken pitäisi toimia kuten ennen. Exception-käsittely aktivoituu vasta kun jotain menee oikeasti pieleen (esim. tietokanta on lukittu tai poissa).

> **Huomaa:** Vaihe 8:ssa käytimme `throw;`-sanaa heittämään exception eteenpäin — ASP.NET Coren middleware palauttaa asiakkaalle geneerisen 500-virheen. Seuraavassa vaiheessa korvaamme tämän `Result.Failure`:lla, jotta service voi palauttaa **hallitun virheviestin** ilman, että sovellus kaatuu.

---

## Vaihe 9: Result Pattern

### Miksi tämä vaihe?

Katsotaan nykyisiä paluutyyppejä ja miten controller käyttää niitä:

```csharp
// Servicen nykyiset paluutyypit:
Task<ProductResponse?> GetByIdAsync(int id);
Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest req);
Task<bool> DeleteAsync(int id);
```

Miten controller käyttää näitä?

```csharp
ProductResponse? product = await _service.GetByIdAsync(id);

if (product == null)
    return NotFound();  // Mutta onko null oikeasti "ei löydy"?
                        // Entä jos null tarkoittaa tietokantavirhettä?
                        // Entä jos null tarkoittaa "ei oikeutta nähdä"?
                        // Controller EI VOI tietää — se näkee vain null:n
```

**Ongelma konkreettisesti:** `null` ja `bool` ovat "yksibittisiä" vastauksia — ne kertovat vain "ei onnistunut", mutta eivät **miksi** ei onnistunut. Controller joutuu **olettamaan** syyn:

| Paluuarvo | Controllerin oletus | Mutta todellisuudessa? |
|-----------|---------------------|------------------------|
| `null` (`GetById`) | "Tuotetta ei löydy" → `404` | Ehkä tietokantavirhe → pitäisi olla `500` |
| `null` (`Update`) | "Tuotetta ei löydy" → `404` | Ehkä validointi epäonnistui → pitäisi olla `400` |
| `false` (`Delete`) | "Tuotetta ei löydy" → `404` | Ehkä ei oikeuksia → pitäisi olla `403` |

Controller palauttaa **väärän statuskoodin**, koska se ei tiedä epäonnistumisen syytä. Asiakas saa `404 Not Found` vaikka oikea vastaus olisi `500 Internal Server Error`.

**Result Pattern** ratkaisee tämän: jokainen operaatio palauttaa `Result`-olion, joka kertoo **eksplisiittisesti** onnistuiko operaatio ja mikä meni vikaan.

```csharp
// Ennen: Controller arvaa
ProductResponse? product = await _service.GetByIdAsync(id);
return product == null ? NotFound() : Ok(product);  // Aina 404 — oikein vai väärin?

// Jälkeen: Result kertoo suoraan syyn
Result<ProductResponse> result = await _service.GetByIdAsync(id);
if (result.IsFailure)
    return NotFound(new { error = result.Error });   // "Tuotetta 5 ei löytynyt"
return Ok(result.Value);
```

### 9.1 Result-luokka

**Luo `Common/Result.cs`:**

```csharp
namespace ProductApi.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new Result(true, string.Empty);

    public static Result Failure(string error)
        => new Result(false, error);

    public static Result<T> Success<T>(T value)
        => Result<T>.Success(value);

    public static Result<T> Failure<T>(string error)
        => Result<T>.Failure(error);
}

public class Result<T> : Result
{
    public T Value { get; }

    private Result(bool isSuccess, T value, string error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value)
        => new Result<T>(true, value, string.Empty);

    public new static Result<T> Failure(string error)
        => new Result<T>(false, default!, error);
}
```

### Mitä tässä tapahtuu?

- **`Result`** — Ilman palautusarvoa (esim. Delete: onnistui tai epäonnistui)
- **`Result<T>`** — Palautusarvon kanssa (esim. GetById: tuote tai virhe)
- **`IsSuccess` / `IsFailure`** — Onnistuiko operaatio?
- **`Value`** — Onnistuneen operaation tulos
- **`Error`** — Virheilmoitus epäonnistuneelle operaatiolle
- **Factory-metodit** — `Result.Success(value)` ja `Result.Failure("virheviesti")`

### 9.2 IProductServicen päivittäminen

Päivitä paluutyypit käyttämään `Result`:ia:

```csharp
using ProductApi.Common;
using ProductApi.Models.Dtos;

namespace ProductApi.Services;

public interface IProductService
{
    Task<Result<List<ProductResponse>>> GetAllAsync();
    Task<Result<ProductResponse>> GetByIdAsync(int id);
    Task<Result<ProductResponse>> CreateAsync(CreateProductRequest request);
    Task<Result<ProductResponse>> UpdateAsync(int id, UpdateProductRequest request);
    Task<Result> DeleteAsync(int id);
}
```

**Vertailu:**

| Ennen | Nyt | Miksi parempi? |
|-------|-----|----------------|
| `Task<List<ProductResponse>>` | `Task<Result<List<ProductResponse>>>` | Voi kertoa virheestä |
| `Task<ProductResponse?>` | `Task<Result<ProductResponse>>` | Ei tarvitse arvata miksi `null` |
| `Task<bool>` | `Task<Result>` | Virheviesti mukana |

### 9.3 ProductServicen päivittäminen

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProductApi.Common;
using ProductApi.Mappings;
using ProductApi.Models;
using ProductApi.Models.Dtos;
using ProductApi.Repositories;

namespace ProductApi.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repository, ILogger<ProductService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<List<ProductResponse>>> GetAllAsync()
    {
        try
        {
            List<Product> products = await _repository.GetAllAsync();
            List<ProductResponse> response = products.Select(p => p.ToResponse()).ToList();
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteiden haussa");
            return Result.Failure<List<ProductResponse>>("Tuotteiden haku epäonnistui");
        }
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(int id)
    {
        try
        {
            Product? product = await _repository.GetByIdAsync(id);

            if (product == null)
                return Result.Failure<ProductResponse>($"Tuotetta {id} ei löytynyt");

            return Result.Success(product.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen haussa: {ProductId}", id);
            return Result.Failure<ProductResponse>("Tuotteen haku epäonnistui");
        }
    }

    public async Task<Result<ProductResponse>> CreateAsync(CreateProductRequest request)
    {
        try
        {
            Product product = request.ToEntity();
            Product created = await _repository.AddAsync(product);
            return Result.Success(created.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen luomisessa: {ProductName}", request.Name);
            return Result.Failure<ProductResponse>("Tuotteen luominen epäonnistui");
        }
    }

    public async Task<Result<ProductResponse>> UpdateAsync(int id, UpdateProductRequest request)
    {
        try
        {
            Product? existing = await _repository.GetByIdAsync(id);

            if (existing == null)
                return Result.Failure<ProductResponse>($"Tuotetta {id} ei löytynyt");

            request.UpdateEntity(existing);
            await _repository.UpdateAsync(existing);
            return Result.Success(existing.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen päivittämisessä: {ProductId}", id);
            return Result.Failure<ProductResponse>("Tuotteen päivittäminen epäonnistui");
        }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        try
        {
            bool deleted = await _repository.DeleteAsync(id);

            if (!deleted)
                return Result.Failure($"Tuotetta {id} ei löytynyt");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tuotteen poistamisessa: {ProductId}", id);
            return Result.Failure("Tuotteen poistaminen epäonnistui");
        }
    }
}
```

### Mitä muuttui?

Huomaa miten virheenkäsittely yhdistyy Result Patterniin:

| Tilanne | Ennen | Nyt |
|---------|-------|-----|
| Tuotetta ei löydy | `return null;` | `return Result.Failure<ProductResponse>("...");` |
| Poisto epäonnistui | `return false;` | `return Result.Failure("...");` |
| Tietokantavirhe | `throw;` (kaataa) | `return Result.Failure("...epäonnistui");` (hallittu) |
| Onnistunut haku | `return product.ToResponse();` | `return Result.Success(product.ToResponse());` |

**Tärkeä ero Vaihe 8:aan:** Nyt tietokantavirheet eivät enää kaada sovellusta — ne palautetaan `Result.Failure`:na.

### 9.4 Controllerin päivittäminen

Korvaa koko `Controllers/ProductsController.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ProductApi.Common;
using ProductApi.Models.Dtos;
using ProductApi.Services;

namespace ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        Result<List<ProductResponse>> result = await _service.GetAllAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        Result<ProductResponse> result = await _service.GetByIdAsync(id);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        Result<ProductResponse> result = await _service.CreateAsync(request);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request)
    {
        Result<ProductResponse> result = await _service.UpdateAsync(id, request);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        Result result = await _service.DeleteAsync(id);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
```

### Vertailu ennen ja jälkeen

| Ennen (Vaihe 5) | Nyt (Vaihe 9) |
|------------------|---------------|
| `ProductResponse? product = await _service.GetByIdAsync(id);` | `Result<ProductResponse> result = await _service.GetByIdAsync(id);` |
| `if (product == null) return NotFound();` | `if (result.IsFailure) return NotFound(new { error = result.Error });` |
| `return Ok(product);` | `return Ok(result.Value);` |
| Ei virheviestiä asiakkaalle | Selkeä virheviesti JSON-muodossa |
| Arvaaminen: miksi `null`? | Eksplisiittinen: `result.Error` kertoo |

### 9.5 Testaaminen

Käynnistä sovellus ja testaa:

1. **GET /api/products** — Pitäisi palauttaa lista kuten ennen
2. **GET /api/products/999** — Nyt palauttaa `404` + `{ "error": "Tuotetta 999 ei löytynyt" }`
3. **POST /api/products** — Luo tuote normaalisti
4. **DELETE /api/products/999** — Palauttaa `404` + virheviestin

**Huomaa ero:** Aiemmin `GET /api/products/999` palautti pelkän `404`. Nyt se palauttaa myös virheviestin!

> **Lisätehtävä:** Tutustu [Result Pattern -teoriamateriaaliin](https://github.com/xamk-mire/Xamk-wiki/blob/main/C%23/fin/04-Advanced/Patterns/Result-Pattern.md) ja `ErrorType`-enumiin, jolla voit erotella virhetyypit tarkemmin (NotFound, Validation, Conflict).

---

## Vaihe 10: Controllerin tyyppiturvallisuus ja API-dokumentaatio

### Miksi tämä vaihe?

Katsotaan nykyistä controlleria:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id)
```

**Ongelma:** `IActionResult` on täysin tyypitön — kääntäjä ei tiedä mitä endpoint palauttaa, eikä Swagger pysty generoimaan response-schemaa automaattisesti.

**Ratkaisu:** `ActionResult<T>` + `[ProducesResponseType]` -attribuutit tekevät API:sta itseään dokumentoivan:

```csharp
[HttpGet("{id}")]
[ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<ProductResponse>> GetById(int id)
```

### Mitä hyötyä tästä on?

| `IActionResult` | `ActionResult<T>` + attribuutit |
|------------------|---------------------------------|
| Tyypitön — mikä tahansa voi tulla ulos | Kääntäjä tietää palautustyypin |
| Swagger näyttää vain "200 OK" | Swagger generoi oikean response-scheman |
| Tiimin pitää lukea koodia ymmärtääkseen API:a | Attribuutit dokumentoivat kaikki statuskoodit |
| Ei varoita virheistä käännösaikana | Kääntäjä varoittaa jos palautustyyppi ei täsmää |

### 10.1 Controllerin päivittäminen

Korvaa koko `Controllers/ProductsController.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ProductApi.Common;
using ProductApi.Models.Dtos;
using ProductApi.Services;

namespace ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ProductResponse>>> GetAll()
    {
        Result<List<ProductResponse>> result = await _service.GetAllAsync();

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        Result<ProductResponse> result = await _service.GetByIdAsync(id);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        Result<ProductResponse> result = await _service.CreateAsync(request);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Update(int id, UpdateProductRequest request)
    {
        Result<ProductResponse> result = await _service.UpdateAsync(id, request);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        Result result = await _service.DeleteAsync(id);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
```

### Mitä tässä tapahtuu?

**`ActionResult<T>` paluutyyppi:**
- `ActionResult<ProductResponse>` kertoo kääntäjälle ja Swaggerille, että onnistunut vastaus sisältää `ProductResponse`:n
- Swagger generoi response-scheman **automaattisesti** — ei tarvitse kirjoittaa dokumentaatiota erikseen
- `Delete` käyttää edelleen `IActionResult`:ia, koska `204 No Content` ei palauta bodya

**`[ProducesResponseType]`-attribuutit:**
- Dokumentoivat **kaikki mahdolliset statuskoodit** jokaiselle endpointille
- Swagger/OpenAPI näyttää oikeat response-tyypit jokaiselle statuskoodille
- Tiimin muut kehittäjät näkevät heti mitä endpoint voi palauttaa ilman koodin lukemista

### Milloin käyttää mitäkin?

| Paluutyyppi | Käyttötapaus |
|-------------|--------------|
| `ActionResult<T>` | Endpoint palauttaa tietyn tyyppisen vastauksen (yleisin) |
| `IActionResult` | Endpoint ei palauta bodya (esim. `204 No Content`) tai palauttaa useita eri tyyppejä |

**Nyrkkisääntö:** Käytä `ActionResult<T>`:tä aina kun endpoint palauttaa datan. Käytä `IActionResult`:ia vain kun bodya ei palauteta.

### 10.2 Testaaminen

Käynnistä sovellus ja avaa Swagger UI. Huomaa ero:

- **Ennen:** Swagger näytti jokaiselle endpointille vain "200 OK" ilman response-schemaa
- **Nyt:** Swagger näyttää tarkan response-scheman (`ProductResponse`) ja kaikki mahdolliset statuskoodit (200, 201, 400, 404, 500)

---

## Yhteenveto — Mitä opimme?

Lopputuloksena meillä on kolmikerroksinen arkkitehtuuri:

```
HTTP-pyyntö → Controller → Service → Repository → Tietokanta
                 ↓             ↓            ↓
            DTO:t          DTO ↔ Entity   Entity:t
            Result → HTTP  Result Pattern  EF Core
            ActionResult<T> ILogger
```

| Käsite | Mitä opittiin |
|--------|---------------|
| Service-kerros | Liiketoimintalogiikka erillään HTTP-kerroksesta |
| Interface | Sopimus — controller riippuu abstraktiosta, ei toteutuksesta |
| DTO ↔ Entity servicessä | Service omistaa kaikki muunnokset — controller tuntee vain DTO:t |
| Ohut controller | Controller ei tunne entiteettejä, mappingia eikä tietokantaa |
| AddScoped | Service luodaan uudelleen joka HTTP-pyynnölle |
| Refaktorointi | Toiminnallisuus pysyy samana, rakenne paranee |
| Repository Pattern | Tietokantakoodi erillään service-kerroksesta |
| ILogger | Virheiden lokittaminen tuotantoympäristöä varten |
| Exception-käsittely | Odotetut vs. odottamattomat virheet, try-catch + lokitus |
| Result Pattern | Eksplisiittinen onnistuminen/epäonnistuminen paluuarvona |
| Result vs null/bool | Virheviesti kulkee mukana — ei arvailua |
| `ActionResult<T>` | Tyyppiturva — kääntäjä ja Swagger tietävät palautustyypin |
| `[ProducesResponseType]` | API-dokumentaatio — kaikki statuskoodit näkyvät Swaggerissa |

---

# Itsenäinen harjoitus — CategoriesController

Nyt on sinun vuorosi. Tee `CategoriesController`:lle sama refaktorointi kuin `ProductsController`:lle tehtiin ohjattussa osiossa. Tavoitteena on, että molemmat controllerit noudattavat samaa arkkitehtuuria.

Sovella kaikki vaiheet samassa järjestyksessä:

### 1. Service-kerros (Vaiheiden 1–6 tapaan)

- [ ] Luo `Services/ICategoryService.cs` — interface, joka ottaa Request-DTO:ita ja palauttaa `CategoryResponse`
- [ ] Luo `Services/CategoryService.cs` — toteutus, joka hoitaa kaikki DTO ↔ Entity -muunnokset
- [ ] Controller välittää DTO:t serviceen ja palauttaa servicen antamat DTO:t sellaisenaan
- [ ] Rekisteröi `ICategoryService` → `CategoryService` `Program.cs`:ssä

### 2. Repository-kerros

- [ ] Luo `Repositories/ICategoryRepository.cs` — interface kategorian tietokantaoperaatioille
- [ ] Luo `Repositories/CategoryRepository.cs` — toteutus EF Coren kanssa
- [ ] Rekisteröi `ICategoryRepository` → `CategoryRepository` `Program.cs`:ssä
- [ ] Päivitä `CategoryService` käyttämään `ICategoryRepository`:a `AppDbContext`:n sijaan

### 3. Exception-käsittely

- [ ] Lisää `ILogger<CategoryService>` konstruktoriin
- [ ] Lisää try-catch repository-kutsujen ympärille
- [ ] Lokita virheet `_logger.LogError`-kutsulla

### 4. Result Pattern

- [ ] Päivitä `ICategoryService` palauttamaan `Result<T>` / `Result`
- [ ] Päivitä `CategoryService` käyttämään `Result.Success()` / `Result.Failure()`
- [ ] Päivitä `CategoriesController` käsittelemään `Result`-olioita

### 5. Tyyppiturvallisuus ja API-dokumentaatio

- [ ] Vaihda `IActionResult` → `ActionResult<CategoryResponse>` (paitsi Delete)
- [ ] Lisää `[ProducesResponseType]`-attribuutit jokaiselle endpointille
- [ ] Tarkista Swagger UI:sta, että response-schemat ja statuskoodit näkyvät oikein

### Tarkistuslista

Kun olet valmis, varmista:

- [ ] `CategoryService` ei sisällä `using Microsoft.EntityFrameworkCore` -lausetta
- [ ] `CategoryService` ottaa vastaan Request-DTO:ita ja palauttaa `CategoryResponse` / `Result<CategoryResponse>`
- [ ] `CategoriesController` ei sisällä `null`-tarkistuksia — vain `result.IsFailure`
- [ ] `CategoriesController` ei sisällä `.ToEntity()` tai `.ToResponse()` -kutsuja — service hoitaa kaiken
- [ ] `CategoriesController` ei tarvitse `using ProductApi.Mappings` eikä `using ProductApi.Models` -lauseita
- [ ] Swagger UI:ssa kaikki Category-endpointit toimivat ja näyttävät oikeat response-schemat
- [ ] Swagger UI:ssa näkyvät kaikki mahdolliset statuskoodit (200, 201, 204, 400, 404)
- [ ] Virhetilanteissa (esim. GET /api/categories/999) palautuu JSON-virheilmoitus

---

## Bonusharjoitus: Hakutoiminto

Lisää tuotteiden hakutoiminto koko ketjun läpi:

1. Lisää `IProductRepository`:iin: `Task<List<Product>> SearchByNameAsync(string searchTerm);`
2. Toteuta `ProductRepository`:ssä EF Coren `Where` + `Contains`-kyselyllä
3. Lisää `IProductService`:iin: `Task<Result<List<ProductResponse>>> SearchByNameAsync(string searchTerm);`
4. Toteuta `ProductService`:ssä — käsittele tyhjä hakutermi `Result.Failure`:lla
5. Lisää `ProductsController`:iin uusi endpoint: `[HttpGet("search")]`

---

## Palautus

Palauta työ GitHub-repoon. Varmista, että:

**Rakenne:**
- Projekti käynnistyy (`dotnet run` tai F5)
- Swagger UI toimii ja kaikki endpointit vastaavat
- `Repositories/`-kansio sisältää interfacet ja toteutukset
- `Common/Result.cs` sisältää Result-luokat

**Products (ohjattu osio):**
- `ProductsController` ei sisällä tietokanta-, muunnos- tai null-tarkistuskoodia
- `ProductsController` ei tarvitse `using ProductApi.Mappings` eikä `using ProductApi.Models` -lauseita
- `ProductsController` käyttää `ActionResult<T>`:tä ja `[ProducesResponseType]`-attribuutteja
- `ProductService` ottaa vastaan Request-DTO:ita, palauttaa `Result<ProductResponse>` ja hoitaa kaiken muunnoksen
- `ProductRepository` sisältää kaiken EF Core -koodin

**Categories (itsenäinen harjoitus):**
- `CategoriesController` käyttää samaa rakennetta kuin Products — ei tunne entiteettejä
- `CategoriesController` käyttää `ActionResult<T>`:tä ja `[ProducesResponseType]`-attribuutteja
- `CategoryService` ottaa vastaan DTO:ita, käyttää `ICategoryRepository`:a ja `Result<CategoryResponse>`:a
- Kaikki Category-endpointit palauttavat JSON-virheilmoitukset
- Swagger UI näyttää oikeat response-schemat ja statuskoodit

**Teoria:**
- Vastaa [teoriakysymyksiin](questions.md)
