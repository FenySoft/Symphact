# OS-követelmények a CFPU hardverhez

> English version: [README.md](README.md)

Ez a könyvtár a Symphact fejlesztése során felszínre kerülő **OS-oldali követelményeket** gyűjti, amelyeket a CFPU hardvertervezésnél figyelembe kell venni (nyilvántartva a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) repóban).

## Munkafolyamat

1. Egy Symphact fejlesztő hardver-alakú igényt fedez fel (profilozás, szoftveres megkerülő megoldás, vagy tervezési falba ütközés során)
2. Issue-t nyit **ebben a repóban** az [`osreq-for-cfpu`](../../.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) sablon használatával
3. Ha a követelmény elég jelentős ahhoz, hogy tartós dokumentumot érdemel, egy markdown fájl kerül **ebbe a könyvtárba**
4. Egy hivatkozott issue nyílik a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) repóban `osreq-from-os` címkével, a Symphact issue-ra hivatkozva
5. A hardvertervezők (F2 RTL, F3 TT, F4 FPGA, F5 Rich core) figyelembe veszik a követelményt a vonatkozó fázisban

## Miért fontos ez

Ez az **OS → HW visszacsatolási hurok**. Enélkül a hardver- és OS-fejlesztés szétcsúszik, és minden CPU/OS eltérés a történelemben (x86 szegmentálás, Itanium VLIW, ARM big.LITTLE bevezetés, Spectre/Meltdown) a mi történetünk is lesz.

Az Apple M-sorozat sikere pontosan erre a hurokra épül: a macOS QoS osztályok informálták a P-core/E-core aszimmetriát, a Core ML a Neural Engine-t, a Keychain a Secure Enclave-et, az x86 emuláció (Rosetta 2) pedig a TSO memory-ordering módot. Azt akarjuk, hogy a nyílt forráskódú Symphact ugyanezt tegye a nyílt forráskódú CFPU-ért.

## Aktuális nyitott követelmények

*(feltöltődik a Symphact fejlesztés előrehaladtával)*

| # | Cím | CFPU fázis | Állapot |
|---|-----|------------|---------|
| [OSREQ-001](osreq-001-tree-interconnect-hu.md) | Fa topológiájú interconnect a core-ok között | F4, F5, F6 | Draft |
| [OSREQ-002](osreq-002-mmio-memory-map-hu.md) | MMIO memória térkép — OS↔HW regiszter interfész | F4, F5, F6 | Draft |
| [OSREQ-003](osreq-003-core-reset-hu.md) | Core reset mechanizmus — supervisor restart | F4, F5, F6 | Draft |
| [OSREQ-004](osreq-004-dma-engine-hu.md) | DMA engine — nem-blokkoló persistence | F5, F6 | Draft |
| [OSREQ-005](osreq-005-mailbox-interrupt-hu.md) | Mailbox interrupt vs polling — core értesítés | F4, F5, F6 | Draft |
| [OSREQ-006](osreq-006-interchip-link-hu.md) | Inter-chip link protokoll — elosztott fabric | F6, F7 | Draft |
