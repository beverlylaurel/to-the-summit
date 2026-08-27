// ROL: dalga alaninin sayisal dogrulamasi. Spec 6.8 ve 7'nin kabul
// kriterlerini olcuyor.
// Cagiran: menu — To The Summit/Deniz/Dalga Alanini Sina

using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// FFT SESSİZCE YANLIŞ ÇALIŞIR.
///
/// RNG Gauss değilse yüzey düzenli desen verir; eşlenik simetri bozuksa
/// yüzey düz kalır. İkisi de ekranda "biraz tuhaf" görünür ve haftalarca
/// yanlış yerde aranır. Bu yüzden kabul kriteri **sayı**, göz değil.
public static class SeaSpectrumTest
{
    /// Rüzgârı bilinen bir değere sabitleyen ortam. Hava sisteminden
    /// gelseydi ölçüm tekrarlanabilir olmazdı.
    sealed class SabitOrtam : ISeaEnvironmentSource
    {
        public Vector3 yon = Vector3.right;
        public float hiz = 8f;

        public Vector3 WindDirection => yon;
        public float WindSpeed => hiz;

        public Light Sun => null;
        public float SunElevation01 => 0.5f;
        public Color SkyColor => Color.gray;
        public Color HorizonColor => Color.gray;
        public float CloudCover01 => 0f;
        public float FogDensity01 => 0f;
        public SeaPrecipitationKind PrecipKind => SeaPrecipitationKind.None;
        public float PrecipIntensity01 => 0f;
    }

    struct Olcum
    {
        public float ortalamaH;
        public float rmsH;
        public float rmsEgim;
        public float katlanmaOrani;
        public float enKucukJ;
        public float ruzgarBandiOrani;
        public float baskinYon;
    }

    [MenuItem("To The Summit/Deniz/Dalga Alanını Sına")]
    public static void Sina()
    {
        var ayar = AssetDatabase.LoadAssetAtPath<SeaSettings>(
            "Assets/Sea/Settings/SeaSettings.asset");
        var spek = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaSpectrum.compute");
        var fft = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaFFT.compute");
        var kopuk = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaFoam.compute");

        if (ayar == null || spek == null || fft == null || kopuk == null)
        {
            Debug.LogError("Deniz testi: ayar veya compute shader bulunamadı.");
            return;
        }

        var ortam = new SabitOrtam();

        // NESNE PASİF KURULUYOR. Aktifken `AddComponent` `OnEnable`'ı
        // hemen çalıştırıyor ve `Bind` henüz olmadığı için bileşen kendini
        // devre dışı bırakıyor.
        var go = new GameObject("SeaSpectrumTest") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);

        var sim = go.AddComponent<SeaSimulation>();
        sim.Bind(ayar, ortam, spek, fft, kopuk);

        go.SetActive(true);

        var rapor = new StringBuilder();
        rapor.AppendLine("DALGA ALANI ÖLÇÜMÜ");
        rapor.AppendLine();

        int hata = 0;

        // --- 1. Eşlenik simetri ve enerji, U10 = 8 m/s ---
        ortam.hiz = 8f;
        ortam.yon = Vector3.right;
        sim.Adim(0f);

        rapor.AppendLine("U10 = 8 m/s, yön = +X");
        rapor.AppendLine("kademe |  mean(h)  |  rms(h)  | rms(eğim) |  J<0   | rüzgâr bandı");

        for (int k = 0; k < SeaConstants.TierCount; k++)
        {
            Olcum o = Olc(sim, k);
            rapor.AppendLine($"   {k}   | {o.ortalamaH,9:F6} | {o.rmsH,8:F4} | {o.rmsEgim,9:F4} |" +
                             $" {o.katlanmaOrani,5:P1} | {o.ruzgarBandiOrani,5:P1}");

            // Spec §6.8: eşlenik simetri bozuksa ortalama sıfırdan uzaklaşır.
            if (Mathf.Abs(o.ortalamaH) >= 1e-3f)
            {
                rapor.AppendLine($"  KIRMIZI kademe {k}: |mean(h)| = {Mathf.Abs(o.ortalamaH):E3}" +
                                 " >= 1e-3. Eşlenik simetri bozuk (spec §6.8).");
                hata++;
            }

            // Spec §7 / plan Faz 3 Adım 3: katlanma oranı beklenen bant.
            if (o.katlanmaOrani > 0.20f)
            {
                rapor.AppendLine($"  KIRMIZI kademe {k}: J<0 oranı %{o.katlanmaOrani * 100f:F1}" +
                                 " > %20. Choppiness çok yüksek, yüzey düğümlenir.");
                hata++;
            }
        }

        rapor.AppendLine();

        // --- 2. Rüzgâr şiddeti dalga yüksekliğini artırmalı ---
        ortam.hiz = 3f; sim.Adim(0f);
        float rms3 = ToplamRms(sim);

        ortam.hiz = 15f; sim.Adim(0f);
        float rms15 = ToplamRms(sim);

        // KATLANMA FIRTINADA ÖLÇÜLÜYOR.
        //
        // U10 = 8'de deniz uysal (Hs ≈ 0.7 m) ve J<0 doğal olarak sıfır.
        // Choppiness'in gerçekten iş yaptığı ancak dik dalgada görülüyor.
        float katlanma15 = 0f;
        float enKucukJ15 = float.MaxValue;
        for (int k = 0; k < SeaConstants.TierCount; k++)
        {
            Olcum o = Olc(sim, k);
            katlanma15 = Mathf.Max(katlanma15, o.katlanmaOrani);
            enKucukJ15 = Mathf.Min(enKucukJ15, o.enKucukJ);
        }

        rapor.AppendLine($"rms(h) toplam:  U10=3 → {rms3:F4} m,  U10=15 → {rms15:F4} m," +
                         $"  oran {rms15 / Mathf.Max(rms3, 1e-6f):F2}×");

        rapor.AppendLine($"U10=15'te en yüksek J<0 oranı: %{katlanma15 * 100f:F2}," +
                         $" en küçük J = {enKucukJ15:F3}");

        // ÖLÇÜT `J < 0` DEĞİL, `min(J) < 1`.
        //
        // Plan Faz 3 "J<0 oranı %0 ise choppiness etkisizdir" diyordu; o
        // ölçüt açık okyanus için doğru ama bu deniz 12 km fetch'li ve
        // U10=15'te bile Hs ≈ 1.4 m. Ölçüldü: min(J) = 0.568 — zincir
        // çalışıyor, yüzey gerçekten kesiliyor, sadece katlanacak kadar
        // dikleşmiyor. Displacement bağlı olmasaydı min(J) tam 1.000
        // olurdu; ayırt eden sayı bu.
        //
        // Sonuç: açık denizde beyaz köpük bu havada seyrek. Kıyı köpüğü
        // (spec §13.3) baskın kaynak olacak.
        if (enKucukJ15 > 0.9f)
        {
            rapor.AppendLine($"  KIRMIZI: fırtınada min(J) = {enKucukJ15:F3} > 0.9." +
                             " Displacement türevleri Jacobian'a bağlı değil (spec §7).");
            hata++;
        }

        if (rms15 <= rms3 * 1.5f)
        {
            rapor.AppendLine("  KIRMIZI: rüzgâr beş katına çıktı, dalga yüksekliği" +
                             " 1.5 kat bile artmadı. Spektrum rüzgâra bağlı değil.");
            hata++;
        }

        // --- 3. Rüzgâr yönü dalga yönünü çevirmeli ---
        ortam.hiz = 8f;
        ortam.yon = Vector3.right;  sim.Adim(0f);
        float yonX = Olc(sim, 1).baskinYon;

        ortam.yon = Vector3.forward; sim.Adim(0f);
        float yonZ = Olc(sim, 1).baskinYon;

        float donme = Mathf.Abs(Mathf.DeltaAngle(yonX * Mathf.Rad2Deg, yonZ * Mathf.Rad2Deg));
        rapor.AppendLine($"baskın eğim yönü: +X rüzgârda {yonX * Mathf.Rad2Deg,7:F1}°," +
                         $" +Z rüzgârda {yonZ * Mathf.Rad2Deg,7:F1}°, fark {donme:F1}°");

        if (donme < 45f)
        {
            rapor.AppendLine("  KIRMIZI: rüzgâr 90° döndü, dalga yönü 45°'den az döndü." +
                             " Yönsel yayılma rüzgâra bağlı değil.");
            hata++;
        }

        // --- 4. Swell yönsel yoğunlaşmayı artırmalı ---
        ortam.yon = Vector3.right;

        float eskiSwell = ayar.swell;
        ayar.swell = 0f;    sim.Adim(0f); float bant0 = Olc(sim, 1).ruzgarBandiOrani;
        ayar.swell = 1f;    sim.Adim(0f); float bant1 = Olc(sim, 1).ruzgarBandiOrani;
        ayar.swell = eskiSwell;

        rapor.AppendLine($"rüzgâr bandı (±30°) enerji payı: swell=0 → %{bant0 * 100f:F1}," +
                         $" swell=1 → %{bant1 * 100f:F1}");

        if (bant1 <= bant0 + 0.02f)
        {
            rapor.AppendLine("  KIRMIZI: swell yönsel yoğunlaşmayı artırmadı." +
                             " Paralel dalga trenleri oluşmaz.");
            hata++;
        }

        Object.DestroyImmediate(go);

        rapor.AppendLine();
        rapor.AppendLine(hata == 0 ? "SONUÇ: geçti." : $"SONUÇ: {hata} kırmızı.");

        if (hata == 0) Debug.Log(rapor.ToString());
        else Debug.LogError(rapor.ToString());
    }

    /// Tex2DArray dilimini CPU'ya indirir. `RGBAFloat` isteniyor: yarım
    /// hassasiyet ölçümün kendi gürültüsü olurdu.
    static Color[] Oku(RenderTexture rt, int dilim, int N)
    {
        var istek = AsyncGPUReadback.Request(rt, 0, 0, N, 0, N, dilim, 1,
                                             TextureFormat.RGBAFloat);
        istek.WaitForCompletion();

        if (istek.hasError)
            throw new System.InvalidOperationException(
                "Deniz testi: GPU okuması başarısız.");

        return istek.GetData<Color>().ToArray();
    }

    static float ToplamRms(SeaSimulation sim)
    {
        float kare = 0f;
        for (int k = 0; k < SeaConstants.TierCount; k++)
        {
            float r = Olc(sim, k).rmsH;
            kare += r * r;
        }
        return Mathf.Sqrt(kare);
    }

    /// Bir kademeyi CPU'ya indirip ölçer.
    ///
    /// `Graphics.CopyTexture` + `GetPixels` KULLANILMIYOR. O ikisi farklı
    /// belleği konuşuyor: kopya GPU tarafını günceller, `GetPixels` CPU
    /// tarafını okur. Sonuç sessizce sabit çıkıyor — ilk ölçümde bütün
    /// kademeler aynı -23.20 değerini verdi ve bu shader hatası sanıldı.
    static Olcum Olc(SeaSimulation sim, int kademe)
    {
        int N = SeaConstants.FftSize;

        Color[] d = Oku(sim.Displacement, kademe, N);
        Color[] e = Oku(sim.Derivatives, kademe, N);

        double toplamH = 0, kareH = 0, kareEgim = 0;
        int katlanan = 0;
        float enKucukJ = float.MaxValue;

        // BASKIN YÖN KOVARYANSTAN, ORTALAMADAN DEĞİL.
        //
        // Dalga eğimi işaret bakımından simetrik: tepe kadar çukur var, o
        // yüzden sx'in ortalaması rüzgâr ne olursa olsun sıfıra gidiyor.
        // Yön ancak eğim kovaryansının ana ekseninden çıkıyor.
        double Sxx = 0, Szz = 0, Sxz = 0;
        double bantIci = 0, bantToplam = 0;

        for (int i = 0; i < d.Length; i++)
        {
            float h = d[i].g;
            toplamH += h;
            kareH += (double)h * h;

            if (d[i].a < 0f) katlanan++;
            enKucukJ = Mathf.Min(enKucukJ, d[i].a);

            float sx = e[i].r;
            float sz = e[i].g;
            double enerji = (double)sx * sx + (double)sz * sz;
            kareEgim += enerji;

            Sxx += (double)sx * sx;
            Szz += (double)sz * sz;
            Sxz += (double)sx * sz;

            float uzunluk = Mathf.Sqrt((float)enerji);
            if (uzunluk > 1e-5f)
            {
                bantToplam += enerji;

                // Rüzgâr +X iken bant |theta| < 30°; yön testinde bu ölçüm
                // kullanılmıyor, swell testinde rüzgâr hep +X.
                float cos = Mathf.Abs(sx) / uzunluk;
                if (cos > 0.8660254f) bantIci += enerji;
            }
        }

        return new Olcum
        {
            ortalamaH = (float)(toplamH / d.Length),
            rmsH = Mathf.Sqrt((float)(kareH / d.Length)),
            rmsEgim = Mathf.Sqrt((float)(kareEgim / d.Length)),
            katlanmaOrani = katlanan / (float)d.Length,
            enKucukJ = enKucukJ,
            ruzgarBandiOrani = bantToplam > 0 ? (float)(bantIci / bantToplam) : 0f,
            baskinYon = 0.5f * Mathf.Atan2((float)(2.0 * Sxz), (float)(Sxx - Szz)),
        };
    }
}
