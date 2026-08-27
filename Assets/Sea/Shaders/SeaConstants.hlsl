// ROL: deniz sisteminin butun sabitleri. `SeaConstants.cs` ile BIREBIR ayni
// degerleri tasiyor; esligi `SeaConstantsTest` siniyor.
// Cagiran: SeaCommon.hlsl uzerinden butun deniz shader'lari.

#ifndef SEA_CONSTANTS_INCLUDED
#define SEA_CONSTANTS_INCLUDED

// --- Fizik ---

/// Yercekimi. [KAYNAK: Tessendorf 2004 4.2]
#define SEA_G                    9.81

#define SEA_TWO_PI               6.28318530718

/// Suyun kirilma indisi. [KAYNAK: Tessendorf 2004 6.1.2, 6.3 ornek shader]
#define SEA_WATER_IOR            1.34

/// Su hacminin toplu yansitmasi, Lambert yansitici gibi ele aliniyor.
/// [KAYNAK: Tessendorf 2004 7.1]
#define SEA_BULK_REFLECTIVITY    0.04

// --- Spektrum (JONSWAP / TMA) ---

/// Tepe keskinligi. [KAYNAK: Horvath 2015 / JONSWAP]
#define SEA_JONSWAP_GAMMA        3.30

/// Tepe genisligi; omega tepe frekansinin altinda ve ustunde farkli.
/// [KAYNAK: JONSWAP]
#define SEA_JONSWAP_SIGMA_LO     0.07
#define SEA_JONSWAP_SIGMA_HI     0.09

/// Derin su dikligi siniri. FFT ciktisinda asilirsa dalga zaten Jacobian
/// testiyle kopuk uretiyor; ayri bir kontrol YAZILMIYOR.
/// [KAYNAK: Michell 1893]
#define SEA_MICHELL_STEEPNESS    0.142

// --- Sig su ve kirilma ---

/// Sifira bolmeyi engelleyen taban derinligi (m). [KALIBRASYON]
#define SEA_MIN_DEPTH            0.05

/// Kiyi cizgisinde dalga sonumu (m). Su derinligi bunun altina inince dalga
/// yuksekligi sifira gidiyor; yoksa mesh araziyle kesisip titriyor.
/// [KALIBRASYON]
#define SEA_SHORE_FADE_DEPTH     0.60

/// Yatay displacement'in sig suda sonduugu derinlik (m). Dalga dikelesir,
/// yatayda yayilmaz. [KALIBRASYON]
#define SEA_CHOP_FADE_DEPTH      8.00

/// KIRILMA DERINLIK INDEKSI, EGIME BAGLI.
///
/// McCowan'in 0.78'i muhendislik pratiginde en yaygin ilk tahmin ama SABIT
/// DEGIL: cok hafif egimlerde alt sinir 0.55'e iniyor, dik sahillerde 1.0'in
/// ustune cikiyor. Bu yuzden egime bagli lerp kullaniliyor, sabit 0.78 degil.
/// [KAYNAK: McCowan 1894; Nelson 1983; DNV 2017; Galvin 1969; Weggel 1972]
#define SEA_GAMMA_MILD           0.55
#define SEA_GAMMA_STEEP          1.10

/// Kirilmanin urettigi kopuk kazanci. [KALIBRASYON]
#define SEA_BREAK_FOAM_GAIN      1.60

// --- Kopuk (Jacobian) ---

/// Jacobian esigi ve gecis araligi. J < 0 yuzeyin katlandigini gosteriyor;
/// esik ondan once basliyor ki kopuk yumusak girsin.
/// [KAYNAK: Tessendorf 2004 4.6 — katlanma testi] [KALIBRASYON: esik degeri]
#define SEA_FOAM_J_THRESHOLD     0.55
#define SEA_FOAM_J_RANGE         0.55

/// Kopugun sonum hizi (1/s). Kopuk ANINDA olusur, YAVAS kaybolur; dogrudan
/// atama yapilirsa kopuk aninda kayboluyor. [KALIBRASYON]
#define SEA_FOAM_DECAY           0.28

// --- FFT ve izgara ---

/// FFT izgarasinin UST SINIRI. Kalite presetleri bunun altinda calisiyor
/// (`_SeaFftSize`); burasi `numthreads`'in degeri ve dokularin en buyuk
/// boyutu — degismez.
///
/// `numthreads` KEYWORD'E BAGLANMADI. Varyanta bagli `numthreads` her
/// varyant icin ayri `GetKernelThreadGroupSizes` ve ayri dispatch sayisi
/// demek; kucuk FFT'de 256 is parcaciginin yarisi bos calisiyor ama
/// bariyerler tek dalda kaliyor ve sessiz bir tanimsiz davranis riski
/// dogmuyor.
/// [KAYNAK: Tessendorf 2004 4.4 — "For many situations, values in the
/// range 128 to 512 are sufficient"]
#define SEA_FFT_SIZE             256
#define SEA_FFT_LOG2             8

/// Kademe UST SINIRI. Preset daha azini calistirabilir; dokular her zaman
/// bu derinlikte kuruluyor.
/// Tek bir yama hem 200 m'lik olu dalgayi hem 20 cm'lik cirpintiyi
/// tasiyamaz. [KAYNAK: Tessendorf 2004 4.4; Dupuy & Bruneton 2012]
#define SEA_TIER_COUNT           3

#endif
