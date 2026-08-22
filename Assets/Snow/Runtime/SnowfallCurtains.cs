// ROL: yağışın uzak katmanı — kameraya kilitli üç perde (spec §17.2).
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;
using UnityEngine.Rendering;

/// SEYREK PARÇACIK + ARALARINI DOLDURAN DOKU
/// `[KAYNAK: Langer ve ark., EGSR 2004]`.
///
/// Yakın katman (`VFX_Snowfall`) tek tek taneleri çiziyor. 20 metrede 2 cm'lik
/// bir tane ekranda yarım pikselin altına düşüyor; asgari ekran boyutu onu
/// kaybolmaktan kurtarıyor ama görünür kılmıyor. Uzaktaki karın hacmi bu
/// katmandan geliyor.
///
/// Spec §17.2 bunu ayrıca uyarıyor: bu katman olmadan aynı yoğunluk için
/// parçacık sayısını 3–4 katına çıkarmak gerekir; makale tam olarak bunu
/// önlemek için yazılmıştır.
///
/// BU BİR PARÇACIK SİSTEMİ DEĞİL. Spawn yok, ömür yok: üç sabit quad ve kayan
/// UV. `SnowCurtainController` ile karıştırılmamalı — o §18.7'nin SAVRULMA
/// perdeleri, tetiği rüzgâr; bu §17.2'nin YAĞIŞ perdeleri, tetiği yağış
/// şiddeti.
[DisallowMultipleComponent]
public class SnowfallCurtains : MonoBehaviour
{
    /// Spec §17.2: mesafeler 18 m, 32 m, 55 m.
    static readonly float[] Distances = { 18f, 32f, 55f };

    /// Spec §17.2 `[KALİBRASYON]`: layerAlpha 0.10 / 0.07 / 0.05.
    static readonly float[] LayerAlpha = { 0.10f, 0.07f, 0.05f };

    /// Spec §17.2: `_ScrollSpeed` katman mesafesiyle TERS orantılı — yakın
    /// katman hızlı kayar. Perspektifin dispersiyon ilişkisinin karşılığı.
    /// Taban hız 18 m'deki katman için; ötekiler mesafeye bölünüyor.
    const float ScrollBase = 4.5f;

    /// Spec §17.2: `uv += _WindWS.xz * 0.12 * time`.
    const float WindUvScale = 0.12f;

    /// Spec §17.2: şiddet bunun altındaysa katman devre dışı.
    const float MinIntensity = 0.05f;

    /// Spec §17.2: `alpha *= 1 - _FogDensity01 * 0.6`. Sis zaten uzağı
    /// örtüyor; perde de üstüne binerse uzak taraf kapkara oluyor.
    const float FogFade = 0.6f;

    /// Dokunun quad üzerinde kaç kez döşendiği. KATMAN BAŞINA AYRI.
    ///
    /// Tane ekranda kaç piksel: doku 512², tane ~4 piksel; quad ekranın
    /// yüksekliğini (h piksel) kaplıyor. Döşeme t iken tane ekranda
    /// `(4 / 512) * (h / t)` piksel. 888 piksellik bir görünümde:
    ///
    ///   t = 2   -> 3.5 px      t = 3   -> 2.3 px      t = 6 -> 1.2 px
    ///
    /// İlk deneme üçünde de t = 6 kullandı ve tane 1.2 piksele düştü: ekranda
    /// tane değil düz bir tül göründü (ölçüldü).
    ///
    /// Uzak katman daha sık döşeniyor — uzaktaki kar gözde daha küçük.
    static readonly float[] Tilings = { 2f, 3f, 4.5f };

    [Header("Bağımlılıklar")]
    [Tooltip("Perde malzemesi (Snow/SnowfallCurtain).")]
    [SerializeField] Material curtainMaterial;

    [Tooltip("Perdelerin kilitlendiği kamera.")]
    [SerializeField] Camera view;

    [Tooltip("Rüzgâr ve sis yoğunluğunu okuyan köprü.")]
    [SerializeField] SnowEnvironmentBridge environment;

    /// Teşhis: o anki katman sayısı (0 veya 3).
    public int Alive { get; private set; }

    /// Teşhis: en yakın katmanın efektif alpha'sı.
    public float NearAlpha { get; private set; }

    Mesh quad;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        block = new MaterialPropertyBlock();
        quad = BuildQuad();
    }

    void OnDisable()
    {
        if (quad != null) Destroy(quad);
        quad = null;
    }

    void LateUpdate()
    {
        Alive = 0;
        NearAlpha = 0f;

        if (curtainMaterial == null || view == null || quad == null) return;

        float i01 = SnowRuntimeState.SnowfallIntensity01;
        if (i01 < MinIntensity) return;

        float fog = environment != null ? environment.FogDensity01 : 0f;
        float fade = 1f - fog * FogFade;

        Vector2 windUv = Vector2.zero;

        if (environment != null)
        {
            Vector3 w = environment.WindDirection * environment.WindSpeed;
            windUv = new Vector2(w.x, w.z) * WindUvScale;
        }

        Transform cam = view.transform;
        float tanHalf = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < Distances.Length; i++)
        {
            float d = Distances[i];

            // KAMERANIN FOV'UNU TAM KAPLIYOR (spec §17.2). Yükseklik
            // `2 * d * tan(fov/2)`, genişlik en-boy oranıyla. Sabit boy
            // verilseydi fov değişince kenar görünürdü.
            float h = 2f * d * tanHalf;
            float w = h * view.aspect;

            // Kenarda kırpılma olmasın diye küçük bir pay: kamera dönerken
            // quad tam sınırda titriyor.
            var scale = new Vector3(w * 1.05f, h * 1.05f, 1f);

            Matrix4x4 m = Matrix4x4.TRS(
                cam.position + cam.forward * d,
                cam.rotation,
                scale);

            block.SetFloat(SnowShaderIDs.CurtainLayerAlpha, LayerAlpha[i] * i01 * fade);
            block.SetFloat(SnowShaderIDs.CurtainScrollSpeed, ScrollBase / d);
            block.SetFloat(SnowShaderIDs.CurtainTiling, Tilings[i]);
            block.SetVector(SnowShaderIDs.CurtainWindUv, windUv);

            var rp = new RenderParams(curtainMaterial)
            {
                worldBounds = new Bounds(cam.position, Vector3.one * (d * 4f)),
                matProps = block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            Graphics.RenderMesh(rp, quad, 0, m);

            if (i == 0) NearAlpha = LayerAlpha[0] * i01 * fade;
        }

        Alive = Distances.Length;
    }

    /// Birim quad, XY düzleminde, merkezde. Ölçek matristen geliyor.
    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "SnowfallCurtainQuad" };

        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };

        m.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        };

        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();

        return m;
    }
}
