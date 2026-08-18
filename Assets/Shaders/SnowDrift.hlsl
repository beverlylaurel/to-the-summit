#ifndef TOTHESUMMIT_SNOW_DRIFT_INCLUDED
#define TOTHESUMMIT_SNOW_DRIFT_INCLUDED

// BİRİKİNTİ ALANI. Kar derinliğinin YATAY şekli.
//
// Neden gerekti: kar derinliği şimdiye kadar yalnız kot bandından, eğimden ve rüzgâr
// maruziyetinden geliyordu. Üçü de arazi ızgarasının çözünürlüğünde (4.28 m) değişiyor,
// yani derinlik dört metrenin altında DÜMDÜZ. Geometriye çevrilseydi birikinti değil
// yumuşak bir kabarma çıkardı; 8b'nin sorduğu şey o değil.
//
// Gerçekte birikintinin şeklini rüzgâr yapıyor:
//   - Kar yığınları rüzgâr ekseni boyunca UZAR, eksene dik daralır (dune ve sastrugi
//     ikisi de böyle). Yönsüz gürültü kabarcık verir, birikinti vermez.
//   - Oyuk dolar, sırt kazınır — arazinin kendi eğrisi genliği modüle eder.
//   - İki ölçek üst üste: gövde (15-40 m) ve yüzey dalgalanması (4-10 m).
//
// AYNI HESAP CPU'DA DA ÇALIŞIYOR (`SnowDriftField.cs`) — çarpışma yüzeyi görsel
// yüzeyi izlemek zorunda. Bu yüzden karma (hash) TAM SAYI aritmetiğiyle: sin tabanlı
// karma platformdan platforma, hatta derleyiciden derleyiciye kayıyor ve iki taraf
// sessizce ayrışıyordu. Tam sayı karması her yerde bit birebir aynı.

/// Wang karması. Tam sayı içeri, 0-1 arası kesir dışarı.
/// PROSEDÜREL YÜZEYİN TOHUMU. Kaya bandı, oksit, liken, tanecik, kırılma ve birikinti
/// şeklinin TAMAMI dünya koordinatına bağlı, sabit hash'li. Arazi baştan üretilse bile
/// aynı dünya koordinatında aynı desen ve aynı birikinti sırtı çıkıyordu — dağ yeni,
/// üstündeki kar heykeli eski.
///
/// Ölçüldü: `snowDisplaceMax` 3.2 m, yani birebir tekrar eden katman 5709 metrelik dağın
/// 1/1780'i. Küçük ama yüzeye yakından bakılınca ekranın çoğunu kaplıyor.
///
/// Tohum İKİ HASH KÖKÜNE birden giriyor (`SnowDriftHash`, `MountainHash`); tek tek çağrı
/// yerlerine değil. Böylece yeni bir katman eklendiğinde kaydırmayı unutmak mümkün değil.
///
/// `SnowDrift.hlsl` en altta duruyor: `MountainSurfaceInput` de `SnowDisplacement` de
/// onu dahil ediyor, yani bildirim burada bir kez yapılıyor.
float4 _PatternSeed;

float SnowDriftHash(uint2 cell)
{
    cell += (uint2)abs(_PatternSeed.xy);
    uint h = cell.x * 73856093u ^ cell.y * 19349663u;
    h = (h ^ 61u) ^ (h >> 16);
    h *= 9u;
    h = h ^ (h >> 4);
    h *= 0x27d4eb2du;
    h = h ^ (h >> 15);
    return float(h & 0x00ffffffu) / 16777216.0;
}

/// Değer gürültüsü: köşeleri karmalanmış ızgara, smoothstep ile ara değerleme.
/// Perlin/simplex yerine bu seçildi çünkü C# tarafında birebir aynısını yazmak
/// gradyan tabanlı gürültüden çok daha güvenli.
float SnowDriftNoise(float2 position)
{
    float2 cell = floor(position);
    float2 f = position - cell;
    f = f * f * (3.0 - 2.0 * f);

    uint2 id = uint2(int2(cell) + 4096);   // negatif koordinatlar için kaydırma

    float a = SnowDriftHash(id);
    float b = SnowDriftHash(id + uint2(1, 0));
    float c = SnowDriftHash(id + uint2(0, 1));
    float d = SnowDriftHash(id + uint2(1, 1));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Birikinti şekli, 0-1. 0.5 nötr: altı kazınmış, üstü yığılmış.
///
/// windAxis: hâkim rüzgârın yatay birim vektörü.
///
/// KONKAVLIK GİRDİ DEĞİL. Denendi ve yüzeyde tarama çizgileri bıraktı: yüzey
/// haritasının konkavlık kanalı akış birikiminden türüyor ve IZGARAYA HİZALI bir
/// gürültü taşıyor (bkz. DECISIONS.md — aynı kanal daha önce de kalınlığa girip
/// yamaçta dişli desen bırakmıştı). Alanın kendisi ölçüldü ve temiz; desen o
/// kanaldan geliyordu.
///
/// Arazi modülasyonu tamamen kaybolmuyor: birikinti EĞİMDEN zaten sınırlanıyor
/// (`SnowMacroDepth` içinde), eğim haritası ise dört kat geniş texel'de ve desen
/// taşımıyor.
float SnowDriftShape(float2 worldXZ, float2 windAxis)
{
    // Rüzgâr eksenine dönük koordinat: u rüzgâr boyunca, v ona dik.
    float2 perpendicular = float2(-windAxis.y, windAxis.x);
    float2 aligned = float2(dot(worldXZ, windAxis), dot(worldXZ, perpendicular));

    // ALAN BÜKÜMÜ. Değer gürültüsü ızgarasına hizalı; uzatılınca düzenli bantlar
    // veriyor ve yüzeyde tarama çizgileri olarak okunuyordu. Koordinatı düşük
    // frekanslı ikinci bir gürültüyle bükmek hizalanmayı kırıyor — şekil ızgaraya
    // değil kendi eğrisine oturuyor.
    //
    // Büküm genliği kaynak özelliğinden KÜÇÜK (12 m büküm / 45 m gövde): büyük olsaydı
    // alan kendi içine katlanır ve birikinti kopuk lekelere dağılırdı.
    float2 warp = float2(SnowDriftNoise(aligned / 62.0),
                         SnowDriftNoise(aligned / 62.0 + 37.7)) - 0.5;
    aligned += warp * 24.0;

    // İKİNCİ BÜKÜM, GÖVDE ÖLÇEĞİNDE. Yukarıdaki büküm 62 metreden örnekleniyor ve
    // gövdenin rüzgâra dik hücresi 16 metre: 62 metrelik bir yamanın içindeki bütün
    // hücreler AYNI yöne kayıyor, yani ızgara kırılmıyor, sadece topluca ötelenip
    // duruyor. Ölçüldü: terminatör bandı boyunca birebir aynı boyda, eşit aralıklı
    // dişler — sırt hattında testere ağzı.
    //
    // Kural (`SYMPTOMS.md`, "Düzenli kafes deseni"): büküm KAYNAK ÖZELLİK ÖLÇEĞİNDE
    // örneklenir. 21 m dalga boyu 16 m hücreyi kırar.
    //
    // Genlik 7 m, hücrenin yarısından küçük: büyük olsaydı alan kendi içine katlanır
    // ve birikinti kopuk lekelere dağılırdı (üstteki bükümde aynı sınır 24/45).
    float2 fineWarp = float2(SnowDriftNoise(aligned / 21.0 + 11.3),
                             SnowDriftNoise(aligned / 21.0 + 57.1)) - 0.5;
    aligned += fineWarp * 7.0;

    // GÖVDE: rüzgâr boyunca uzun, ona dik geniş. Oran 2.8:1 — daha keskin uzatma
    // (önceki 4:1) birikinti değil çizgi üretiyordu.
    float2 body = float2(aligned.x / 45.0, aligned.y / 16.0);
    float shape = SnowDriftNoise(body);

    // İKİNCİ OKTAV DÖNDÜRÜLMÜŞ: aynı eksende ikinci bir ölçek, ızgarayı yeniden
    // aynı yöne dizerdi. 31 derece dönüş iki oktavın hizalanmasını engelliyor.
    const float2x2 turn = float2x2(0.857, -0.515, 0.515, 0.857);
    float2 second = mul(turn, aligned) / float2(19.0, 11.0);
    shape = shape * 0.68 + SnowDriftNoise(second) * 0.32;

    // Mikro dalgalanma BURADA YOK. Yüzeyin santimetre ölçeği kar dokularının işi
    // (SnowPowder/SnowPacked normal haritaları); buraya konunca metre ölçeğinde
    // ince çizgilere dönüşüyordu.

    return saturate(shape);
}

#endif
