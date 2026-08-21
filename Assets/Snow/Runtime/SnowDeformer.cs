// ROL: kara basan tek bir temas noktası. Bot, toynak, tekerlek, düşen gövde.
// Çağıran: SnowDeformerRegistry (her karede okur), SnowFootstepDriver (sürer).

using System.Runtime.InteropServices;
using UnityEngine;

/// GPU'ya giden temas kaydı. 64 bayt — düzen DEĞİŞMEYECEK, HLSL tarafındaki
/// `SnowDeformer` yapısıyla birebir aynı.
[StructLayout(LayoutKind.Sequential)]
public struct SnowDeformerGPU
{
    public Vector2 posXZ;      //  0  dünya XZ
    public Vector2 sizeXZ;     //  8  temas kutusu boyutu, metre
    public Vector2 dirXZ;      // 16  (cos yaw, sin yaw) — önceden hesaplanmış
    public Vector2 velXZ;      // 24  m/s, kenar yığılması yönü için
    public float loadN;        // 32  bu temasa binen kuvvet, Newton
    public float posY;         // 36  temas noktası dünya Y'si
    public float maxSink;      // 40  bu deformer'ın batabileceği azami derinlik, m
    public float strength;     // 44  0..1, kalkış/iniş yumuşatması
    public uint shapeId;       // 48  stamp atlas dilimi
    public uint flags;         // 52  bit0 = sürekli/pulluk, bit1 = eritir
    public Vector2 pad;        // 56
}

/// Damga atlası dilimleri (§5.2). Sıra `kStampAreaFrac` ile aynı olmak zorunda.
public enum SnowDeformerShape
{
    Circle = 0,
    BootLeft = 1,
    BootRight = 2,
    Hoof = 3,
    Wheel = 4,
    Body = 5,
}

[DisallowMultipleComponent]
public class SnowDeformer : MonoBehaviour
{
    public const float Gravity = 9.81f;

    [SerializeField] SnowDeformerShape shape = SnowDeformerShape.Circle;

    [Tooltip("Temas kutusunun boyutu, metre. §5.2 tablosundaki varsayılanlar.")]
    [SerializeField] Vector2 contactSize = new Vector2(0.20f, 0.20f);

    [Tooltip("Bu temasa binen kütle, kg. loadN = loadKg * 9.81.")]
    [SerializeField] float loadKg = 82f;

    [Tooltip("Bu deformer'ın batabileceği azami derinlik, metre. Bacak boyu sınırı.")]
    [SerializeField] float maxSink = 0.45f;

    [Tooltip("Kayıt defteri. Elle yerleştirilen deformer'lar için Inspector'dan verilir.")]
    [SerializeField] SnowDeformerRegistry registry;

    int handle = -1;

    /// 0 = temas yok, 1 = tam yük. Sürücü kalkış ve inişte yumuşatıyor.
    public float Strength { get; set; }

    /// Temas noktasının yatay hızı, m/s. Kenar yığılması bu yöne kayıyor.
    public Vector2 Velocity { get; set; }

    /// bit0 sürekli/pulluk, bit1 eritir.
    public uint Flags { get; set; }

    public SnowDeformerShape Shape => shape;
    public Vector2 ContactSize => contactSize;
    public float LoadKg => loadKg;
    public float MaxSink => maxSink;

    /// Çalışma zamanında üretilen deformer'lar için. OnEnable'dan ÖNCE çağrılmalı.
    public void Bind(SnowDeformerRegistry target, SnowDeformerShape deformerShape,
                     Vector2 size, float mass, float sinkLimit)
    {
        registry = target;
        shape = deformerShape;
        contactSize = size;
        loadKg = mass;
        maxSink = sinkLimit;
    }

    void OnEnable()
    {
        if (registry == null)
            throw new System.InvalidOperationException("SnowDeformer: kayıt defteri atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        handle = registry.Register(this);
    }

    void OnDisable()
    {
        if (handle < 0) return;

        registry.Unregister(handle);
        handle = -1;
    }

    /// Bileşenin o anki hâlini GPU kaydına çevirir.
    public SnowDeformerGPU ToGPU()
    {
        Transform t = transform;
        Vector3 position = t.position;

        float yaw = t.eulerAngles.y * Mathf.Deg2Rad;

        return new SnowDeformerGPU
        {
            posXZ = new Vector2(position.x, position.z),
            sizeXZ = contactSize,
            dirXZ = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw)),
            velXZ = Velocity,
            loadN = loadKg * Gravity,
            posY = position.y,
            maxSink = maxSink,
            strength = Mathf.Clamp01(Strength),
            shapeId = (uint)shape,
            flags = Flags,
            pad = Vector2.zero,
        };
    }
}
