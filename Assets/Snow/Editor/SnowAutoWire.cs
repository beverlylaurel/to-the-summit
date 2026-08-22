// ROL: kar sisteminin sahne kurulumunu kendiliğinden koşturur.
// Çağıran: Unity (domain reload ve Play'e girişte).

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// KURULUMA TIKLAMAK KULLANICININ İŞİ DEĞİL.
///
/// `CLAUDE.md`: "Claude bir şeyi otomatikleştirebiliyorsa otomatikleştirir.
/// Kullanıcıya 'şu menüye tıkla' demek son çaredir." Kar sistemine her yeni
/// referans eklendiğinde kullanıcıdan düğmeye basması istendi ve bir kez
/// unutuldu: `detailNormal` boş kaldı, bağlanmamış sampler NaN üretti, dağın
/// tamamı siyah çıktı. Hiçbir yerde hata mesajı yoktu.
///
/// Bu araç domain reload'dan sonra ve Play'e girmeden önce sahneyi denetliyor;
/// eksik referans varsa kurulumu koşturup tek satır bildiriyor.
///
/// EKSİK YOKSA HİÇBİR ŞEY YAPMIYOR: sahne kirletilmiyor, log basılmıyor.
[InitializeOnLoad]
public static class SnowAutoWire
{
    /// Boş olması kurulumu tetikleyen alanlar. Hepsi `SnowManager` üzerinde ve
    /// hepsi kurulumun bağlayabildiği şeyler — kullanıcı kararı olan alanlar
    /// (karakterin ayak kemiği, ateşin ısı kaynağı) burada YOK.
    static readonly string[] Required =
    {
        "settings", "simCompute", "captureShader", "skyShader",
        "groundHeight", "environmentSource", "followTarget", "detailNormal",
    };

    static SnowAutoWire()
    {
        // TEK ATIŞLIK `delayCall` YETMİYOR. Domain reload'dan hemen sonra
        // derleme veya asset import sürüyor olabiliyor; o an çıkılırsa bir
        // daha çağrılan olmuyor ve denetim sessizce atlanıyor (ölçüldü:
        // `detailNormal` boş kaldı, kurulum hiç koşmadı).
        //
        // Koşullar oluşana kadar `update`'te bekleniyor, bir kez koşup
        // kendini söküyor.
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        // Play'e girerken bir kez daha bakılıyor: sahne bu arada değişmiş
        // olabilir.
        if (state == PlayModeStateChange.ExitingEditMode) Check();
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= Tick;
        Check();
    }

    /// F1 panelinin kar alanları. Kurulum bunları da bağlıyor.
    static readonly string[] MenuRequired =
    {
        "temperature", "snowfall", "snowManager",
    };

    /// Kar yüzeyinin kendi referansları. AYRI BİLEŞEN, AYRI DENETİM: boş
    /// kalırsa `OnEnable` fırlatıyor ve mesh hiç çizilmiyor — ekrandan
    /// bakınca "kar yok" gibi görünüyor, sebebi ise tek bir boş alan.
    static readonly string[] SurfaceRequired =
    {
        "settings", "manager", "snowMaterial",
    };

    static void Count(SerializedObject so, string[] names, ref int missing, ref string first)
    {
        foreach (string name in names)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null && prop.objectReferenceValue != null) continue;

            missing++;
            first ??= name;
        }
    }

    static void Check()
    {
        if (EditorApplication.isPlaying) return;

        var manager = Object.FindAnyObjectByType<SnowManager>();
        if (manager == null) return;   // Kar sistemi kurulmamış: karışmıyoruz.

        int missing = 0;
        string first = null;

        Count(new SerializedObject(manager), Required, ref missing, ref first);

        // F1'İN REFERANSLARI DA BURADA. Önce yalnız `SnowManager` denetleniyordu
        // ve yeni bir F1 alanı eklendiğinde kurulum hiç koşmuyordu: alan boş
        // kalıyor, sürgü sessizce hiçbir şey yapmıyordu. Otomasyonun deliği
        // buydu.
        var menu = Object.FindAnyObjectByType<DebugMenu>();
        if (menu != null) Count(new SerializedObject(menu), MenuRequired, ref missing, ref first);

        var surface = Object.FindAnyObjectByType<SnowSurface>();
        if (surface != null) Count(new SerializedObject(surface), SurfaceRequired, ref missing, ref first);

        if (missing == 0) return;

        SnowDebugWindow.SetupScene();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"[Kar] {missing} referans boştu (ilki `{first}`), sahne kurulumu " +
                  "kendiliğinden koşturuldu.");
    }
}
