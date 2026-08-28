# Uygulama raporu: aydınlatma/sis entegrasyon boşlukları

Plan: `.claude/PRPs/plans/completed/lighting-integration-gaps.plan.md`
Commit'ler: `b44698c` (davranış), `5f32429` (davranış), `48f0ab9` (belge)

## Görevler

| # | Görev | Durum |
|---|---|---|
| 0 | F1'e deniz izolasyon anahtarları | bitti |
| 1 | `SeaLit`: ölü `MixFog` → `ApplyHeightFog`, `GetMainLight(shadowCoord)` + cookie | bitti |
| 2 | `SnowCoverObject`: sis + cookie | bitti |
| 3 | `SnowfallParticle`: sis + cookie | bitti |
| 4 | `BikeSurface`: `_LIGHT_COOKIES` pragması | bitti |
| 5 | Alpenglow ↔ bulut kapsaması | bitti — karar (a) bağ kur |
| 6 | Ay tooltip'i, varsayılanları, hüzme invariantı yorumu | bitti |
| 7 | `skyAmount` ad çakışması | bitti (önceki oturumda) |
| 8 | Gökyüzü yedeğinin ölü dalı | bitti — karar (b) kes |
| 9 | Gölge mesafesi 50 → 150 (yorum + `DECISIONS.md`) | bitti |
| 10 | `SYSTEMS.md`, `RATIONALE.md`, `DECISIONS.md` | bitti |

Plan dışı çıkan bir madde: `MountainSurface.shader` `fogFactor` interpolatörünü ve
`ComputeFogFactor` çağrısını hâlâ taşıyordu; okuyucusu yalnız `MixFog` olduğu için ölüydü,
aynı sınıfın beşinci örneği olarak aynı adımda silindi.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| Proje geneli shader denetimi | 17 shader, 0 hata |
| Konsol | boş |
| `grep MixFog Assets` | yalnız açıklama satırlarında |
| `Sea/Test Constant Parity` | 20/20 aynı |
| `Sea/Test Wave Field` | passed; U10=15'te min(J) = 0,219 |

**Ekranda doğrulanmadı.** Editör odaksızken `ExecuteAlways` tick atmıyor, malzeme
uniform'ları okunamıyor; Play'e kullanıcı basacak.

## Ölçülmeyen maliyet

Kar taneciği başına 8 adımlı yükseklik integrali. `DECISIONS.md` → "Kar taneciği sis
maliyeti ölçülmedi", tetikleyicisiyle birlikte.
