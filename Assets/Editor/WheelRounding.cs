using UnityEditor;
using UnityEngine;

/// JANTI ÇEMBERE OTURTUR. Üretilen ön tekerlekte kırk derecelik bir yay on iki milimetre
/// şişmiş: dönerken devirde bir kez yukarı atıyor. Ölçüldü — pivot doğru yerde (1 mm),
/// yani sebep bağlama değil, mesh'in kendisi.
///
/// Düzeltme YALNIZ YARIÇAPTA: köşe kendi açısında kalıyor, eksende kaymıyor, kalınlık
/// değişmiyor. Ölçülen dış profil ortalama yarıçapa oranlanıyor ve köşeler o oranla
/// dışa/içe çekiliyor. En büyük düzeltme %3.3, ortalama %0.6 — lastik profili bu kadarını
/// yutuyor.
///
/// GÖBEK VE TELLER DOKUNULMAZ: ağırlık dış kenardan içeri doğru sıfırlanıyor. Bütün
/// mesh ölçeklenseydi göbek deliği ovalleşir, teller birbirine girerdi.
///
/// Sonuç dosyaya yazılıyor ve git'e girmiyor: üretilen varlık repoda durmaz, menüden
/// yeniden üretilir.
public static class WheelRounding
{
    const string Folder = "Assets/Models/Bike/Generated";

    /// Bu sapmanın altındaki tekerleğe dokunulmuyor (metre). Arka tekerlek 0.9 mm ile
    /// zaten çember; düzeltmek bir şey kazandırmadan ikinci bir mesh dosyası yaratırdı.
    const float Threshold = 0.0015f;

    /// Düzeltmenin başladığı yer: dış profilin bu oranından itibaren. Altında kalan
    /// köşeler (göbek, teller, fren yüzeyi) hiç kıpırdamıyor.
    const float Inner = 0.7f;

    /// Düzeltilmiş mesh'ler bir kez üretilip dosyada duruyor; düzeltme ayarları
    /// değişirse eskisi geçersiz olur ve elle silinmesi gerekir.
    [MenuItem("To The Summit/Model/Jant Düzeltmesini Sıfırla", false, 123)]
    static void Reset()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Debug.Log("[Tekerlek] düzeltilmiş mesh yok.");
            return;
        }

        AssetDatabase.DeleteAsset(Folder);
        Debug.Log("[Tekerlek] düzeltilmiş mesh'ler silindi; kurulumda yeniden üretilecek.");
    }

    /// Tekerleği düzeltir ve düzeltilmiş mesh'i döndürür. Ölçüm sınırın altındaysa
    /// kaynak mesh olduğu gibi geri veriliyor.
    public static Mesh Round(Mesh source, Transform space, Vector3 axis,
        string assetName, string label)
    {
        // Dosya adı PARÇADAN geliyor, mesh adından değil: üretilen modelde iki parçanın
        // mesh adı aynı olabiliyor ve o durumda ikinci tekerlek birincinin dosyasını
        // okurdu — iki tekerlek tek mesh'e düşerdi.
        string path = $"{Folder}/{assetName}_Round.asset";

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        WheelProfile profile = WheelProfile.Measure(source, space, axis);

        if (profile.Deviation < Threshold)
        {
            Debug.Log($"[Tekerlek] {label} zaten çember "
                    + $"({profile.Deviation * 1000f:F1} mm sapma) — düzeltilmedi.");
            return source;
        }

        Mesh rounded = Correct(source, space, profile);

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Models/Bike", "Generated");

        AssetDatabase.CreateAsset(rounded, path);
        AssetDatabase.SaveAssets();

        WheelProfile after = WheelProfile.Measure(rounded, space, axis);

        Debug.Log($"[Tekerlek] {label} çembere oturtuldu.\n"
            + $"  sapma {profile.Deviation * 1000f:F1} mm → {after.Deviation * 1000f:F1} mm\n"
            + $"  en geniş − en dar {(profile.Max - profile.Min) * 1000f:F0} mm → "
            + $"{(after.Max - after.Min) * 1000f:F0} mm\n"
            + $"  yarıçap {after.Radius:F3} m, genişlik {after.Width * 1000f:F0} mm");

        return rounded;
    }

    static Mesh Correct(Mesh source, Transform space, WheelProfile profile)
    {
        // Ölçüm dünya uzayında, köşe verisi mesh uzayında: her köşe ölçüme gidip
        // düzeltilmiş hâlde geri geliyor. Düzeltmeyi mesh uzayında yapmak, parça
        // dönüşümündeki yüz kat ölçeği hesaba katmamak demekti.
        Matrix4x4 toWorld = space.localToWorldMatrix;
        Matrix4x4 toLocal = space.worldToLocalMatrix;

        Vector3[] vertices = source.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
            Vector3 offset = world - profile.Centre;

            float along = Vector3.Dot(offset, profile.Axis);
            float x = Vector3.Dot(offset, profile.Right);
            float y = Vector3.Dot(offset, profile.Up);

            float radius = Mathf.Sqrt(x * x + y * y);
            if (radius < 1e-5f) continue;

            float measured = profile.RadiusAt(Mathf.Atan2(y, x));
            if (measured < 1e-5f) continue;

            // Kenara yaklaştıkça düzeltme açılıyor. Sert bir sınır olsaydı jantın iç
            // kenarında görünür bir basamak kalırdı.
            float t = Mathf.Clamp01((radius / measured - Inner) / (1f - Inner));
            float weight = t * t * (3f - 2f * t);

            float scale = Mathf.Lerp(1f, profile.Radius / measured, weight);
            float target = radius * scale;

            vertices[i] = toLocal.MultiplyPoint3x4(profile.Centre + profile.Axis * along
                        + (profile.Right * x + profile.Up * y) / radius * target);
        }

        var mesh = new Mesh { name = source.name };
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(source.triangles, 0);

        // Normaller KAYNAKTAN: düzeltme yüzeyi yüzde birkaç kaydırıyor, yeniden
        // hesaplansaydı modelin kendi yumuşatması kaybolur ve jant fasetli görünürdü.
        Vector3[] normals = source.normals;
        if (normals.Length == vertices.Length) mesh.SetNormals(normals);

        Vector2[] uv = source.uv;
        if (uv.Length == vertices.Length) mesh.SetUVs(0, uv);

        mesh.RecalculateBounds();
        return mesh;
    }
}
