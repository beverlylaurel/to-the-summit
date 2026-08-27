// ROL: dalga alanini uretir. Spektrum ve IFFT compute'larini surer, sonucu
// global doku olarak yayinlar.
// Cagiran: yok — kendi basina calisiyor, bagimliliklari Inspector'dan.

using System;
using UnityEngine;
using UnityEngine.Rendering;

/// DALGA ALANI HER FRAME, SPEKTRUM SADECE RÜZGÂR DEĞİŞİNCE.
///
/// `KInitialSpectrum` pahalı ve rüzgâr sabitken sonucu değişmiyor; her
/// frame çalıştırmak boşuna (spec §15.2). Eşikler orada verildi:
/// hız 0.25 m/s, yön 3°.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaSimulation : MonoBehaviour
{
    [SerializeField] SeaSettings settings;
    [SerializeField] SeaEnvironmentBridge environment;
    [SerializeField] ComputeShader spectrumShader;
    [SerializeField] ComputeShader fftShader;

    ISeaEnvironmentSource env;

    RenderTexture h0;
    RenderTexture spectrumHt;
    RenderTexture spectrumSlope;
    RenderTexture displacement;
    RenderTexture derivatives;

    int kInitial = -1;
    int kTime = -1;
    int kFftH = -1;
    int kFftV = -1;
    int kAssemble = -1;

    float lastWindSpeed = float.NaN;
    Vector3 lastWindDir = Vector3.zero;
    Vector4 lastSpectrumAyar = Vector4.zero;

    /// Faz 2 sayısal doğrulaması bu dokuyu okuyor.
    public RenderTexture Displacement => displacement;
    public RenderTexture Derivatives => derivatives;
    public RenderTexture H0 => h0;

    public void Bind(SeaSettings source, SeaEnvironmentBridge bridge,
                     ComputeShader spectrum, ComputeShader fft)
    {
        environment = bridge;
        Bind(source, (ISeaEnvironmentSource)bridge, spectrum, fft);
    }

    /// ARAYÜZ ÜZERİNDEN BAĞLAMA.
    ///
    /// `ISeaEnvironmentSource` tam da bunun için var: rüzgârı bilinen bir
    /// değere sabitleyip dalga alanını ölçebilmek. Sayısal doğrulama
    /// (`SeaSpectrumTest`) bu yolu kullanıyor — rüzgâr hava sisteminden
    /// gelseydi ölçüm tekrarlanabilir olmazdı.
    public void Bind(SeaSettings source, ISeaEnvironmentSource bridge,
                     ComputeShader spectrum, ComputeShader fft)
    {
        settings = source;
        env = bridge;
        spectrumShader = spectrum;
        fftShader = fft;
    }

    void OnEnable()
    {
        // `Bind` arayüz üzerinden çağrıldıysa serileştirilmiş köprü boş
        // olabilir; o durumda üzerine yazılmıyor.
        if (env == null) env = environment;

        if (env == null)
        {
            Debug.LogError($"{nameof(SeaSimulation)}: {nameof(environment)} atanmadı. " +
                           "Dalga alanı üretilmiyor.");
            enabled = false;
            return;
        }

        if (settings == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(settings)} atanmadı.");
        if (spectrumShader == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(spectrumShader)} atanmadı.");
        if (fftShader == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(fftShader)} atanmadı.");

        // KERNEL VARLIĞI AÇIKÇA SINANIYOR.
        //
        // `GetComputeShaderMessages` boş dönerken `FindKernel` yine de
        // patlayabiliyor — kar sisteminde bir tur bu yüzden yandı. Hata
        // yutulmuyor, doğrudan fırlıyor.
        kInitial = spectrumShader.FindKernel("KInitialSpectrum");
        kTime = spectrumShader.FindKernel("KTimeSpectrum");
        kFftH = fftShader.FindKernel("KIFFTHorizontal");
        kFftV = fftShader.FindKernel("KIFFTVertical");
        kAssemble = fftShader.FindKernel("KAssemble");

        DokulariKur();

        // Rüzgâr eşiği ilk karede kesin tetiklensin.
        lastWindSpeed = float.NaN;
    }

    void OnDisable()
    {
        DokulariBirak();
    }

    /// `GetTemporary` KULLANILMIYOR (spec §15.2). Dokular bir kez kuruluyor,
    /// `OnDisable`'da bırakılıyor.
    RenderTexture Kur(string ad, RenderTextureFormat format)
    {
        var rt = new RenderTexture(SeaConstants.FftSize, SeaConstants.FftSize, 0, format)
        {
            name = ad,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,

            // ZORUNLU: mesh yüzeyi dünya koordinatından örneklüyor ve yama
            // sınırını geçince doku tekrar etmeli (spec §10.4).
            wrapMode = TextureWrapMode.Repeat,

            useMipMap = false,
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = SeaConstants.TierCount,
            hideFlags = HideFlags.DontSave,
        };

        rt.Create();
        return rt;
    }

    void DokulariKur()
    {
        DokulariBirak();

        // Yarım hassasiyet FFT için yeterli; `Float` iki kat bant genişliği
        // ve görsel fark yok (spec §6.8).
        h0 = Kur("Sea_H0", RenderTextureFormat.ARGBHalf);
        spectrumHt = Kur("Sea_SpectrumHt", RenderTextureFormat.ARGBHalf);
        spectrumSlope = Kur("Sea_SpectrumSlope", RenderTextureFormat.ARGBHalf);
        displacement = Kur("Sea_Displacement", RenderTextureFormat.ARGBHalf);
        derivatives = Kur("Sea_Derivatives", RenderTextureFormat.ARGBHalf);
    }

    void DokulariBirak()
    {
        Birak(ref h0);
        Birak(ref spectrumHt);
        Birak(ref spectrumSlope);
        Birak(ref displacement);
        Birak(ref derivatives);
    }

    static void Birak(ref RenderTexture rt)
    {
        if (rt == null) return;

        rt.Release();
        if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
        rt = null;
    }

    void Update()
    {
        if (env == null || settings == null) return;

        if (displacement == null || !displacement.IsCreated())
            DokulariKur();

        Adim(Application.isPlaying ? Time.time : 0f);
    }

    /// Bir simülasyon adımı. Editör testi de bunu çağırıyor.
    public void Adim(float zaman)
    {
        Vector3 yon = env.WindDirection;
        float hiz = env.WindSpeed;

        // RÜZGÂR TEK GİRDİ DEĞİL.
        //
        // Spec §15.2 yalnız rüzgâr eşiğini veriyor ama `h0` swell, fetch,
        // derinlik ve kesme uzunluğuna da bağlı. Yalnız rüzgâra bakılırsa
        // Inspector'dan swell değiştirmek hiçbir şey yapmıyor — ölçüldü,
        // swell 0 ile 1 arasında yönsel yoğunlaşma birebir aynı çıktı.
        Vector4 ayarImza = new Vector4(settings.swell, settings.fetch,
                                       settings.spectrumDepth,
                                       settings.smallWaveCutoff);

        bool kirli = float.IsNaN(lastWindSpeed)
                  || Mathf.Abs(hiz - lastWindSpeed) > 0.25f
                  || Vector3.Angle(yon, lastWindDir) > 3f
                  || ayarImza != lastSpectrumAyar;

        AyarlariYaz(spectrumShader, yon, hiz);
        AyarlariYaz(fftShader, yon, hiz);

        if (kirli)
        {
            BaslangicSpektrumu();
            lastWindSpeed = hiz;
            lastWindDir = yon;
            lastSpectrumAyar = ayarImza;
        }

        // DÖNGÜ KUANTİZE ZAMAN. `Time.time` doğrudan verilirse uzun
        // oturumda float hassasiyeti kayboluyor (spec §6.5).
        spectrumShader.SetFloat(SeaShaderIDs.SeaTime,
                                Mathf.Repeat(zaman, settings.loopPeriod));

        int grup = SeaConstants.FftSize / 8;

        spectrumShader.SetTexture(kTime, SeaShaderIDs.H0RW, h0);
        spectrumShader.SetTexture(kTime, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        spectrumShader.SetTexture(kTime, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);
        spectrumShader.Dispatch(kTime, grup, grup, SeaConstants.TierCount);

        FftGecisi(kFftH);
        FftGecisi(kFftV);

        fftShader.SetTexture(kAssemble, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.DisplacementRW, displacement);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.DerivativesRW, derivatives);
        fftShader.Dispatch(kAssemble, grup, grup, SeaConstants.TierCount);

        Shader.SetGlobalTexture(SeaShaderIDs.Displacement, displacement);
        Shader.SetGlobalTexture(SeaShaderIDs.Derivatives, derivatives);
    }

    void BaslangicSpektrumu()
    {
        int grup = SeaConstants.FftSize / 8;

        spectrumShader.SetTexture(kInitial, SeaShaderIDs.H0RW, h0);
        spectrumShader.Dispatch(kInitial, grup, grup, SeaConstants.TierCount);
    }

    void FftGecisi(int kernel)
    {
        fftShader.SetTexture(kernel, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        fftShader.SetTexture(kernel, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);

        // Grup başına bir satır; iş parçacığı sayısı satır uzunluğu kadar.
        fftShader.Dispatch(kernel, 1, SeaConstants.FftSize, SeaConstants.TierCount);
    }

    /// COMPUTE SHADER GLOBALLERİ AYRI YAZILIYOR.
    ///
    /// `Shader.SetGlobal*` compute shader'a güvenilir biçimde ulaşmıyor;
    /// değerler doğrudan compute'a yazılıyor. `SeaManager`'ın yayınladığı
    /// globaller yüzey shader'ı için.
    void AyarlariYaz(ComputeShader cs, Vector3 yon, float hiz)
    {
        Vector3 w = yon * hiz;
        cs.SetVector(SeaShaderIDs.SeaWindWS, new Vector4(w.x, w.z, 0f, 0f));

        cs.SetVector(SeaShaderIDs.PatchSizes, settings.patchSizes);
        cs.SetVector(SeaShaderIDs.ChoppinessPerTier, settings.choppinessPerTier);
        cs.SetFloat(SeaShaderIDs.Choppiness, settings.choppiness);
        cs.SetFloat(SeaShaderIDs.SpectrumDepth, settings.spectrumDepth);
        cs.SetFloat(SeaShaderIDs.Fetch, settings.fetch);
        cs.SetFloat(SeaShaderIDs.Swell, settings.swell);
        cs.SetFloat(SeaShaderIDs.SmallWaveCutoff, settings.smallWaveCutoff);
        cs.SetFloat(SeaShaderIDs.LoopPeriod, settings.loopPeriod);

        cs.SetVector(SeaShaderIDs.TierCutoffK, KademeSinirlari());
    }

    /// KADEME BANDI SINIRLARI.
    ///
    /// Üç kademe aynı `k` aralığını taşısa enerji üç kez sayılırdı. Kural:
    /// bir kademe, yamasına en az dört tam periyot sığan dalga boyunu
    /// taşıyor (λ ≤ L/4); daha uzunu bir kaba kademeye devrediliyor.
    ///
    ///   kademe 0: λ > 32 m       (k < 0.196)
    ///   kademe 1: λ 6 – 32 m     (k 0.196 – 1.047)
    ///   kademe 2: λ < 6 m        (k > 1.047)
    ///
    /// En kaba kademenin uzun ucu sınırlanmıyor — üstünde kademe yok.
    /// Dört [KALİBRASYON].
    Vector4 KademeSinirlari()
    {
        Vector3 L = settings.patchSizes;

        float s0 = 4f * SeaConstants.TwoPi / Mathf.Max(L.y, 1f);
        float s1 = 4f * SeaConstants.TwoPi / Mathf.Max(L.z, 1f);

        return new Vector4(s0, s1, 1e9f, 0f);
    }
}
