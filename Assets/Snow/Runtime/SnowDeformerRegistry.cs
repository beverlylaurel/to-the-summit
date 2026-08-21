// ROL: etkin deformer'ları sabit boyutlu bir dizide tutar ve her karede GPU tamponunu
// doldurur. Liste büyümüyor, yer ayırma yok (§5.1).
// Çağıran: SnowDeformer (kayıt), SnowManager (tamponu okur).

using Unity.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SnowDeformerRegistry : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    SnowDeformer[] slots;

    /// Boş yuvaların indeksleri. `List` değil — yığın olarak kullanılan sabit dizi.
    int[] freeSlots;
    int freeCount;

    NativeArray<SnowDeformerGPU> cpu;
    ComputeBuffer buffer;

    public ComputeBuffer Buffer => buffer;

    /// Bu karede tampona yazılan deformer sayısı.
    public int ActiveCount { get; private set; }

    /// Etkin deformer'ların en büyük temas kutusu kenarı, metre. Tarama kutusu
    /// bundan türüyor: hepsine aynı kutuyu vermek en büyüğünü kapsamak zorunda.
    public float MaxContactExtent { get; private set; }

    public int Capacity => slots != null ? slots.Length : 0;

    /// Teşhis: kapasitenin nereden geldiği. Rapora yazılıyor.
    public static string LastCapacityReading = "okunmadı";

    void OnEnable() => EnsureInitialized();

    /// GEÇ BAŞLATMA. Deformer'lar BAŞKA nesnelerde (oyuncunun ayaklarında) ve onların
    /// OnEnable'sı bu bileşenden ÖNCE koşabiliyor — Unity ayrı nesneler arasında sıra
    /// garanti etmiyor. Kayıt sırasında da çağrılıyor, böylece hangisi önce gelirse
    /// gelsin yuvalar hazır oluyor.
    void EnsureInitialized()
    {
        if (slots != null) return;

        if (settings == null)
            throw new System.InvalidOperationException("SnowDeformerRegistry: SnowSettings atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        int capacity = settings.QualityData.MaxDeformers;

        // ÖLÇÜM: kapasite sıfır çıkıyor ve sorumlu ayar asset'i mi kalite tablosu mu
        // koddan ayrılamıyor. İkisi de yazılıyor.
        LastCapacityReading = settings.name + " / " + settings.Quality + " / " + capacity;

        if (capacity <= 0)
            throw new System.InvalidOperationException(
                "SnowDeformerRegistry: kalite tablosu sıfır kapasite verdi — " + LastCapacityReading);

        slots = new SnowDeformer[capacity];
        freeSlots = new int[capacity];

        // Yığın tersten doldurulıyor ki ilk kayıt 0. yuvayı alsın.
        for (int i = 0; i < capacity; i++)
            freeSlots[i] = capacity - 1 - i;

        freeCount = capacity;

        cpu = new NativeArray<SnowDeformerGPU>(capacity, Allocator.Persistent,
                                               NativeArrayOptions.ClearMemory);

        buffer = new ComputeBuffer(capacity, SnowConstants.DeformerStride,
                                   ComputeBufferType.Structured);
    }

    void OnDisable()
    {
        // Serbest bırakılmazsa Unity uyarı basıyor ve bu bir hatadır, susturulmaz (§11.3).
        buffer?.Release();
        buffer = null;

        if (cpu.IsCreated) cpu.Dispose();

        slots = null;
        freeSlots = null;
        freeCount = 0;
        ActiveCount = 0;
        MaxContactExtent = 0f;
    }

    public int Register(SnowDeformer deformer)
    {
        EnsureInitialized();

        if (freeCount == 0)
            throw new System.InvalidOperationException(
                "SnowDeformerRegistry: yuva kalmadı (" + Capacity + "). Kalite seviyesi " +
                "MAX_DEFORMERS'ı belirliyor.");

        int handle = freeSlots[--freeCount];
        slots[handle] = deformer;

        return handle;
    }

    public void Unregister(int handle)
    {
        if (slots == null || handle < 0 || handle >= slots.Length) return;

        slots[handle] = null;
        freeSlots[freeCount++] = handle;
    }

    /// GPU tamponunu doldurur. SnowManager'dan ÖNCE koşmalı; script sırası yerine
    /// SnowManager kendi LateUpdate'inde bunu çağırıyor.
    public void Collect()
    {
        if (slots == null) return;

        int count = 0;
        float maxExtent = 0f;

        for (int i = 0; i < slots.Length; i++)
        {
            SnowDeformer deformer = slots[i];
            if (deformer == null || deformer.Strength <= 0f) continue;

            SnowDeformerGPU gpu = deformer.ToGPU();
            cpu[count++] = gpu;

            // Köşegen: damga deformer'ın yerel uzayında dönebiliyor, kutu en kötü
            // hâli kapsamalı.
            float extent = Mathf.Sqrt(gpu.sizeXZ.x * gpu.sizeXZ.x + gpu.sizeXZ.y * gpu.sizeXZ.y);
            if (extent > maxExtent) maxExtent = extent;
        }

        ActiveCount = count;
        MaxContactExtent = maxExtent;

        if (count > 0) buffer.SetData(cpu, 0, 0, count);
    }
}
