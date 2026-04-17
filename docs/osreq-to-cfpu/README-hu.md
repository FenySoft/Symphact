# OS-követelmények a CFPU hardverhez

> English version: [README.md](README.md)

Ez a könyvtár a Neuron OS fejlesztése során felszínre kerülő **OS-oldali követelményeket** gyűjti, amelyeket a CFPU hardvertervezésnél figyelembe kell venni (nyilvántartva a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) repóban).

## Munkafolyamat

1. Egy Neuron OS fejlesztő hardver-alakú igényt fedez fel (profilozás, szoftveres megkerülő megoldás, vagy tervezési falba ütközés során)
2. Issue-t nyit **ebben a repóban** az [`osreq-for-cfpu`](../../.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) sablon használatával
3. Ha a követelmény elég jelentős ahhoz, hogy tartós dokumentumot érdemel, egy markdown fájl kerül **ebbe a könyvtárba**
4. Egy hivatkozott issue nyílik a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) repóban `osreq-from-os` címkével, a Neuron OS issue-ra hivatkozva
5. A hardvertervezők (F2 RTL, F3 TT, F4 FPGA, F5 Rich core) figyelembe veszik a követelményt a vonatkozó fázisban

## Miért fontos ez

Ez az **OS → HW visszacsatolási hurok**. Enélkül a hardver- és OS-fejlesztés szétcsúszik, és minden CPU/OS eltérés a történelemben (x86 szegmentálás, Itanium VLIW, ARM big.LITTLE bevezetés, Spectre/Meltdown) a mi történetünk is lesz.

Az Apple M-sorozat sikere pontosan erre a hurokra épül: a macOS QoS osztályok informálták a P-core/E-core aszimmetriát, a Core ML a Neural Engine-t, a Keychain a Secure Enclave-et, és így tovább. Azt akarjuk, hogy a nyílt forráskódú Neuron OS ugyanezt tegye a nyílt forráskódú CFPU-ért.

## Aktuális nyitott követelmények

*(feltöltődik a Neuron OS fejlesztés előrehaladtával)*

| # | Cím | CFPU fázis | Állapot |
|---|-----|------------|---------|
| — | (még nincs — a fejlesztés most indult) | — | — |
