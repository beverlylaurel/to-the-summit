// ROL: izi iki ayrı ayak izi olarak açar. Her ayak üç kapsül: topuk, bel,
// ön taban. Ayak basış anında dünyada sabitleniyor, kalkana kadar orada.
// Çağıran: SnowDeformerRegistry (kayıt), SnowManager (parça tamponu).

using UnityEngine;

/// AYAK İZİ HER YERİ EŞİT DERİN DEĞİL.
///
/// [KAYNAK: ayak izi çizim rehberleri — "a foot would push deeper into only
/// SOME of the snow while stepping", "distinct toes and heel marks".]
/// Ağırlık topuğa ve ön tabana biniyor; aradaki bel (arch) yere neredeyse
/// değmiyor ve orada kar sığ kalıyor. Tek tip bir kapsül bu yüzden yapay
/// okunuyor.
///
/// Her ayak ÜÇ KAPSÜL:
///   ön taban — geniş, derin
///   bel      — dar, SIĞ
///   topuk    — orta genişlik, derin
///
/// ÖNCEKİ ÜÇ DENEME VE NEDEN BAŞARISIZ OLDUKLARI:
///
/// 1. Adım olayında tek damga. İz yarım adımda bir (39 cm) BİRDEN
///    beliriyordu: damga karakter o mesafeyi aldıktan SONRA basılıyor
///    (kullanıcı: "Minecraft'ta blok koyar gibi, gecikmeli").
/// 2. İki hattı sürekli süpürmek. Ayaklar hiç yerden kalkmıyor, iki paralel
///    oluk çıktı (kullanıcı: "ip ile iki ayağım birbirine bağlanmış").
/// 3. Sabit ayak + bacağın yarma izi. Gecikme çözüldü ama her adım HALTER
///    şekli verdi: iki oval, aralarında yarma izinden gelen ince çubuk. Yan
///    yürürken o çubuk düz bir çizgiye dönüyordu.
///
/// Buradaki yol 3'ün düzeltilmişi: yarma izi YOK (halterin çubuğu oydu),
/// ayak tek kapsül değil üç kapsül (topakların yerine gerçek bot silueti).
/// Konum basış anında donuyor ama İZ o andan itibaren HER KARE yazılıyor —
/// gecikmeyi kapatan şey bu.
[DisallowMultipleComponent]
public class SnowFootprintDeformer : SnowDeformer
{
    [Header("Kaynak")]
    [Tooltip("Adım olayının okunduğu ritim.")]
    [SerializeField] SnowStepRhythm rhythm;

    [Header("Duruş")]
    [Tooltip("İki ayağın merkezleri arası mesafe (m). İnsan duruşunda ~0.20.")]
    [SerializeField, Min(0.02f)] float stanceWidth = 0.20f;

    [Tooltip("Bot tabanının uzunluğu (m).")]
    [SerializeField, Min(0.05f)] float bootLength = 0.30f;

    [Tooltip("Bot tabanının genişliği (m).")]
    [SerializeField, Min(0.03f)] float bootWidth = 0.11f;

    [Tooltip("Ayak ucunun gidiş yönünden dışa dönme açısı (derece).")]
    [SerializeField, Range(0f, 20f)] float toeOut = 7f;

    /// BOT TABANININ ÜÇ BÖLÜMÜ.
    ///
    /// Değerler bot uzunluğunun ve genişliğinin oranı. Ölçüler gerçek bir
    /// tabandan: ön taban en geniş yer, bel belirgin biçimde dar, topuk
    /// ikisinin arasında.
    ///   x = merkezin ayak ekseni üzerindeki yeri (boy oranı, + ileri)
    ///   y = bölümün boyu (boy oranı)
    ///   z = yarıçap (genişliğin yarısına oran)
    ///   w = batma payı
    static readonly Vector4[] Bolumler =
    {
        new(+0.30f, 0.44f, 1.00f, 1.00f),   // ön taban
        new(+0.02f, 0.26f, 0.62f, 0.45f),   // bel — SIĞ
        new(-0.31f, 0.34f, 0.84f, 0.95f),   // topuk
    };

    struct Ayak
    {
        public Vector3 konum;
        public Vector2 ileri;
        public bool    basili;
    }

    Ayak sol, sag;

    public override int SegmentCount => Bolumler.Length * 2;

    public override void GetSegment(int index, out Vector4 a, out Vector4 b)
    {
        // Taban sınıf yol dalgalanmasını veriyor (genişlik ve derinlik).
        base.GetSegment(index, out Vector4 tabanA, out Vector4 tabanB);

        Ayak ayak = index < Bolumler.Length ? sol : sag;
        Vector4 bol = Bolumler[index % Bolumler.Length];

        float yaricap = bootWidth * 0.5f * bol.z * (tabanA.w / Mathf.Max(Radius, 1e-4f));
        float yari    = Mathf.Max(0f, bootLength * bol.y * 0.5f - yaricap);

        var ileri3 = new Vector3(ayak.ileri.x, 0f, ayak.ileri.y);
        Vector3 orta = ayak.konum + ileri3 * (bootLength * bol.x);

        Vector3 pa = orta - ileri3 * yari;
        Vector3 pb = orta + ileri3 * yari;

        // Havadaki ayak iz bırakmıyor: batma çarpanı sıfır, `KDeform` orada
        // hiçbir şey yazmıyor.
        float basinc = ayak.basili ? tabanB.w * bol.w : 0f;

        a = new Vector4(pa.x, pa.y, pa.z, yaricap);
        b = new Vector4(pb.x, pb.y, pb.z, basinc);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Yerlestir(ref sol, true);
        Yerlestir(ref sag, false);

        // Dururken iki ayak da yerde.
        sol.basili = true;
        sag.basili = true;

        if (rhythm != null) rhythm.Stepped += Bas;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (rhythm != null) rhythm.Stepped -= Bas;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (rhythm == null) return;

        // DURUNCA İKİ AYAK DA YERE İNİYOR. Ritim hız eşiğinin altında fazı
        // sıfırlıyor; orada havada asılı bir ayak bırakmak yanlış olur.
        if (rhythm.Speed <= 0.001f)
        {
            if (!sol.basili) Yerlestir(ref sol, true);
            if (!sag.basili) Yerlestir(ref sag, false);

            sol.basili = true;
            sag.basili = true;
        }
    }

    /// Adım düştü: bu ayak yere basıyor, öteki kalkıyor.
    void Bas(int ayak)
    {
        if (ayak == 0)
        {
            Yerlestir(ref sol, true);
            sol.basili = true;
            sag.basili = false;
        }
        else
        {
            Yerlestir(ref sag, false);
            sag.basili = true;
            sol.basili = false;
        }
    }

    /// Ayağı gövdenin yanına, gidiş yönüne göre yerleştirir.
    ///
    /// YÖN HIZDAN, BAKIŞTAN DEĞİL. Oyuncu yana kayarken (A/D) gövde ileri
    /// bakmaya devam ediyor ama ayaklar gidiş yönüne dönüyor. Bakışa
    /// bağlansaydı yan yürüyüşte izler yürüyüş hattına dik çıkardı.
    void Yerlestir(ref Ayak ayak, bool solMu)
    {
        Vector3 ileri3 = new Vector3(VelocityXZ.x, 0f, VelocityXZ.y);

        if (ileri3.sqrMagnitude < 1e-4f)
        {
            ileri3 = transform.forward;
            ileri3.y = 0f;
        }

        if (ileri3.sqrMagnitude < 1e-6f) ileri3 = Vector3.forward;
        ileri3.Normalize();

        var sag3 = new Vector3(ileri3.z, 0f, -ileri3.x);

        // Ayak ucu dışa dönük: sol ayak sola, sağ ayak sağa.
        float rad = (solMu ? -toeOut : toeOut) * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        ayak.konum = transform.position + sag3 * (solMu ? -0.5f : 0.5f) * stanceWidth;
        ayak.ileri = new Vector2(ileri3.x * c + ileri3.z * s,
                                 -ileri3.x * s + ileri3.z * c).normalized;
    }
}
