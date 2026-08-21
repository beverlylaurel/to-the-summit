#ifndef SNOW_CONSTANTS_INCLUDED
#define SNOW_CONSTANTS_INCLUDED

// ROL: kar sisteminin FİZİKSEL sabitleri, HLSL tarafı.
// C# ikizi: Runtime/SnowConstants.cs. İkisi SnowDebugWindow'daki eşlik testiyle
// karşılaştırılır; biri değişip diğeri değişmezse test kırmızıya döner.
// Sanatsal ayar buraya GİRMEZ — onların yeri SnowSettings asset'i.

// --- yoğunluk ve kütle (§1, §2.1) ---
#define SNOW_RHO_MIN             50.0
#define SNOW_RHO_MAX            550.0
#define SNOW_RHO_WATER         1000.0

// --- taşıma kapasitesi (§5.3) ---
#define SNOW_SIGMA_REF         4000.0
#define SNOW_RHO_REF            100.0
#define SNOW_BEARING_N            3.2

// --- durum sınırları ---
#define SNOW_SWE_MAX              0.60
#define SNOW_REPOSE_TAN           0.781

// --- zaman sabitleri, saniye (§6) ---
#define SNOW_SETTLE_TAU       21600.0
#define SNOW_DISTURB_TAU        900.0
#define SNOW_WET_TAU           1800.0

// Derece-gün erime katsayısı: 4 mm/(C.gun) = 0.004 / 86400 m/(C.s).
#define SNOW_MELT_DDF          4.63e-8

// --- sabit noktalı atomik toplama ölçekleri (§5.4, §5.5) ---
#define SNOW_MASS_FIXED_SCALE 4194304.0
#define SNOW_RING_SUM_SCALE     65536.0

// Damga dilimi başına temas alanı oranı (§5.2 tablosu).
static const float kStampAreaFrac[6] = { 0.785, 0.62, 0.62, 0.78, 0.90, 0.70 };

// Duruş açısı geçişinin dört komşusu (§5.6).
static const int2 kOffsets4[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

#endif
