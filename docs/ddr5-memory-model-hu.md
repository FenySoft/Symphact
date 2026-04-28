# DDR5 Memória Modell — Symphact Nézőpont

> English version: [ddr5-memory-model-en.md](ddr5-memory-model-en.md)

> Version: 1.2

Ez a dokumentum a Symphact **DDR5 memóriakezelési modelljét** rögzíti: hogyan kér egy aktor hozzáférést, hogyan használja a memóriát, és hogyan adja vissza. Nem csak a végeredményt, hanem az **érvelési utat** is dokumentálja.

> **Célközönség:** OS fejlesztők, Symphact API tervezők, aktor-szoftver fejlesztők. A hardveres (RTL) nézőpontot a [CLI-CPU docs/ddr5-architecture-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/ddr5-architecture-hu.md) tartalmazza.

## Kontextus: nincs kernel, nincs kernel/user mód

A Symphact-ben **nincs hagyományos kernel réteg**. A hardveres izoláció (shared-nothing multi-core, core-onkénti SRAM) már garantálja azt, amit más OS-ek a kernel/user mód váltással érnek el. Ehelyett **privilégium szinteket az aktor-kapcsolatok** fejezik ki:

```
         root_supervisor
        /               \
kernel_core_sup      kernel_io_sup
   /    \               /      \
scheduler allocator  ddr5_gw   eth_gw   ...
                        |
                   DDR5 Controller (HW, config port)
```

A `kernel_io_sup` és gyermekei **ugyanolyan aktorok**, mint bármelyik alkalmazás-aktor — Rich Core-on futnak, mailbox-on kommunikálnak. A különbség: a `root_supervisor`-tól kapott **capability** révén ismerik a DDR5 Controller config port MMIO címét.

---

## 1. döntés: Ki kezdeményezi a DDR5 hozzáférést?

### 1.a) Elvetett megoldás: a kernel_io_sup ütemezi a betöltést

Az első ötlet az volt, hogy a `kernel_io_sup` (OS) dönt arról, mikor és milyen adatot tölt be a core SRAM-jába — központi DMA-ütemezéssel.

**Miért vetettük el:** Az OS nem tudhatja, mikor és milyen adatra van szüksége az aktornak. Csak az aktor maga ismeri a saját feldolgozási logikáját. Központi ütemezés **felesleges komplexitást** és **latenciát** ad a rendszerhez.

### 1.b) Elvetett megoldás: az aktor közvetlenül eléri a DDR5-öt

A másik véglet: az aktor MMIO-n keresztül szabadon olvassa a DDR5-öt, mindenféle engedélykérés nélkül.

**Miért vetettük el:** Ha bármelyik aktor bármelyik DDR5 címet olvashatja, a shared-nothing izoláció **illúzió**. Egy kompromittált aktor más aktorok adatait olvasná.

### 1.c) Végső döntés: az aktor kér, a kernel_io_sup engedélyez

Az aktor **maga kezdeményezi** a hozzáférést, de a `kernel_io_sup` **ellenőrzi és engedélyezi**. Ez a capability modell:

- Az aktor tudja, mire van szüksége → ő kér
- A `kernel_io_sup` tudja, mihez van joga → ő dönt
- A DDR5 Controller CAM tábla érvényesíti → HW garantálja

---

## 2. döntés: Minden kéréshez vagy egyszeri engedélyhez kötött a hozzáférés?

### 2.a) Elvetett megoldás: minden DDR5 művelethez üzenet

Az aktor minden olvasás/írás előtt üzenetet küld a `kernel_io_sup`-nak, aki elvégzi a műveletet és visszaküldi az eredményt.

**Miért vetettük el:** Minden egyes DDR5 hozzáférés három üzenetet igényelne (kérés → io_sup → DDR5 → io_sup → válasz). Ez megháromszorozza a latenciát. Ha egy aktor intenzíven dolgozik egy DDR5 tartománnyal (pl. nagy tábla scan), ez elfogadhatatlan overhead.

### 2.b) Végső döntés: capability grant — egyszeri engedély, szabad használat

Az aktor egyszer kér hozzáférést, megkapja a capability-t (tartomány + jogosultság), és utána **közvetlenül, MMIO-n** olvassa/írja a DDR5-öt:

```
1. Aktor --> kernel_io_sup: MsgGrantRequest(ObjectId, Access: RW)
2. kernel_io_sup ellenőrzi a jogosultságot
3. kernel_io_sup --> DDR5 Controller config port: CAM bejegyzés hozzáadása
4. kernel_io_sup --> Aktor: MsgGranted(Region: start, length, access)
5. Aktor MMIO-n keresztül szabadon olvas/ír
   ... ahányszor akarja, további engedély nélkül
6. Aktor --> kernel_io_sup: MsgReleaseRegion(Region)
7. kernel_io_sup --> DDR5 Controller config port: CAM bejegyzés törlése
```

**Döntési érvek:**
- Egyszeri engedélykérés = egyszeri latencia, utána nulla overhead
- A HW CAM tábla biztosítja, hogy a core tényleg csak a saját tartományát érheti el
- Ha az aktor crash-el, a supervisor értesíti a `kernel_io_sup`-ot → capability automatikusan visszavonódik

---

## 3. döntés: Ownership modell

### 3.a) Elvetett megoldás: több aktor egyidejű hozzáférése

Engedélyezhetnénk, hogy több aktor egyidejűleg olvassa ugyanazt a DDR5 tartományt (reader-writer lock vagy immutable shared data analógia).

**Miért vetettük el:** Még a read-only megosztás is **shared state** — pontosan az, amit a shared-nothing architektúra kizár. Ha egy tartományt több core is lát egyidejűleg, implicit csatolás jön létre közöttük. Ha egy core-nak szüksége van egy másik core adatára, **üzenetben kap másolatot** — ez az actor modell alapelve.

### 3.b) Végső döntés: egyetlen tulajdonos, mindig

Egy DDR5 tartományra **egyetlen core-nak lehet hozzáférése egy időben**, legyen az olvasás vagy írás. Ha Core 42 megkapta, más core nem kaphatja meg, amíg Core 42 nem adta vissza.

Ha más core-nak is kell ugyanaz az adat, két megoldás van:

```
1. Másolat üzenetben:
   Core 42 (tulajdonos) --> MsgData(tartalom) --> Core 99

2. Szekvenciális hozzáférés:
   Core 42 megkapja → feldolgoz → visszaadja (MsgReleaseRegion)
   Core 99 megkapja → feldolgoz → visszaadja
```

---

## Aktor API — a programozó nézőpontja

A programozó **objektumokban gondolkodik**, nem DDR5 régiókban, címekben vagy MMIO műveletekben. Az API a C# természetes adatkezelési módját tükrözi: objektumokat tölt be, módosít és ment — az, hogy a háttérben DDR5, CAM tábla vagy NoC flit van, **láthatatlan**.

> **FONTOS:** Az actor modellben **nincs `await`** — az await deadlock-ot okoz, mert blokkolja a mailbox feldolgozást. Minden művelet üzenet-küldés + válasz külön `Handle` metódusban.

```csharp
public class TOrderProcessor : CfpuActor
{
    public void Handle(MsgProcessOrder AMsg)
    {
        // Objektumot kérek — nem tudok DDR5-ről, MMIO-ról, CAM tábláról
        FStoreRef.Send(new MsgLoad<TCustomer>(AMsg.CustomerId));
    }

    public void Handle(MsgLoaded<TCustomer> AMsg)
    {
        // Az objektum a core SRAM-ban van — dolgozhatok vele
        var LCustomer = AMsg.Object;
        LCustomer.Balance += 100;

        // Visszamentem — a Store aktor kezeli a DDR5 részleteket
        FStoreRef.Send(new MsgSave<TCustomer>(LCustomer));
    }

    public void Handle(MsgSaved AMsg)
    {
        // Az objektum kiírva, a DDR5 capability visszaadva
        FRequesterRef.Send(new MsgOrderDone());
    }

    public void Handle(MsgLoadFailed AMsg)
    {
        // Nem sikerült (foglalt, nem létezik, nincs jog)
    }
}
```

### Mi történik a háttérben?

A `FStoreRef` egy **Store aktor** (a `kernel_io_sup` gyermeke), amely a programozó elől elrejti a DDR5 kezelést:

```
Programozó:     MsgLoad<TCustomer>(id)
                    |
Store aktor:    ObjectId → DDR5 cím mapping
                MsgGrantRequest → kernel_io_sup
                DDR5 olvasás (MMIO)
                Deszerializálás → TCustomer objektum
                MsgLoaded<TCustomer> → kérő aktor
                MsgReleaseRegion → kernel_io_sup
```

A programozó **egy üzenetet küld** (`MsgLoad`), és **egy választ kap** (`MsgLoaded` + az objektum). A DDR5 capability grant/release, a szerializálás és a memória-kezelés a Store aktor belügye.

## Üzenettípusok

### Programozói szint (amit a fejlesztő lát)

| Üzenet | Irány | Leírás |
|--------|-------|--------|
| `MsgLoad<T>(ObjectId)` | Aktor → Store | Objektum betöltése |
| `MsgLoaded<T>(Object)` | Store → Aktor | Objektum betöltve, SRAM-ban |
| `MsgSave<T>(Object)` | Aktor → Store | Objektum mentése |
| `MsgSaved(ObjectId)` | Store → Aktor | Mentés kész |
| `MsgLoadFailed(Reason)` | Store → Aktor | Betöltés sikertelen |

### Rendszer szint (amit a Store aktor belsőleg használ)

| Üzenet | Irány | Leírás |
|--------|-------|--------|
| `MsgGrantRequest(ObjectId, Access)` | Store → kernel_io_sup | DDR5 hozzáférés kérése |
| `MsgGranted(Region)` | kernel_io_sup → Store | Hozzáférés megadva |
| `MsgReleaseRegion(Region)` | Store → kernel_io_sup | Hozzáférés visszaadása |

## Crash recovery

Ha egy aktor crash-el, és nem adta vissza a capability-t:

```
1. Aktor trap-et generál
2. A core HW érzékeli (cooperative switching szint)
3. Scheduler értesíti a supervisor-t: MsgActorCrashed(ActorId)
4. Supervisor jelzi a kernel_io_sup-nak: MsgActorCrashed(src[24], src_actor[8])
5. kernel_io_sup törli CSAK az adott aktor CAM bejegyzéseit
6. A többi aktor ugyanazon a core-on zavartalanul fut tovább
7. A tartomány felszabadul — más aktor megkaphatja
```

Ez az Erlang/OTP "let it crash" modell hardveres implementációja — az aktor nem kell, hogy "takarítson maga után", a supervisor hierarchy kezeli. Az N:M actor-to-core mapping miatt a crash recovery **aktor szintű**, nem core szintű — a DDR5 Controller CAM tábla `src[24] + src_actor[8]` alapján azonosítja a bejegyzéseket (lásd `interconnect-hu.md` v2.4 header spec).

## Kapcsolódó HW döntések

A Symphact fejlesztőknek fontos tudni a HW korlátokat, amik az API-t befolyásolják:

| HW tény | OS következmény |
|---------|----------------|
| A DDR5 Controller **10 portja** van | Max ~10 egyidejű DDR5 kérés feldolgozás — de a core-ok ritkán fordulnak DDR5-höz (SRAM-ból dolgoznak) |
| A CAM tábla **véges méretű** | A kernel_io_sup-nak nyilván kell tartania az aktív capability-ket, és limitálnia az egyidejű grant-ok számát |
| A CAM `src[24] + src_actor[8]` alapján ellenőriz | **Aktor szintű** jogosultság — egy core-on több aktor kaphat külön DDR5 tartományt (max 256 actor/core) |
| A **src[24] nem hamisítható** (HW) | Az OS nem kell, hogy külön azonosítsa a core-t — a HW megtette |
| A **src_actor[8] a core HW tölti ki** (aktív actor context regiszter), nem hamisítható | Az aktor nem tudja felülírni a saját actor ID-ját — a HW automatikusan az aktuális context regiszterből tölti |
| A config port **hardwired** | A kernel_io_sup-nak azon a Rich Core-on kell futnia, amelyik fizikailag össze van kötve a config porttal |

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.2 | 2026-04-24 | src_actor[16] → src_actor[8] (max 256 actor/core). src_actor kitöltése: core HW (aktív actor context regiszter), nem hamisítható. Core scheduler → Core HW javítás |
| 1.1 | 2026-04-22 | Crash recovery aktor szintűre javítva (N:M mapping). CAM tábla src[24]+src_actor[16] alapú. Objektum-szintű aktor API (MsgLoad/MsgSave, nincs await). HW döntések tábla bővítve |
| 1.0 | 2026-04-22 | Első verzió — capability modell, ownership, crash recovery, aktor API |
